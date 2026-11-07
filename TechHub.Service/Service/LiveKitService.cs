using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using TechHub.Core.Configuration;
using TechHub.Service.Interface;

namespace TechHub.Service.Service
{
	/// <summary>
	/// LiveKit access tokens are plain HS256 JWTs (iss = API key, sub = identity, a "video" grant claim),
	/// and its room API is Twirp over HTTP with JSON bodies, so no SDK package is needed.
	/// </summary>
	public class LiveKitService : ILiveKitService
	{
		private const string RoomServicePath = "/twirp/livekit.RoomService/";

		private readonly LiveKitSettings _settings;
		private readonly HttpClient _http;
		private readonly ILogger _logger;

		public LiveKitService(IOptions<LiveKitSettings> settings, HttpClient http, ILogger logger)
		{
			_settings = settings.Value;
			_http = http;
			_http.Timeout = TimeSpan.FromSeconds(10);
			_logger = logger;
		}

		public bool IsConfigured => _settings.IsConfigured;

		public string ClientUrl => _settings.ResolvedClientUrl;

		public string CreateJoinToken(string roomName, string identity, string displayName, bool canSpeak, TimeSpan ttl, bool canPublishData = false)
		{
			var grant = new Dictionary<string, object>
			{
				["room"] = roomName,
				["roomJoin"] = true,
				["canSubscribe"] = true,
				["canPublish"] = canSpeak,
				// Only the teacher may publish data (the live board strokes); students never can.
				["canPublishData"] = canPublishData,
				// Microphone only: no camera and no screen share, ever.
				["canPublishSources"] = new[] { "microphone" }
			};
			return Sign(identity, displayName, grant, ttl);
		}

		public async Task<bool> CreateRoomAsync(string roomName, int maxParticipants, int emptyTimeoutSeconds)
			=> await CallAsync("CreateRoom", new { name = roomName, empty_timeout = emptyTimeoutSeconds, max_participants = maxParticipants },
				new Dictionary<string, object> { ["roomCreate"] = true });

		public async Task DeleteRoomAsync(string roomName)
			=> await CallAsync("DeleteRoom", new { room = roomName },
				new Dictionary<string, object> { ["roomCreate"] = true });

		public async Task<bool> SetCanSpeakAsync(string roomName, string identity, bool canSpeak)
			=> await CallAsync("UpdateParticipant",
				new
				{
					room = roomName,
					identity,
					permission = new
					{
						can_subscribe = true,
						can_publish = canSpeak,
						can_publish_data = false,
						can_publish_sources = new[] { "MICROPHONE" }
					}
				},
				new Dictionary<string, object> { ["roomAdmin"] = true, ["room"] = roomName });

		private async Task<bool> CallAsync(string method, object body, Dictionary<string, object> adminGrant)
		{
			if (!IsConfigured)
				return false;

			try
			{
				using var request = new HttpRequestMessage(HttpMethod.Post, _settings.ServerUrl!.TrimEnd('/') + RoomServicePath + method)
				{
					Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
				};
				request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
					"Bearer", Sign("api-server", "api-server", adminGrant, TimeSpan.FromMinutes(1)));

				using var response = await _http.SendAsync(request);
				if (response.IsSuccessStatusCode)
					return true;

				var text = await response.Content.ReadAsStringAsync();
				_logger.Warning("LiveKit {Method} returned {Status}: {Body}", method, (int)response.StatusCode,
					text.Length > 300 ? text[..300] : text);
				return false;
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "LiveKit {Method} call failed", method);
				return false;
			}
		}

		private string Sign(string identity, string name, Dictionary<string, object> videoGrant, TimeSpan ttl)
		{
			var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.ApiSecret!));
			var now = DateTime.UtcNow;
			var claims = new List<Claim>
			{
				new Claim(JwtRegisteredClaimNames.Sub, identity),
				new Claim("name", name),
				new Claim("video", JsonSerializer.Serialize(videoGrant), JsonClaimValueTypes.Json)
			};
			var token = new JwtSecurityToken(
				issuer: _settings.ApiKey,
				audience: null,
				claims: claims,
				notBefore: now.AddSeconds(-5),
				expires: now.Add(ttl),
				signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
			return new JwtSecurityTokenHandler().WriteToken(token);
		}
	}
}
