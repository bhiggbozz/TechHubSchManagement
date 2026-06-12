using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Serilog;
using TechHub.Core.Configuration;
using TechHub.Core.Entities.Board;
using TechHub.Core.Messages;
using TechHub.Core.ViewModels.Board.Manifest;
using TechHub.Service.Interface;


namespace TechHub.Service.Repository;

public class BoardSessionRepository : IBoardSessionRepository
{
	private readonly IMongoCollection<BoardManifest> _manifests;
	private readonly IMongoCollection<BoardBatchDocument> _batches;

	private readonly ILogger _logger;

	public BoardSessionRepository(IOptions<MongoDbSettings> mongoSettings, ILogger logger)
	{
		_logger = logger;
		var client = new MongoClient(mongoSettings.Value.ConnectionString);
		var database = client.GetDatabase(mongoSettings.Value.DatabaseName);
		_manifests = database.GetCollection<BoardManifest>("board_manifests");
		_batches = database.GetCollection<BoardBatchDocument>("board_batches");
	}

	// ── Duplicate check ───────────────────────────────────────────────────────
	public async Task<bool> BatchExistsAsync(string sessionId, int batchIndex)
	{
		var indexKey = $"{sessionId}_{batchIndex}";
		return await _batches
			.Find(Builders<BoardBatchDocument>.Filter.Eq(b => b.Id, indexKey))
			.AnyAsync();
	}

