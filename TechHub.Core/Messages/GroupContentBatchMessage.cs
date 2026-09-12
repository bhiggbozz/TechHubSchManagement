using TechHub.Core.ViewModels.Board;

namespace TechHub.Core.Messages;

/// <summary>
/// Queue payload for student group-content board batches. Deliberately a separate
/// type from BoardBatchMessage (not just extra fields on it) so the two never share
/// a queue, a consumer, or a Mongo collection — see GroupContentBatchQueue.
///
/// No SessionId — GroupId + StudentId + ContentId + BatchIndex is the whole key.
/// </summary>
public class GroupContentBatchMessage
{
	public string GroupId { get; set; } = string.Empty;
	public string SchoolId { get; set; } = string.Empty;
	public string StudentId { get; set; } = string.Empty;
	public string ContentId { get; set; } = string.Empty;
	public int BatchIndex { get; set; }
	public long StartMs { get; set; }
	public long EndMs { get; set; }
	public int StrokeCount { get; set; }
	public long SizeBytes { get; set; }
	public int BoardIndex { get; set; }
	public List<StrokeViewModel> Strokes { get; set; } = new();
	public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
	public string? AudioUrl { get; set; }
	public List<BoardSwitchViewModel> BoardSwitches { get; set; } = new();

	public static GroupContentBatchMessage FromViewModel(
		GroupContentBoardBatchViewModel model,
		string schoolId,
		string studentId) => new()
		{
			GroupId = model.GroupId,
			SchoolId = schoolId,
			StudentId = studentId,
			ContentId = model.ContentId,
			BatchIndex = model.BatchIndex,
			StartMs = model.StartMs,
			EndMs = model.EndMs,
			StrokeCount = model.StrokeCount,
			SizeBytes = model.SizeBytes,
			BoardIndex = model.BoardIndex,
			Strokes = model.Strokes,
			ReceivedAt = DateTime.UtcNow,
			AudioUrl = model.AudioUrl,
			BoardSwitches = model.BoardSwitches
		};
}
