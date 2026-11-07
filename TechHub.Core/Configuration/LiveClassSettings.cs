namespace TechHub.Core.Configuration
{
	/// <summary>
	/// Connection to the self-hosted LiveKit media server that carries live-class audio.
	/// Secrets come from environment variables / user-secrets (LiveKit__ApiKey, LiveKit__ApiSecret),
	/// never from committed files.
	/// </summary>
	public class LiveKitSettings
	{
		public const string SectionName = "LiveKit";

		/// <summary>HTTPS base URL the API uses to call LiveKit's room API (e.g. https://live.bluetsch.com).</summary>
		public string? ServerUrl { get; set; }

		/// <summary>WebSocket URL handed to browsers. Defaults to ServerUrl with wss://.</summary>
		public string? ClientUrl { get; set; }

		public string? ApiKey { get; set; }
		public string? ApiSecret { get; set; }

		public bool IsConfigured =>
			!string.IsNullOrWhiteSpace(ServerUrl)
			&& !string.IsNullOrWhiteSpace(ApiKey)
			&& !string.IsNullOrWhiteSpace(ApiSecret);

		public string ResolvedClientUrl =>
			!string.IsNullOrWhiteSpace(ClientUrl)
				? ClientUrl!.TrimEnd('/')
				: (ServerUrl ?? string.Empty).TrimEnd('/').Replace("https://", "wss://").Replace("http://", "ws://");
	}

	/// <summary>Rules for live classes. Hard limits (3 students, 40 minutes) are also enforced in code.</summary>
	public class LiveClassSettings
	{
		public const string SectionName = "LiveClass";

		/// <summary>Hard ceiling on students per class.</summary>
		public const int AbsoluteMaxStudents = 3;

		/// <summary>Hard ceiling on class length in minutes.</summary>
		public const int AbsoluteMaxMinutes = 40;

		/// <summary>Classes allowed to be live at the same time across the whole platform (protects the shared server).</summary>
		public int MaxConcurrent { get; set; } = 30;

		public int DurationMinutes { get; set; } = AbsoluteMaxMinutes;

		/// <summary>When true a school must have the "live_class" feature switched on (POST /api/school-features).</summary>
		public bool RequireSchoolFeature { get; set; } = true;
	}
}
