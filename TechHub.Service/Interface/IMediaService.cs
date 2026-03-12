// Services/Interface/IMediaService.cs

using Microsoft.AspNetCore.Http;
using TechHub.Core;
using TechHub.Core.Enum;
using TechHub.Core.Model;
using TechHub.Core.ResponseModel;
using TechHub.Service.ViewModels;

namespace TechHub.Service.Interface;

/// <summary>
/// Service for managing media files (upload, link, move, delete)
/// </summary>
public interface IMediaService
{
	/// <summary>
	/// Upload media file to Cloudinary and save metadata to database
	/// </summary>
	/// <param name="file">Uploaded file from controller</param>
	/// <param name="mediaType">Type of media (Video, Image, Document, Audio)</param>
	/// <param name="displayName">Optional friendly name for display</param>
	/// <param name="userClaims">Authenticated user claims (UserId, SchoolId, etc.)</param>
	/// <returns>Upload result with media file details</returns>
	Task<UploadMediaResponse> UploadMedia(IFormFile file,MediaType mediaType,string? displayName,AuthenticatedUserClaims userClaims);

	/// <summary>
	/// Link uploaded media files to a class preparation
	/// </summary>
	/// <param name="classPreparationId">Class to link media to</param>
	/// <param name="mediaIds">List of media file IDs</param>
	/// <param name="userClaims">Authenticated user claims</param>
	/// <returns>Success or failure</returns>
	Task<BaseResponse> LinkMediaToClass(Guid classPreparationId,List<Guid> mediaIds,AuthenticatedUserClaims userClaims);

	/// <summary>
	/// Move all media for a class from temporary to permanent storage
	/// Called when admin approves a class
	/// </summary>
	/// <param name="classPreparationId">Approved class ID</param>
	/// <returns>Success or failure with count of files moved</returns>
	Task<BaseResponse> MoveMediaToPermanent(Guid classPreparationId);

	/// <summary>
	/// Delete all media for a rejected class
	/// Soft deletes in database and physically deletes from Cloudinary
	/// </summary>
	/// <param name="classPreparationId">Rejected class ID</param>
	/// <param name="deletedBy">Admin who rejected the class</param>
	/// <param name="reason">Rejection reason</param>
	/// <returns>Success or failure with count of files deleted</returns>
	Task<BaseResponse> DeleteMediaForRejectedClass(Guid classPreparationId,Guid deletedBy,string reason);

	/// <summary>
	/// Get all media files for a class preparation
	/// </summary>
	/// <param name="classPreparationId">Class ID</param>
	/// <returns>List of media files</returns>
	Task<MediaFilesListResponse> GetClassMediaFiles(Guid classPreparationId);

	/// <summary>
	/// Delete a single media file (soft delete)
	/// </summary>
	/// <param name="mediaId">Media file ID</param>
	/// <param name="deletedBy">User deleting the file</param>
	/// <param name="reason">Deletion reason</param>
	/// <returns>Success or failure</returns>
	Task<BaseResponse> DeleteMedia(Guid mediaId,Guid deletedBy,string? reason);
	/// <summary>
	/// Get upload status for a media file
	/// Used by frontend to poll progress
	/// </summary>
	Task<MediaUploadStatusResponse> GetUploadStatus(Guid mediaId);
}
