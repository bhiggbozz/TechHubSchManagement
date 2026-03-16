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
	public static string GenerateMediaKey(Guid schoolId,string originalFileName,byte[] fileContent)
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

	/// <summary>
	/// Generate unique media key for Cloudinary storage
	/// 
	/// USAGE:
	/// var mediaKey = MediaKeyGenerator.GenerateKey(schoolId, "Physics Lecture.mp4");
	/// // Returns: "a1b2c3d4_20250315143025_f7a8b9_physics-lecture.mp4"
	/// 
	/// ALGORITHM:
	/// 1. Extract first 8 characters of school ID
	/// 2. Get current timestamp (yyyyMMddHHmmss)
	/// 3. Generate 6-character random hash
	/// 4. Sanitize filename (remove special characters)
	/// 5. Combine into format: {schoolId}_{timestamp}_{hash}_{filename}
	/// </summary>
	/// <param name="schoolId">School GUID</param>
	/// <param name="fileName">Original filename with extension</param>
	/// <returns>Unique sanitized media key</returns>
	public static string GenerateKey(Guid schoolId, string fileName)
	{
		// Extract first 8 characters of school ID
		// Example: a1b2c3d4-e5f6-7890-abcd-ef1234567890 → a1b2c3d4
		var schoolPrefix = schoolId.ToString("N").Substring(0, 8);

		// Generate timestamp
		// Format: yyyyMMddHHmmss (20250315143025)
		var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");

		// Generate random 6-character hash for uniqueness
		// Prevents collisions if two files uploaded at same second
		var randomHash = GenerateRandomHash(6);

		// Sanitize filename
		var sanitizedFileName = SanitizeFileName(fileName);

		// Combine components
		// Format: {schoolPrefix}_{timestamp}_{hash}_{sanitizedFileName}
		var mediaKey = $"{schoolPrefix}_{timestamp}_{randomHash}_{sanitizedFileName}";

		return mediaKey;
	}

	/// <summary>
	/// Generate random alphanumeric hash
	/// 
	/// USED FOR: Adding uniqueness to prevent filename collisions
	/// 
	/// ALGORITHM:
	/// 1. Generate random bytes using cryptographically secure RNG
	/// 2. Convert to Base64
	/// 3. Remove non-alphanumeric characters
	/// 4. Take first N characters
	/// 5. Convert to lowercase
	/// </summary>
	/// <param name="length">Desired hash length (default 6)</param>
	/// <returns>Random lowercase alphanumeric string</returns>
	private static string GenerateRandomHash(int length = 6)
	{
		using (var rng = RandomNumberGenerator.Create())
		{
			// Generate random bytes (need more than length to account for filtering)
			var bytes = new byte[length * 2];
			rng.GetBytes(bytes);

			// Convert to Base64 and remove non-alphanumeric characters
			var hash = Convert.ToBase64String(bytes)
				.Replace("+", "")
				.Replace("/", "")
				.Replace("=", "")
				.ToLowerInvariant();

			// Take first N characters
			return hash.Substring(0, Math.Min(length, hash.Length));
		}
	}
}