	// ── Save batch — called by BoardSyncWorker ────────────────────────────────
	public async Task SaveBatchAsync(BoardBatchMessage message)
	{
		try
		{
			var indexKey = $"{message.SessionId}_{message.BatchIndex}";

			var exists = await _batches
				.Find(Builders<BoardBatchDocument>.Filter.Eq(b => b.Id, indexKey))
				.AnyAsync();

			if (exists)
			{
				_logger.Warning(
					"Duplicate batch - SessionId: {SessionId}, BatchIndex: {BatchIndex}",
					message.SessionId, message.BatchIndex);
				return;
			}

			var strokes = message.Strokes.Select(s => new BoardStroke
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
			}).ToList();

			var batchDoc = new BoardBatchDocument
			{
				Id = indexKey,
				SessionId = message.SessionId,
				SchoolId = message.SchoolId,
				BatchIndex = message.BatchIndex,
				BoardIndex = message.BoardIndex,
				StartMs = message.StartMs,
				EndMs = message.EndMs,
				StrokeCount = message.StrokeCount,
				ReceivedAt = message.ReceivedAt,
				AudioUrl = message.AudioUrl,        
				BoardSwitches = message.BoardSwitches  
					.Select(s => new BoardSwitchEvent
					{
						FromBoard = s.FromBoard,
						ToBoard = s.ToBoard,
						TimestampMs = s.TimestampMs
					}).ToList(),
				Strokes = strokes
			};

			await _batches.InsertOneAsync(batchDoc);

			// Push lightweight ref into manifest document
			var batchRef = new BatchRef
			{
				BatchIndex = message.BatchIndex,
				IndexKey = indexKey,
				StartMs = message.StartMs,
				EndMs = message.EndMs,
				StrokeCount = message.StrokeCount,
				SizeBytes = message.SizeBytes,
				BoardIndex = message.BoardIndex,
				AudioUrl = message.AudioUrl,        
				BoardSwitches = message.BoardSwitches  
					.Select(s => new BoardSwitchEvent
					{
						FromBoard = s.FromBoard,
						ToBoard = s.ToBoard,
						TimestampMs = s.TimestampMs
					}).ToList()
				};

			var manifestFilter = Builders<BoardManifest>.Filter
				.Eq(m => m.Id, message.SessionId);

			var manifestUpdate = Builders<BoardManifest>.Update
				.SetOnInsert(m => m.Id, message.SessionId)
				.SetOnInsert(m => m.LessonId, message.LessonId)
				.SetOnInsert(m => m.SchoolId, message.SchoolId)
				.SetOnInsert(m => m.TeacherId, message.TeacherId)
				.SetOnInsert(m => m.Status, SessionStatus.InProgress)
				.SetOnInsert(m => m.CreatedAt, DateTime.UtcNow)
				.Set(m => m.UpdatedAt, DateTime.UtcNow)
				.Push(m => m.BatchRefs, batchRef);

			await _manifests.UpdateOneAsync(
				manifestFilter, manifestUpdate,
				new UpdateOptions { IsUpsert = true });

			_logger.Information(
				"Batch saved - IndexKey: {IndexKey}, Strokes: {Count}",
				indexKey, strokes.Count);
		}
		catch (Exception ex)
		{
			_logger.Error(ex,
				"Failed to save batch - SessionId: {SessionId}", message.SessionId);
			throw;
		}
	}

	// ── Save manifest — called when class ends ────────────────────────────────
	public async Task SaveManifestAsync(string sessionId, SessionManifestViewModel manifest)
	{
		try
		{
			var filter = Builders<BoardManifest>.Filter.Eq(m => m.Id, sessionId);

			var update = Builders<BoardManifest>.Update
				.Set(m => m.Version, manifest.Version)
				.Set(m => m.Status, SessionStatus.Completed)
				.Set(m => m.Teacher, new TeacherInfo
				{
					Id = manifest.Session.Teacher.Id,
					Name = manifest.Session.Teacher.Name
				})
				.Set(m => m.Lesson, new LessonInfo
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
				.Set(m => m.Stats, new SessionStats
				{
					TotalDurationMs = manifest.Stats.TotalDurationMs,
					TotalDurationFormatted = manifest.Stats.TotalDurationFormatted,
					ChunkCount = manifest.Stats.ChunkCount,
					ChunkDurationMs = manifest.Stats.ChunkDurationMs,
					SeekGranularityMs = manifest.Stats.SeekGranularityMs,
					TotalAudioSizeBytes = manifest.Stats.TotalAudioSizeBytes,
					TotalStrokeCount = manifest.Stats.TotalStrokeCount,
					BoardCount = manifest.Stats.BoardCount,
					StrokeBatchCount = manifest.Stats.StrokeBatchCount
				})
				.Set(m => m.Chunks, manifest.Chunks.Select(c => new SessionChunk
				{
					Index = c.Index,
					StartMs = c.StartMs,
					EndMs = c.EndMs,
					Audio = new AudioChunk
					{
						Url = c.Audio.Url,
						MediaId = c.Audio.MediaId,
						SizeBytes = c.Audio.SizeBytes,
						DurationMs = c.Audio.DurationMs
					},
					Events = c.Events.Select(e => new ChunkEvent
					{
						//Type = e.Type,
						//TimestampMs = e.TimestampMs,
						//MediaAssetId = e.MediaAssetId
					}).ToList()
				}).ToList())
				.Set(m => m.MediaAssets, manifest.MediaAssets.Select(a => new MediaAsset
				{
					Id = a.Id,
					Name = a.Name,
					Type = a.Type,
					Url = a.Url
				}).ToList())
				.Set(m => m.Boards, manifest.Boards.Select(b => new BoardInfo
				{
					Index = b.Index,
					Dimensions = new BoardDimensions
					{
						Width = b.Dimensions.Width,
						Height = b.Dimensions.Height
					},
					StrokeCount = b.StrokeCount
				}).ToList())
				.Set(m => m.Chapters, manifest.Chapters.Select(c => new Chapter
				{
					Title = c.Title,
					StartMs = c.StartMs,
					EndMs = c.EndMs
				}).ToList())
				.Set(m => m.BoardSwitches, manifest.BoardSwitches.Select(s => new BoardSwitchEvent
				{
					FromBoard = s.FromBoard,
					ToBoard = s.ToBoard,
					TimestampMs = s.TimestampMs
				}).ToList())
				.Set(m => m.UpdatedAt, DateTime.UtcNow);

			await _manifests.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true });

			_logger.Information("Manifest saved - SessionId: {SessionId}", sessionId);
		}
		catch (Exception ex)
		{
			_logger.Error(ex,
				"Failed to save manifest - SessionId: {SessionId}", sessionId);
			throw;
		}
	}

	// ── Get manifest — lightweight, no strokes ────────────────────────────────
	public async Task<BoardManifest?> GetManifestAsync(string sessionId, string schoolId)
	{
		var filter = Builders<BoardManifest>.Filter.And(
			Builders<BoardManifest>.Filter.Eq(m => m.Id, sessionId),
			Builders<BoardManifest>.Filter.Eq(m => m.SchoolId, schoolId),
			Builders<BoardManifest>.Filter.Eq(m => m.Status, SessionStatus.Completed)
		);
		return await _manifests.Find(filter).FirstOrDefaultAsync();
	}

	// ── Get single batch by indexKey — O(1) _id lookup ───────────────────────
	public async Task<BoardBatchDocument?> GetBatchByIndexKeyAsync(string indexKey)
	{
		return await _batches
			.Find(Builders<BoardBatchDocument>.Filter.Eq(b => b.Id, indexKey))
			.FirstOrDefaultAsync();
	}

	// ── Update audio final URL after concatenation ────────────────────────────
	public async Task UpdateAudioFinalUrlAsync(string sessionId, string audioFinalUrl)
	{
		var filter = Builders<BoardManifest>.Filter.Eq(m => m.Id, sessionId);
		var update = Builders<BoardManifest>.Update
			.Set(m => m.AudioFinalUrl, audioFinalUrl)
			.Set(m => m.UpdatedAt, DateTime.UtcNow);
		await _manifests.UpdateOneAsync(filter, update);

		_logger.Information(
			"Audio final URL updated - SessionId: {SessionId}", sessionId);
	}

	// ── Mark session completed ────────────────────────────────────────────────
	public async Task MarkCompletedAsync(string sessionId)
	{
		var filter = Builders<BoardManifest>.Filter.Eq(m => m.Id, sessionId);
		var update = Builders<BoardManifest>.Update
			.Set(m => m.Status, SessionStatus.Completed)
			.Set(m => m.UpdatedAt, DateTime.UtcNow);
		await _manifests.UpdateOneAsync(filter, update);

		_logger.Information(
			"Session marked completed - SessionId: {SessionId}", sessionId);
	}

	public async Task<BoardManifest?> GetSessionAsync(string sessionId, string schoolId)
	{
		return await GetManifestAsync(sessionId, schoolId);
	}
}