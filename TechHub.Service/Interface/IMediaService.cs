// Services/Interface/IMediaService.cs

using Microsoft.AspNetCore.Http;
using TechHub.Core;
using TechHub.Core.Enum;
using TechHub.Core.Model;
using TechHub.Core.ResponseModel;
using TechHub.Core.ViewModel;
using TechHub.Service.ViewModels;

namespace TechHub.Service.Interface;

public interface IMediaService
{
	/// <summary>
	/// ✅ NEW: Request upload token for direct-to-CDN upload
	/// </summary>
	Task<RequestUploadTokenResponse> RequestUploadToken(
		RequestUploadTokenViewModel model,
		AuthenticatedUserClaims userClaims);

	/// <summary>
	/// ✅ NEW: Confirm upload completed
	/// </summary>
	Task<BaseResponse> ConfirmUpload(
		ConfirmUploadViewModel model,
		AuthenticatedUserClaims userClaims);

	/// <summary>
	/// Upload media file through server (legacy)
	/// </summary>
	Task<UploadMediaResponse> UploadMedia(IFormFile file,MediaType mediaType,string displayName,AuthenticatedUserClaims userClaims);

	/// <summary>
	/// Get upload status for a media file
	/// </summary>
	Task<MediaUploadStatusResponse> GetUploadStatus(Guid mediaId);

	/// <summary>
	/// Get all media files for a class preparation
	/// </summary>
	Task<MediaFilesListResponse> GetClassMediaFiles(Guid classPreparationId);

	/// <summary>
	/// Link media files to a class preparation
	/// </summary>
	Task<BaseResponse> LinkMediaToClass(Guid classPreparationId,List<Guid> mediaIds,AuthenticatedUserClaims userClaims);

	/// <summary>
	/// Move all class media from temporary to permanent storage
	/// </summary>
	Task<BaseResponse> MoveMediaToPermanent(Guid classPreparationId);

	/// <summary>
	/// Delete all media for a rejected class
	/// </summary>
	Task<BaseResponse> DeleteMediaForRejectedClass(Guid classPreparationId,Guid deletedBy,string reason);

	/// <summary>
	/// Delete single media file
	/// </summary>
	Task<BaseResponse> DeleteMedia(Guid mediaId,Guid deletedBy,string reason);
}
