using TechHub.Core.ViewModels.Board.Manifest;

namespace TechHub.Core.ViewModels.Board;

/// <summary>
/// Student equivalent of the teacher's SessionManifestViewModel — finalizes a
/// group-content board recording. No lesson/teacher metadata (that lives on
/// GroupLessonContent, set separately via SubmitContent) — just the recording's
/// own stats, batch refs, boards, and final audio URL.
///
/// No sessionId — keyed by GroupId (route/body) + the caller's StudentId (JWT) +
/// ContentId (route/body), scoping the recording to one specific
/// GroupLessonContent submission.
/// </summary>
public class GroupContentManifestViewModel
{
	public string GroupId { get; set; } = string.Empty;
	public string ContentId { get; set; } = string.Empty;
	public ManifestStatsViewModel Stats { get; set; } = new();
	public List<StrokeBatchRefViewModel> StrokeBatches { get; set; } = new();
	public List<BoardInfoViewModel> Boards { get; set; } = new();
	public List<BoardSwitchViewModel> BoardSwitches { get; set; } = new();

	// Legacy single-file slot — always null in practice, since the actual upload
	// pipeline (useSessionUpload.ts) never produces one continuous audio file.
	// Left in place rather than removed; AudioChunks below is the real data.
	public string? AudioFinalUrl { get; set; }

	// One entry per 60s upload batch (chunkIndex = batch index, same granularity
	// as StrokeBatches) — mirrors how audio actually gets uploaded, unlike the
	// single AudioFinalUrl slot above.
	public List<AudioChunkRefViewModel> AudioChunks { get; set; } = new();
}

public class AudioChunkRefViewModel
{
	public int ChunkIndex { get; set; }
	public string Url { get; set; } = string.Empty;
	public string? MediaId { get; set; }
}
