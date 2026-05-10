using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Serilog;
using TechHub.Core.Configuration;
using TechHub.Core.Entities.Board;
using TechHub.Core.Messages;
using TechHub.Core.ViewModels.Board;
using TechHub.Core.ViewModels.Board.Manifest;
using TechHub.Service.Interface;

namespace TechHub.Service.Repository;

public class BoardSessionRepository : IBoardSessionRepository
{
    private readonly IMongoCollection<BoardSession> _sessions;
    private readonly ILogger _logger;

    public BoardSessionRepository(
        IOptions<MongoDbSettings> mongoSettings,
        ILogger logger)
    {
        _logger = logger;

        var client = new MongoClient(mongoSettings.Value.ConnectionString);
        var database = client.GetDatabase(mongoSettings.Value.DatabaseName);
        _sessions = database.GetCollection<BoardSession>("board_sessions");

       // EnsureIndexes();
    }

    private void EnsureIndexes()
    {
        var indexKeys = Builders<BoardSession>.IndexKeys
            .Ascending(s => s.SchoolId)
            .Ascending(s => s.Status);

        _sessions.Indexes.CreateOne(new CreateIndexModel<BoardSession>(indexKeys));

        var batchIndexKeys = Builders<BoardSession>.IndexKeys
            .Ascending(s => s.Id)
            .Ascending("batches.batchIndex");

        _sessions.Indexes.CreateOne(new CreateIndexModel<BoardSession>(batchIndexKeys));
    }

    public async Task<bool> BatchExistsAsync(string sessionId, int batchIndex)
    {
        var filter = Builders<BoardSession>.Filter.And(
            Builders<BoardSession>.Filter.Eq(s => s.Id, sessionId),
            Builders<BoardSession>.Filter.ElemMatch(s => s.Batches, b => b.BatchIndex == batchIndex)
        );

        var count = await _sessions.CountDocumentsAsync(filter);
        return count > 0;
    }

    public async Task SaveBatchAsync(BoardBatchMessage message)
    {
        var batch = new BoardBatch
        {
            BatchIndex = message.BatchIndex,
            StartMs = message.StartMs,
            EndMs = message.EndMs,
            StrokeCount = message.StrokeCount,
            SizeBytes = message.SizeBytes,
            ReceivedAt = message.ReceivedAt,
            Strokes = message.Strokes.Select(s => new BoardStroke
            {
                Id = s.Id,
                SessionId = s.SessionId,
                Type = s.Type,
                Data = s.Data,
                Color = s.Color,
                Width = s.Width,
                CurrentBoard = s.CurrentBoard,
                Timestamp = s.Timestamp,
                Duration = s.Duration,
                StartTime = s.StartTime,
                EndTime = s.EndTime
            }).ToList()
        };

        var filter = Builders<BoardSession>.Filter.Eq(s => s.Id, message.SessionId);

        var update = Builders<BoardSession>.Update
            .SetOnInsert(s => s.Id, message.SessionId)
            .SetOnInsert(s => s.LessonId, message.LessonId)
            .SetOnInsert(s => s.SchoolId, message.SchoolId)
            .SetOnInsert(s => s.TeacherId, message.TeacherId)
            .SetOnInsert(s => s.Status, SessionStatus.InProgress)
            .SetOnInsert(s => s.CreatedAt, DateTime.UtcNow)
            .Set(s => s.UpdatedAt, DateTime.UtcNow)
            .Push(s => s.Batches, batch);

        var options = new UpdateOptions { IsUpsert = true };

        await _sessions.UpdateOneAsync(filter, update, options);

        _logger.Information(
            "Saved batch {BatchIndex} for session {SessionId}, StrokeCount: {StrokeCount}",
            message.BatchIndex,
            message.SessionId,
            message.StrokeCount);
    }

