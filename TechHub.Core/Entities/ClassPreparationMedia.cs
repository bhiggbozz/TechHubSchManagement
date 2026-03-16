using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.Entities;


	/// <summary>
	/// Media file associated with class preparation
	/// Supports videos, images, documents, and audio
	/// Includes upload status tracking for background processing
	/// </summary>
public class ClassPreparationMedia
{


	public Guid Id { get; set; } = Guid.NewGuid();

	/// <summary>
	/// Foreign key to ClassPreparation
	/// NULL until teacher links media to class (via SaveClassPreparation)
	/// </summary>
	public Guid? ClassPreparationId { get; set; }

	/// <summary>
	/// Unique identifier for this media file
	/// Format: {schoolId-8chars}_{yyyyMMddHHmmss}_{hash-6chars}_{filename}
	/// </summary>
	public string MediaKey { get; set; } = string.Empty;

	/// <summary>
	/// Cloudinary public_id (used for API operations)
	/// Initially same as MediaKey, updated if file moved
	/// </summary>
	public string? PublicId { get; set; }

	/// <summary>Media type: 1=Audio, 2=Video, 3=Document, 4=Image</summary>
	public int MediaType { get; set; }

	/// <summary>Original filename uploaded by teacher</summary>
	public string OriginalFileName { get; set; } = string.Empty;

	/// <summary>Display name (friendly name for UI)</summary>
	public string? DisplayName { get; set; }

	/// <summary>File size after compression (in bytes)</summary>
	public long FileSizeBytes { get; set; }

	/// <summary>Original file size before compression (in bytes)</summary>
	public long OriginalSizeBytes { get; set; }

	/// <summary>Video/audio duration in seconds</summary>
	public int? DurationSeconds { get; set; }

	/// <summary>MIME type (e.g., "video/mp4", "application/pdf")</summary>
	public string? MimeType { get; set; }

	/// <summary>File extension without dot (e.g., "mp4", "pdf")</summary>
	public string? FileExtension { get; set; }

	/// <summary>SHA256 hash for duplicate detection</summary>
	public string? SHA256Hash { get; set; }

	/// <summary>Cloudinary CDN URL (main file)</summary>
	public string CdnUrl { get; set; } = string.Empty;

	/// <summary>
	/// NEW: Auto-generated thumbnail URL
	/// For videos: Frame at 2 seconds
	/// For PDFs: First page preview
	/// Used in admin quick preview
	/// </summary>
	public string? ThumbnailUrl { get; set; }

	/// <summary>
	/// NEW: Preview URL (first 30 seconds of video)
	/// Allows admin to quickly review without full download
	/// Generated via Cloudinary transformation
	/// </summary>
	public string? PreviewUrl { get; set; }

	/// <summary>CDN provider name (default: "Cloudinary")</summary>
	public string? CdnProvider { get; set; } = "Cloudinary";

	/// <summary>
	/// Is file in temporary storage?
	/// true: temp/pending/{schoolId}/ (deleted if not linked within 7 days)
	/// false: schools/{schoolId}/ (permanent storage)
	/// </summary>
	public bool IsTemporary { get; set; } = true;

	/// <summary>
	/// Upload status: 0=Pending, 1=Uploading, 2=Completed, 3=Failed
	/// Used for tracking background upload progress
	/// </summary>
	public int UploadStatus { get; set; } = 0;

	/// <summary>Error message if upload failed</summary>
	public string? UploadErrorMessage { get; set; }

	/// <summary>
	/// NEW: AI analysis status
	/// 0=Pending (not started)
	/// 1=Processing (AI job running)
	/// 2=Completed (results available)
	/// 3=Failed (error occurred)
	/// </summary>
	public int AIAnalysisStatus { get; set; } = 0;

	/// <summary>
	/// ✅ NEW: AI analysis results (JSON format)
	/// Example: {
	///   "inappropriate": false,
	///   "educational": true,
	///   "quality": "good",
	///   "detectedTopics": ["physics", "molecules"]
	/// }
	/// </summary>
	public string? AIAnalysisData { get; set; }

	/// <summary>
	/// ✅ NEW: Key moments in video (JSON array)
	/// Example: [
	///   {"time": 0, "label": "Introduction"},
	///   {"time": 135, "label": "Main Content"}
	/// ]
	/// Used for admin to skip to important sections
	/// </summary>
	public string? KeyMoments { get; set; }

	/// <summary>
	/// ✅ NEW: Full video transcript
	/// Future feature: Speech-to-text transcription
	/// Used for search, accessibility, content analysis
	/// </summary>
	public string? TranscriptText { get; set; }

	/// <summary>Number of times file has been downloaded</summary>
	public int DownloadCount { get; set; } = 0;

	/// <summary>Last download timestamp</summary>
	public DateTime? LastDownloadDate { get; set; }

	/// <summary>User ID who last downloaded the file</summary>
	public Guid? LastDownloadedBy { get; set; }

	/// <summary>Soft delete flag (true = deleted, false = active)</summary>
	public bool IsDeleted { get; set; } = false;

	/// <summary>When file was soft deleted</summary>
	public DateTime? DeletedDate { get; set; }

	/// <summary>User ID who deleted the file</summary>
	public Guid? DeletedBy { get; set; }

	/// <summary>Reason for deletion (e.g., "Class rejected")</summary>
	public string? DeletionReason { get; set; }

	/// <summary>When file was uploaded to CDN</summary>
	public DateTime UploadedDate { get; set; } = DateTime.UtcNow;

	/// <summary>School that owns this media file</summary>
	public Guid SchoolId { get; set; }

	/// <summary>Record creation timestamp</summary>
	public string CreationDate { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

	/// <summary>Last modification timestamp</summary>
	public string? ModifiedDate { get; set; }

	/// <summary>User who uploaded the file</summary>
	public Guid CreatedBy { get; set; }

	/// <summary>Active flag (false = soft deleted at record level)</summary>
	public bool IsActive { get; set; } = true;


}
	

