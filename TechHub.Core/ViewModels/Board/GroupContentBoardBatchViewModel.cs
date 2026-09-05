using System.Text.Json.Serialization;

namespace TechHub.Core.ViewModels.Board;

/// <summary>
/// Student equivalent of BoardBatchViewModel for group-content submissions.
/// Same 1-minute batch shape (multi-board via BoardIndex/BoardSwitches, optional
/// per-minute AudioUrl), published to a separate RabbitMQ queue so it can never
/// contend with the teacher's live board queue.
///
/// No SessionId: unlike a live classroom broadcast, a student's recording is
/// already uniquely identified by GroupId (route/body) + StudentId (JWT claims) +
/// BatchIndex — an invented session GUID would just be a second name for the same
/// key, so the document ID is built directly from IDs the caller already has.
/// </summary>
public class GroupContentBoardBatchViewModel
{
	[JsonPropertyName("groupId")]
	public string GroupId { get; set; } = string.Empty;

	[JsonPropertyName("batchIndex")]
	public int BatchIndex { get; set; }

	[JsonPropertyName("startMs")]
	public long StartMs { get; set; }

	[JsonPropertyName("endMs")]
	public long EndMs { get; set; }

	[JsonPropertyName("strokeCount")]
	public int StrokeCount { get; set; }

	[JsonPropertyName("sizeBytes")]
	public long SizeBytes { get; set; }

	[JsonPropertyName("boardIndex")]
	public int BoardIndex { get; set; }

	[JsonPropertyName("strokes")]
	public List<StrokeViewModel> Strokes { get; set; } = new();

	[JsonPropertyName("boardSwitches")]
	public List<BoardSwitchViewModel> BoardSwitches { get; set; } = new();

	[JsonPropertyName("audioUrl")]
	public string? AudioUrl { get; set; }
}
