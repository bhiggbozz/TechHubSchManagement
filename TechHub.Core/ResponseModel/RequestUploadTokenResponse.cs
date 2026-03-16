using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core.DTO;

namespace TechHub.Core.ResponseModel;

/// <summary>
/// Response containing upload token
/// Frontend uses this to upload directly to Cloudinary
/// Contains all credentials needed for direct upload
/// </summary>
public class RequestUploadTokenResponse : BaseResponse
{
	/// <summary>Database record ID (used to track upload status)</summary>
	public Guid MediaId { get; set; }

	/// <summary>Cloudinary upload endpoint URL</summary>
	public string UploadUrl { get; set; }

	/// <summary>Cryptographic signature (proves request from your server)</summary>
	public string Signature { get; set; }

	/// <summary>Unix timestamp when token was created</summary>
	public long Timestamp { get; set; }

	/// <summary>Cloudinary public_id for the file</summary>
	public string PublicId { get; set; }

	/// <summary>Folder where file will be stored</summary>
	public string Folder { get; set; }

	/// <summary>Your Cloudinary API key (safe to expose)</summary>
	public string ApiKey { get; set; }

	/// <summary>Your Cloudinary cloud name</summary>
	public string CloudName { get; set; }

	/// <summary>When token expires (ISO 8601 format)</summary>
	public string ExpiresAt { get; set; }
}

//public class UploadMediaResponse : BaseResponse
//{
//	public MediaFileDto MediaFile { get; set; }
//}