    public async Task SaveManifestAsync(string sessionId, SessionManifestViewModel manifest)
    {
        var filter = Builders<BoardSession>.Filter.Eq(s => s.Id, sessionId);

        var update = Builders<BoardSession>.Update
            .Set(s => s.Version, manifest.Version)
            .Set(s => s.Status, SessionStatus.Completed)
            .Set(s => s.RecordedAt, manifest.Session.RecordedAt)
            .Set(s => s.PublishedAt, manifest.Session.PublishedAt)
            .Set(s => s.Teacher, new TeacherInfo
            {
                Id = manifest.Session.Teacher.Id,
                Name = manifest.Session.Teacher.Name
            })
            .Set(s => s.Lesson, new LessonInfo
            {
                Topic = manifest.Lesson.Topic,
                SubTopic = manifest.Lesson.SubTopic,
                Aim = manifest.Lesson.Aim,
                Subject = new SubjectInfo
                {
                    Id = manifest.Lesson.Subject.Id,
                    Name = manifest.Lesson.Subject.Name
                },
                Classroom = new ClassroomInfo
                {
                    Id = manifest.Lesson.Classroom.Id,
                    Name = manifest.Lesson.Classroom.Name
                }
            })
            .Set(s => s.Stats, new SessionStats
            {
                TotalDurationMs = manifest.Stats.TotalDurationMs,
                TotalDurationFormatted = manifest.Stats.TotalDurationFormatted,
                ChunkCount = manifest.Stats.ChunkCount,
                ChunkDurationMs = manifest.Stats.ChunkDurationMs,
                TotalAudioSizeBytes = manifest.Stats.TotalAudioSizeBytes,
                TotalStrokeCount = manifest.Stats.TotalStrokeCount,
                BoardCount = manifest.Stats.BoardCount
            })
            .Set(s => s.Chunks, manifest.Chunks.Select(c => new SessionChunk
            {
                Index = c.Index,
                StartMs = c.StartMs,
                EndMs = c.EndMs,
                Audio = new AudioChunk
                {
                    Url = c.Audio.Url,
                    SizeBytes = c.Audio.SizeBytes,
                    DurationMs = c.Audio.DurationMs
                },
                Strokes = new StrokeSummary
                {
                    Count = c.Strokes.Count,
                    SizeBytes = c.Strokes.SizeBytes
                },
                Events = c.Events
            }).ToList())
            .Set(s => s.MediaAssets, manifest.MediaAssets.Select(m => new MediaAsset
            {
                Id = m.Id,
                Name = m.Name,
                Type = m.Type,
                Url = m.Url
            }).ToList())
            .Set(s => s.Boards, manifest.Boards.Select(b => new BoardInfo
            {
                Index = b.Index,
                Dimensions = new BoardDimensions
                {
                    Width = b.Dimensions.Width,
                    Height = b.Dimensions.Height
                },
                StrokeCount = b.StrokeCount
            }).ToList())
            .Set(s => s.Chapters, manifest.Chapters.Select(c => new Chapter
            {
                TimestampMs = c.TimestampMs,
                Label = c.Label
            }).ToList())
            .Set(s => s.UpdatedAt, DateTime.UtcNow);

        var options = new UpdateOptions { IsUpsert = true };

        await _sessions.UpdateOneAsync(filter, update, options);

        _logger.Information(
            "Saved manifest for session {SessionId}, Status: Completed",
            sessionId);
    }

    public async Task<BoardSession?> GetSessionAsync(string sessionId, string schoolId)
    {
        var filter = Builders<BoardSession>.Filter.And(
            Builders<BoardSession>.Filter.Eq(s => s.Id, sessionId),
            Builders<BoardSession>.Filter.Eq(s => s.SchoolId, schoolId)
        );

        return await _sessions.Find(filter).FirstOrDefaultAsync();
    }

    public async Task UpdateAudioFinalUrlAsync(string sessionId, string audioFinalUrl)
    {
        var filter = Builders<BoardSession>.Filter.Eq(s => s.Id, sessionId);
        var update = Builders<BoardSession>.Update
            .Set(s => s.AudioFinalUrl, audioFinalUrl)
            .Set(s => s.UpdatedAt, DateTime.UtcNow);

        await _sessions.UpdateOneAsync(filter, update);

        _logger.Information(
            "Updated audio final URL for session {SessionId}",
            sessionId);
    }

    public async Task MarkCompletedAsync(string sessionId)
    {
        var filter = Builders<BoardSession>.Filter.Eq(s => s.Id, sessionId);
        var update = Builders<BoardSession>.Update
            .Set(s => s.Status, SessionStatus.Completed)
            .Set(s => s.UpdatedAt, DateTime.UtcNow);

        await _sessions.UpdateOneAsync(filter, update);

        _logger.Information(
            "Marked session {SessionId} as completed",
            sessionId);
    }
}
