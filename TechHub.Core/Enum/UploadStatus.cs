// TechHub.Core/Enums/UploadStatus.cs

namespace TechHub.Core.Enums;

	/// <summary>
	/// Upload status for media files
	/// Tracks progression from queue to completion
	/// </summary>
public enum UploadStatus
{
	/// <summary>
	/// Queued but not started yet
	/// </summary>
	Pending = 0,

	/// <summary>
	/// Currently uploading to Cloudinary
	/// </summary>
	Uploading = 1,

	/// <summary>
	/// Successfully uploaded and processed
	/// </summary>
	Completed = 2,

	/// <summary>
	/// Upload failed (check UploadErrorMessage)
	/// </summary>
	Failed = 3,
	AwaitingUpload = 4
}

/// <summary>
///NEW: AI content analysis status
/// </summary>
public enum AIAnalysisStatus
{
	/// <summary>Analysis not started</summary>
	Pending = 0,

	/// <summary>AI job currently running</summary>
	Processing = 1,

	/// <summary>Analysis completed, results available</summary>
	Completed = 2,

	/// <summary>Analysis failed, error occurred</summary>
	Failed = 3
}
