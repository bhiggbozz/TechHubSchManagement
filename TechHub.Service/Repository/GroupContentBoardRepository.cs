using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Serilog;
using TechHub.Core.Configuration;
using TechHub.Core.Entities.Board;
using TechHub.Core.Messages;
using TechHub.Core.ViewModels.Board;
using TechHub.Service.Interface;

namespace TechHub.Service.Repository;

/// <summary>
/// Own Mongo collections ("group_content_batches", "group_content_manifests"),
/// separate from the teacher's "board_batches"/"board_manifests" — a slow or
/// backed-up group-content consumer can never affect teacher board reads/writes.
/// </summary>
public class GroupContentBoardRepository : IGroupContentBoardRepository
{
	private readonly IMongoCollection<GroupContentBatchDocument> _batches;
	private readonly IMongoCollection<GroupContentManifestDocument> _manifests;
	private readonly ILogger _logger;

	public GroupContentBoardRepository(IOptions<MongoDbSettings> mongoSettings, ILogger logger)
	{
		_logger = logger;
		var client = new MongoClient(mongoSettings.Value.ConnectionString);
		var database = client.GetDatabase(mongoSettings.Value.DatabaseName);
		_batches = database.GetCollection<GroupContentBatchDocument>("group_content_batches");
		_manifests = database.GetCollection<GroupContentManifestDocument>("group_content_manifests");

		// _id lookups (BatchExistsAsync/SaveBatchAsync) are covered by Mongo's automatic
		// _id index. GetLatestBatchIndexAsync queries by GroupId+StudentId instead, so it
		// needs its own index or it would fall back to a collection scan.
		var indexKeys = Builders<GroupContentBatchDocument>.IndexKeys
			.Ascending(b => b.GroupId)
			.Ascending(b => b.StudentId)
			.Descending(b => b.BatchIndex);
		_batches.Indexes.CreateOne(new CreateIndexModel<GroupContentBatchDocument>(indexKeys));
	}

	private static string BuildIndexKey(string groupId, string studentId, int batchIndex) =>
		$"{groupId}_{studentId}_{batchIndex}";

	public async Task<bool> BatchExistsAsync(string groupId, string studentId, int batchIndex)
	{
		var indexKey = BuildIndexKey(groupId, studentId, batchIndex);
		return await _batches
			.Find(Builders<GroupContentBatchDocument>.Filter.Eq(b => b.Id, indexKey))
			.AnyAsync();
	}

	public async Task SaveBatchAsync(GroupContentBatchMessage message)
	{
		try
		{
			var indexKey = BuildIndexKey(message.GroupId, message.StudentId, message.BatchIndex);

			var exists = await _batches
				.Find(Builders<GroupContentBatchDocument>.Filter.Eq(b => b.Id, indexKey))
				.AnyAsync();

			if (exists)
			{
				_logger.Warning(
					"Duplicate group-content batch - GroupId: {GroupId}, StudentId: {StudentId}, BatchIndex: {BatchIndex}",
					message.GroupId, message.StudentId, message.BatchIndex);
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

			var batchDoc = new GroupContentBatchDocument
			{
				Id = indexKey,
				GroupId = message.GroupId,
				SchoolId = message.SchoolId,
				StudentId = message.StudentId,
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

			_logger.Information(
				"Group-content batch saved - IndexKey: {IndexKey}, Strokes: {Count}",
				indexKey, strokes.Count);
		}
		catch (Exception ex)
		{
			_logger.Error(ex,
				"Failed to save group-content batch - GroupId: {GroupId}, StudentId: {StudentId}", message.GroupId, message.StudentId);
			throw;
		}
	}

	public async Task SaveManifestAsync(string groupId, string studentId, string schoolId, GroupContentManifestViewModel manifest)
	{
		try
		{
			var id = $"{groupId}_{studentId}";

			var document = new GroupContentManifestDocument
			{
				Id = id,
				GroupId = groupId,
				StudentId = studentId,
				SchoolId = schoolId,
				Stats = new SessionStats
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
				},
				StrokeBatches = manifest.StrokeBatches.Select(b => new BatchRef
				{
					BatchIndex = b.BatchIndex,
					IndexKey = b.IndexKey,
					StartMs = b.StartMs,
					EndMs = b.EndMs,
					StrokeCount = b.StrokeCount,
					SizeBytes = b.SizeBytes
				}).ToList(),
				Boards = manifest.Boards.Select(b => new BoardInfo
				{
					Index = b.Index,
					Dimensions = new BoardDimensions
					{
						Width = b.Dimensions.Width,
						Height = b.Dimensions.Height
					},
					StrokeCount = b.StrokeCount
				}).ToList(),
				BoardSwitches = manifest.BoardSwitches.Select(s => new BoardSwitchEvent
				{
					FromBoard = s.FromBoard,
					ToBoard = s.ToBoard,
					TimestampMs = s.TimestampMs
				}).ToList(),
				AudioFinalUrl = manifest.AudioFinalUrl,
				UpdatedAt = DateTime.UtcNow
			};

			var filter = Builders<GroupContentManifestDocument>.Filter.Eq(m => m.Id, id);
			var options = new ReplaceOptions { IsUpsert = true };

			await _manifests.ReplaceOneAsync(filter, document, options);

			_logger.Information(
				"Group-content manifest saved - GroupId: {GroupId}, StudentId: {StudentId}", groupId, studentId);
		}
		catch (Exception ex)
		{
			_logger.Error(ex,
				"Failed to save group-content manifest - GroupId: {GroupId}, StudentId: {StudentId}", groupId, studentId);
			throw;
		}
	}

	public async Task<int?> GetLatestBatchIndexAsync(string groupId, string studentId)
	{
		var filter = Builders<GroupContentBatchDocument>.Filter.And(
			Builders<GroupContentBatchDocument>.Filter.Eq(b => b.GroupId, groupId),
			Builders<GroupContentBatchDocument>.Filter.Eq(b => b.StudentId, studentId));

		var latest = await _batches
			.Find(filter)
			.SortByDescending(b => b.BatchIndex)
			.Limit(1)
			.FirstOrDefaultAsync();

		return latest?.BatchIndex;
	}

	public async Task<GroupContentManifestDocument?> GetManifestAsync(string groupId, string studentId)
	{
		var id = $"{groupId}_{studentId}";
		return await _manifests
			.Find(Builders<GroupContentManifestDocument>.Filter.Eq(m => m.Id, id))
			.FirstOrDefaultAsync();
	}
}
