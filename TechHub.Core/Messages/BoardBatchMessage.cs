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
    public List<StrokeViewModel> Strokes { get; set; } = new();
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    public static BoardBatchMessage FromViewModel(
        BoardBatchViewModel viewModel,
        string schoolId,
        string teacherId)
    {
        return new BoardBatchMessage
        {
            SessionId = viewModel.SessionId,
            LessonId = viewModel.LessonId,
            SchoolId = schoolId,
            TeacherId = teacherId,
            BatchIndex = viewModel.BatchIndex,
            StartMs = viewModel.StartMs,
            EndMs = viewModel.EndMs,
            StrokeCount = viewModel.StrokeCount,
            SizeBytes = viewModel.SizeBytes,
            Strokes = viewModel.Strokes,
            ReceivedAt = DateTime.UtcNow
        };
    }
}
