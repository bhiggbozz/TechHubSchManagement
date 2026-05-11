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
    private readonly IMongoCollection<BoardSession> _collection;

    private readonly ILogger _logger;

    public BoardSessionRepository(IOptions<MongoDbSettings> mongoSettings, ILogger logger)
    {
        _logger = logger;

        var client = new MongoClient(mongoSettings.Value.ConnectionString);
        var database = client.GetDatabase(mongoSettings.Value.DatabaseName);
        _sessions = database.GetCollection<BoardSession>("board_sessions");
        _collection = database.GetCollection<BoardSession>("BoardSessions");

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
		try
		{
			// Check for duplicate batchIndex before pushing
			var duplicateFilter = Builders<BoardSession>.Filter.And(
				Builders<BoardSession>.Filter.Eq(s => s.Id, message.SessionId),
				Builders<BoardSession>.Filter.ElemMatch(s => s.Batches,
					Builders<BoardBatch>.Filter.Eq(b => b.BatchIndex, message.BatchIndex))
			);

			var exists = await _sessions.Find(duplicateFilter).AnyAsync();
			if (exists)
			{
				_logger.Warning(
					"Duplicate batch ignored - SessionId: {SessionId}, BatchIndex: {BatchIndex}",
					message.SessionId, message.BatchIndex);
				return;
			}

			// Map strokes from StrokeViewModel to BoardStroke entity
			//var strokes = message.Strokes.Select(s => new BoardStroke
			//{
			//	Id = s.Id,
			//	SessionId = s.SessionId,
			//	Pts = s.Pts,
			//	Color = s.C,           // ViewModel uses C, entity uses Color
			//	Width = s.W,           // ViewModel uses W, entity uses Width
			//	Timestamp = s.Ts,          // ViewModel uses Ts, entity uses Timestamp
			//	CurrentBoard = s.CurrentBoard
			//}).ToList();

			var batch = new BoardBatch
			{
				BatchIndex = message.BatchIndex,
				IndexKey = $"{message.LessonId}_{message.BatchIndex}",
				StartMs = message.StartMs,
				EndMs = message.EndMs,
				StrokeCount = message.StrokeCount,
				SizeBytes = message.SizeBytes,
				ReceivedAt = message.ReceivedAt,
				//Strokes = strokes
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
				"Saved batch {BatchIndex} for session {SessionId}, StrokeCount: {StrokeCount}, IndexKey: {IndexKey}",
				message.BatchIndex, message.SessionId, message.StrokeCount, batch.IndexKey);
		}
		catch (Exception ex)
		{
			_logger.Error(ex,
				"Failed to save batch - SessionId: {SessionId}, BatchIndex: {BatchIndex}",
				message.SessionId, message.BatchIndex);
			throw;
		}
	}

	public async Task SaveManifestAsync(string sessionId, SessionManifestViewModel manifest)
	{
		try
		{
			var filter = Builders<BoardSession>.Filter.Eq(s => s.Id, sessionId);

			var update = Builders<BoardSession>.Update
				.Set(s => s.Version, manifest.Version)
				.Set(s => s.Status, SessionStatus.Completed)
				//.Set(s => s.PublishedAt, manifest.Session.PublishedAt)
				//.Set(s => s.RecordedAt, manifest.Session.RecordedAt)
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
					ChunkCount = manifest.Stats.ChunkCount,
					//StrokeBatchCount = manifest.Stats.StrokeBatchCount
				})
				.Set(s => s.Chunks, manifest.Chunks.Select(c => new SessionChunk
				{
					Index = c.Index,
					StartMs = c.StartMs,
					EndMs = c.EndMs,
					Audio = new AudioChunk
					{
						Url = c.Audio.Url,
						//MediaId = c.Audio.MediaId
					}
				}).ToList())
				.Set(s => s.UpdatedAt, DateTime.UtcNow);

			var options = new UpdateOptions { IsUpsert = true };
			await _sessions.UpdateOneAsync(filter, update, options);

			_logger.Information(
				"Manifest saved - SessionId: {SessionId}, ChunkCount: {ChunkCount}, StrokeBatchCount: {StrokeBatchCount}",
				sessionId, manifest.Stats.ChunkCount, manifest.Stats.StrokeBatchCount);
		}
		catch (Exception ex)
		{
			_logger.Error(ex, "Failed to save manifest - SessionId: {SessionId}", sessionId);
			throw;
		}
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

    public async Task<BoardSession?> GetManifestAsync(string sessionId, string schoolId)
    {
        var filter = Builders<BoardSession>.Filter.And(
            Builders<BoardSession>.Filter.Eq(s => s.Id, sessionId),
            Builders<BoardSession>.Filter.Eq(s => s.SchoolId, schoolId),
            Builders<BoardSession>.Filter.Eq(s => s.Status, SessionStatus.Completed));

        // Exclude raw strokes from batches — manifest + batch refs only
        var projection = Builders<BoardSession>.Projection
            .Exclude("batches.strokes");

        return await _collection
            .Find(filter)
            .Project<BoardSession>(projection)
            .FirstOrDefaultAsync();
    }

    public async Task<BoardBatch?> GetBatchByIndexKeyAsync(string sessionId, string schoolId, string indexKey)
    {
        var filter = Builders<BoardSession>.Filter.And(
            Builders<BoardSession>.Filter.Eq(s => s.Id, sessionId),
            Builders<BoardSession>.Filter.Eq(s => s.SchoolId, schoolId)
        );

        var session = await _collection
            .Find(filter)
            .Project<BoardSession>(
                Builders<BoardSession>.Projection
                    .ElemMatch(s => s.Batches,
                        Builders<BoardBatch>.Filter
                            .Eq(b => b.IndexKey, indexKey)))
            .FirstOrDefaultAsync();

        return session?.Batches?.FirstOrDefault();
    }
}

	//public async Task<BoardSession?> GetManifestAsync(string sessionId, string schoolId)
	//{
	//	var filter = Builders<BoardSession>.Filter.And(
	//		Builders<BoardSession>.Filter.Eq(s => s.Id, sessionId),
	//		Builders<BoardSession>.Filter.Eq(s => s.SchoolId, schoolId),
	//		Builders<BoardSession>.Filter.Eq(s => s.Status, "Completed")
	//	);

