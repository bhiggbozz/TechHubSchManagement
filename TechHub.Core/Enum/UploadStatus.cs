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
		Failed = 3
	}
