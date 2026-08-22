using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Serilog;
using TechHub.Core.Configuration;
using TechHub.Core.Interface;
using TechHub.Core.Model;

namespace TechHub.Service.Service.ImageGeneration;

/// <summary>
/// OpenAI GPT Image agent (the successor to DALL·E, which was removed from the
/// OpenAI API on 2026-05-12). Works with gpt-image-1 / gpt-image-1.5 /
/// gpt-image-2 via POST /v1/images/generations (always returns base64).
///
/// SWAPPABLE: registered under Name = "OpenAI" and resolved by the factory
/// from ImageGeneration:Provider. The model id itself is read from
/// ImageGeneration:Providers:OpenAI:Model — switching models or providers is a
/// configuration change, not a code change.
/// </summary>
public class OpenAiImageGenerationAgent : IImageGenerationAgent
{
	private const string DefaultBaseUrl = "https://api.openai.com";
	private const string DefaultEndpoint = "/v1/images/generations";
	private const string DefaultModel = "gpt-image-1";

	private readonly HttpClient _httpClient;
	private readonly ImageGenerationProviderOptions _options;
	private readonly ILogger _logger;

	public string Name => "OpenAI";

	public OpenAiImageGenerationAgent(
		IHttpClientFactory httpClientFactory,
		IOptions<ImageGenerationSettings> settings,
		ILogger logger)
	{
		_logger = logger;
		_options = settings.Value.Providers.GetValueOrDefault(Name) ?? new ImageGenerationProviderOptions();
		_httpClient = httpClientFactory.CreateClient("ImageGeneration");
		_httpClient.Timeout = TimeSpan.FromSeconds(Math.Max(10, settings.Value.TimeoutSeconds));
	}

	public async Task<ImageGenerationResult> GenerateAsync(
		ImageGenerationRequest request,
		CancellationToken cancellationToken = default)
	{
		try
		{
			if (string.IsNullOrWhiteSpace(_options.ApiKey))
				return new ImageGenerationResult
				{
					Success = false,
					ErrorMessage = "OpenAI provider is not configured (missing API key)"
				};

			var baseUrl = (_options.BaseUrl ?? DefaultBaseUrl).TrimEnd('/');
			var endpoint = string.IsNullOrWhiteSpace(_options.Endpoint) ? DefaultEndpoint : _options.Endpoint;
			var model = string.IsNullOrWhiteSpace(_options.Model) ? DefaultModel : _options.Model;

			var payload = new Dictionary<string, object?>
			{
				["model"] = model,
				["prompt"] = request.Prompt,
				["n"] = 1,
				["size"] = ResolveSize(request)
			};

			if (!string.IsNullOrWhiteSpace(_options.Quality))
				payload["quality"] = _options.Quality;

			for (var attempt = 1; attempt <= ImageGenerationRetryPolicy.MaxAttempts; attempt++)
			{
				using var httpRequest = new HttpRequestMessage(HttpMethod.Post, baseUrl + endpoint);
				httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
				httpRequest.Content = new StringContent(
					JsonSerializer.Serialize(payload),
					Encoding.UTF8,
					"application/json");

				_logger.Information("Calling OpenAI agent - Model: {Model}, Size: {Size}, Attempt: {Attempt}", model, ResolveSize(request), attempt);

				using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
				if (!response.IsSuccessStatusCode)
				{
					var body = await response.Content.ReadAsStringAsync(cancellationToken);
					_logger.Error("OpenAI generation failed - Status: {Status}, Attempt: {Attempt}, Body: {Body}",
						(int)response.StatusCode, attempt, Truncate(body, 1000));

					if (ImageGenerationRetryPolicy.IsTransient(response.StatusCode) && attempt < ImageGenerationRetryPolicy.MaxAttempts)
					{
						await Task.Delay(ImageGenerationRetryPolicy.GetDelay(attempt), cancellationToken);
						continue;
					}

					return new ImageGenerationResult
					{
						Success = false,
						ErrorMessage = $"Image provider error ({(int)response.StatusCode})"
					};
				}

				var json = await response.Content.ReadAsStringAsync(cancellationToken);
				using var doc = JsonDocument.Parse(json);
				var data = doc.RootElement.GetProperty("data");
				if (data.GetArrayLength() == 0 ||
					!data[0].TryGetProperty("b64_json", out var b64) ||
					string.IsNullOrWhiteSpace(b64.GetString()))
				{
					return new ImageGenerationResult { Success = false, ErrorMessage = "Image provider returned no image data" };
				}

				var bytes = Convert.FromBase64String(b64.GetString()!);

				_logger.Information("OpenAI generation succeeded - Bytes: {Bytes}, Attempt: {Attempt}", bytes.Length, attempt);

				return new ImageGenerationResult
				{
					Success = true,
					ImageBytes = bytes,
					ContentType = "image/png",
					ModelUsed = model
				};
			}

			return new ImageGenerationResult { Success = false, ErrorMessage = "Image generation failed after retries" };
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			return new ImageGenerationResult { Success = false, ErrorMessage = "Image generation timed out" };
		}
		catch (Exception ex)
		{
			_logger.Error(ex, "OpenAI agent exception");
			return new ImageGenerationResult { Success = false, ErrorMessage = "Image generation failed" };
		}
	}

	/// <summary>
	/// GPT Image models accept 1024x1024, 1024x1536 (portrait), 1536x1024
	/// (landscape) and "auto". Map requested dims to the closest supported size.
	/// </summary>
	private string ResolveSize(ImageGenerationRequest request)
	{
		if (!string.IsNullOrWhiteSpace(_options.Size))
			return _options.Size!;

		var width = request.Width ?? 1024;
		var height = request.Height ?? 1024;

		if (width > height) return "1536x1024";
		if (height > width) return "1024x1536";
		return "1024x1024";
	}

	private static string Truncate(string value, int max)
		=> string.IsNullOrEmpty(value) ? string.Empty
			: value.Length <= max ? value : value[..max];
}
