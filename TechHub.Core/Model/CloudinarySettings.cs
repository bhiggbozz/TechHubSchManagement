//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace TechHub.Core.Model
//{
//	public class CloudinarySettings
//	{
//		public string CloudName { get; set; } = string.Empty;
//		public string ApiKey { get; set; } = string.Empty;
//		public string ApiSecret { get; set; } = string.Empty;
//		public CloudinaryAppSettings Settings { get; set; }
//			= new();
//	}

//	public class CloudinaryAppSettings
//	{
//		public FileLimits FileLimits { get; set; } = new();
//		public CompressionSettings Compression { get; set; }
//			= new();
//		public FolderStructure FolderStructure { get; set; }
//			= new();
//	}

//	public class FileLimits
//	{
//		public int MaxFileSizeMB { get; set; } = 100;
//	}

//	public class CompressionSettings
//	{
//		public VideoCompression Video { get; set; } = new();
//		public ImageCompression Image { get; set; } = new();
//	}

//	public class VideoCompression
//	{
//		public int MaxWidth { get; set; } = 720;
//		public string Bitrate { get; set; } = "1m";
//	}

//	public class ImageCompression
//	{
//		public int MaxWidth { get; set; } = 1920;
//	}

//	public class FolderStructure
//	{
//		public string TempPending { get; set; }
//			= "temp/pending/{schoolId}";
//		public string Permanent { get; set; }
//			= "schools/{schoolId}";
//	}
//}
