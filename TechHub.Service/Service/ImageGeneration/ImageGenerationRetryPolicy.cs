using System.Net;

namespace TechHub.Service.Service.ImageGeneration;

/// <summary>
/// Shared retry policy for image-generation provider calls. Capped at 2
/// retries (3 attempts total) and only for conditions that are actually
/// worth retrying — timeouts, rate limits (429) and provider 5xx errors.
/// Permanent failures (400/401/403 — bad request, bad key, billing block)
/// are never retried since another attempt cannot fix them.
/// </summary>
internal static class ImageGenerationRetryPolicy
{
	public const int MaxAttempts = 3;

	private static readonly int[] BackoffSeconds = { 2, 5 };

	public static bool IsTransient(HttpStatusCode statusCode)
		=> (int)statusCode == 429 || (int)statusCode >= 500;

	/// <summary>Delay to wait after the given attempt (1-based) before retrying.</summary>
	public static TimeSpan GetDelay(int attempt)
	{
		var index = Math.Clamp(attempt - 1, 0, BackoffSeconds.Length - 1);
		return TimeSpan.FromSeconds(BackoffSeconds[index]);
	}
}
