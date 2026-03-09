using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.Configuration;

public class CloudinarySettings
{
	public const string SectionName = "Cloudinary";

	[Required(ErrorMessage = "Cloudinary CloudName is required")]
	public string CloudName { get; set; } = string.Empty;

	[Required(ErrorMessage = "Cloudinary ApiKey is required")]
	public string ApiKey { get; set; } = string.Empty;

	[Required(ErrorMessage = "Cloudinary ApiSecret is required")]
	public string ApiSecret { get; set; } = string.Empty;

	public CloudinaryAppSettings Settings { get; set; } = new();
}

public class CloudinaryAppSettings
{
	public FolderStructureSettings FolderStructure { get; set; } = new();
	public CompressionSettings Compression { get; set; } = new();
	public FileLimitsSettings FileLimits { get; set; } = new();
}

public class FolderStructureSettings
{
	public string TempPending { get; set; } = "temp/pending/{schoolId}";
	public string Permanent { get; set; } = "schools/{schoolId}";
}