//	// Return manifest + batch index references only
//	// Exclude the strokes array inside each batch — can be several MB
//	var projection = Builders<BoardSession>.Projection
//		.Exclude("batches.strokes");

//	return await _collection
//		.Find(filter)
//		.Project<BoardSession>(projection)
//		.FirstOrDefaultAsync();
//}

//public async Task<BoardSessionBatch?> GetBatchAsync(string sessionId, string schoolId, int batchIndex)
//{
//	var filter = Builders<BoardSession>.Filter.And(
//		Builders<BoardSession>.Filter.Eq(s => s.Id, sessionId),
//		Builders<BoardSession>.Filter.Eq(s => s.SchoolId, schoolId)
//	);

//	var session = await _collection
//		.Find(filter)
//		.Project<BoardSession>(
//			Builders<BoardSession>.Projection
//				.ElemMatch(s => s.Batches,
//					Builders<BoardSessionBatch>.Filter.Eq(b => b.BatchIndex, batchIndex))
//				.Include("batches.$"))
//		.FirstOrDefaultAsync();

//	return session?.Batches?.FirstOrDefault();
//}

//	public async Task<BoardSession?> GetSessionWithManifestAsync(
//	string sessionId, string schoolId)
//	{
//		var filter = Builders<BoardSession>.Filter.And(
//			Builders<BoardSession>.Filter.Eq(s => s.Id, sessionId),
//			Builders<BoardSession>.Filter.Eq(s => s.SchoolId, schoolId),
//			Builders<BoardSession>.Filter.Eq(s => s.Status, "Completed")
//		);

//		// Project only the manifest — exclude the batches array
//		// Batches contain raw strokes and are not needed by the student
//		// Manifest has everything needed for download
//		var projection = Builders<BoardSession>.Projection
//			.Exclude(s => s.Batches);

//		return await _collection
//			.Find(filter)
//			.Project<BoardSession>(projection)
//			.FirstOrDefaultAsync();
//	}
//}
