using TechHub.Core.ViewModels.Board.Manifest;

namespace TechHub.Core.ViewModels.Board;

/// <summary>
/// Student equivalent of the teacher's SessionManifestViewModel — finalizes a
/// group-content board recording. No lesson/teacher metadata (that lives on
/// GroupLessonContent, set separately via SubmitContent) — just the recording's
/// own stats, batch refs, boards, and final audio URL.
///
/// No sessionId — keyed by GroupId (route/body) + the caller's StudentId (JWT).
/// </summary>
public class GroupContentManifestViewModel
{
	public string GroupId { get; set; } = string.Empty;
	public ManifestStatsViewModel Stats { get; set; } = new();
	public List<StrokeBatchRefViewModel> StrokeBatches { get; set; } = new();
	public List<BoardInfoViewModel> Boards { get; set; } = new();
	public List<BoardSwitchViewModel> BoardSwitches { get; set; } = new();
	public string? AudioFinalUrl { get; set; }
}
