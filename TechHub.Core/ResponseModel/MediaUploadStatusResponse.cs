// TechHub.Service/ViewModels/MediaViewModels.cs

using TechHub.Core;
using TechHub.Core.DTO;
using TechHub.Core.Enums;
using TechHub.Core.ViewModel;

namespace TechHub.Service.ViewModels
{
	/// <summary>
	/// Response for upload status check
	/// Used by frontend to poll progress
	/// </summary>
	public class MediaUploadStatusResponse : BaseResponse
	{
		//public ResponseCode ResponseCode { get; set; }
		//public string ResponseMessage { get; set; } = string.Empty;
		//public string Status { get; set; } = string.Empty;

		// Upload status details
		public Guid MediaId { get; set; }
		public int UploadStatus { get; set; }
		public string UploadStatusName { get; set; } = string.Empty;
		public string? ErrorMessage { get; set; }

		// Available after completion
		public string CdnUrl { get; set; } = string.Empty;
		public string? ThumbnailUrl { get; set; }
		public long FileSizeBytes { get; set; }
		public string FileSizeFormatted { get; set; } = string.Empty;
	}

	/// <summary>
	/// Response for media upload
	/// </summary>
	public class UploadMediaResponse : BaseResponse
	{
		//public ResponseCode ResponseCode { get; set; }
		//public string ResponseMessage { get; set; } = string.Empty;
		//public string Status { get; set; } = string.Empty;
		public MediaFileDto? MediaFile { get; set; }
	}

	/// <summary>
	/// Response for getting media files list
	/// </summary>
	public class MediaFilesListResponse : BaseResponse
	{
		//public ResponseCode ResponseCode { get; set; }
		//public string ResponseMessage { get; set; } = string.Empty;
		//public string Status { get; set; } = string.Empty;
		public List<MediaFileDto> MediaFiles { get; set; } = new();
		public int TotalCount { get; set; }
	}

	
}