using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.Model;

public static class LessonStatus
{
	public const string PendingApproval = "PendingApproval";
	public const string Approved = "Approved";
	public const string Rejected = "Rejected";
	public const string Published = "Published";
}

public static class MediaTypes
{
	public const string Video = "Video";
	public const string Audio = "Audio";
	public const string PDF = "PDF";
	public const string Image = "Image";
	public const string Document = "Document";

	public static string Resolve(string extension) =>
		extension?.ToLower() switch
		{
			".mp4" or ".mov" or ".avi" or ".mkv" or ".webm" => Video,
			".mp3" or ".wav" or ".m4a" or ".aac" => Audio,
			".pdf" => PDF,
			".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" => Image,
			".doc" or ".docx" or ".ppt" or ".pptx" => Document,
			_ => Document
		};
}

