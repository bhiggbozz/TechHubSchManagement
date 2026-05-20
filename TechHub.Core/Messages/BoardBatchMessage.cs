using TechHub.Core.ViewModels.Board;

namespace TechHub.Core.Messages;

public class BoardBatchMessage
{
	public string SessionId { get; set; } = string.Empty;
	public string LessonId { get; set; } = string.Empty;
	public string SchoolId { get; set; } = string.Empty;
	public string TeacherId { get; set; } = string.Empty;
	public int BatchIndex { get; set; }
	public long StartMs { get; set; }
	public long EndMs { get; set; }
	public int StrokeCount { get; set; }
	public long SizeBytes { get; set; }
	public int BoardIndex { get; set; }
	public List<StrokeViewModel> Strokes { get; set; } = new();
	public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

	public static BoardBatchMessage FromViewModel(
		BoardBatchViewModel model,
		string schoolId,
		string teacherId) => new()
		{
			SessionId = model.SessionId,
			LessonId = model.LessonId,
			SchoolId = schoolId,
			TeacherId = teacherId,
			BatchIndex = model.BatchIndex,
			StartMs = model.StartMs,
			EndMs = model.EndMs,
			StrokeCount = model.StrokeCount,
			SizeBytes = model.SizeBytes,
			BoardIndex = model.BoardIndex,
			Strokes = model.Strokes,
			ReceivedAt = DateTime.UtcNow
		};
}