using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.Helper;

public static class MediaKeyGenerator
{
	/// <summary>
	/// Generates a unique, permanent media key
	/// Format: {schoolId-short}_{timestamp}_{hash}_{sanitized-filename}
	/// Example: "e589_20250308143022_a1b2c3_physics_lecture.mp4"
	/// </summary>
	public static string GenerateMediaKey(
		Guid schoolId,
		string originalFileName,
		byte[] fileContent)
	{
		// 1. Short school ID (first 8 chars)
		var schoolPrefix = schoolId.ToString("N")[..8];

		// 2. Timestamp (yyyyMMddHHmmss)
		var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");

		// 3. Short hash from content (first 6 chars of SHA256)
		var hash = GenerateShortHash(fileContent);

		// 4. Sanitize filename
		var sanitizedName = SanitizeFileName(originalFileName);

		// 5. Combine
		var mediaKey = $"{schoolPrefix}_{timestamp}_{hash}_{sanitizedName}";

		// 6. Ensure within 100 char limit
		if (mediaKey.Length > 100)
		{
			var extension = Path.GetExtension(sanitizedName);
			var nameWithoutExt = Path.GetFileNameWithoutExtension(sanitizedName);
			var maxNameLength = 100 - schoolPrefix.Length - timestamp.Length - hash.Length - extension.Length - 4;
			nameWithoutExt = nameWithoutExt[..Math.Min(nameWithoutExt.Length, maxNameLength)];
			mediaKey = $"{schoolPrefix}_{timestamp}_{hash}_{nameWithoutExt}{extension}";
		}

		return mediaKey;
	}

	/// <summary>
	/// Generate full SHA256 hash for duplicate detection
	/// </summary>
	public static string GenerateSHA256Hash(byte[] fileContent)
	{
		using var sha256 = SHA256.Create();
		var hashBytes = sha256.ComputeHash(fileContent);
		return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
	}

	private static string GenerateShortHash(byte[] fileContent)
	{
		using var sha256 = SHA256.Create();
		var hashBytes = sha256.ComputeHash(fileContent);
		var fullHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
		return fullHash[..6];
	}

	private static string SanitizeFileName(string fileName)
	{
		fileName = Path.GetFileName(fileName);

		var extension = Path.GetExtension(fileName);
		var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);

		// Remove invalid characters
		var invalidChars = Path.GetInvalidFileNameChars();
		var sanitized = string.Join("_", nameWithoutExt.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));

		// Replace spaces
		sanitized = sanitized.Replace(" ", "_");

		// Remove consecutive underscores
		while (sanitized.Contains("__"))
			sanitized = sanitized.Replace("__", "_");

		// Lowercase
		sanitized = sanitized.ToLower();

		// Limit length
		if (sanitized.Length > 50)
			sanitized = sanitized[..50];

		return sanitized + extension.ToLower();
	}
}


