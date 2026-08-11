using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Serilog;
using TechHub.Core.Configuration;
using TechHub.Core.Interface;
using TechHub.Core.Model;

namespace TechHub.Service.Service.ImageGeneration;

/// <summary>
/// Stability AI text-to-image agent (Stable Image Core).
///
/// SWAPPABLE: registered under Name = "Stability" and resolved by the factory
/// from ImageGeneration:Provider. Adding another provider only requires a new
/// <see cref="IImageGenerationAgent"/> implementation + config block.
/// </summary>
public class StabilityImageGenerationAgent : IImageGenerationAgent
{
	private const int MaxDimension = 1536;
	private const string DefaultBaseUrl = "https://api.stability.ai";
	private const string DefaultEndpoint = "/v2beta/stable-image/generate/core";

	private readonly HttpClient _httpClient;
	private readonly ImageGenerationProviderOptions _options;
	private readonly ILogger _logger;

	public string Name => "Stability";

	public StabilityImageGenerationAgent(
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
					ErrorMessage = "Stability provider is not configured (missing API key)"
				};

			var baseUrl = (_options.BaseUrl ?? DefaultBaseUrl).TrimEnd('/');
			var endpoint = string.IsNullOrWhiteSpace(_options.Endpoint) ? DefaultEndpoint : _options.Endpoint;
			var url = baseUrl + endpoint;

			using var form = new MultipartFormDataContent();
			form.Add(new StringContent(request.Prompt), "prompt");

			if (!string.IsNullOrWhiteSpace(request.NegativePrompt))
				form.Add(new StringContent(request.NegativePrompt), "negative_prompt");

			var width = Math.Clamp(request.Width ?? 1024, 256, MaxDimension);
			var height = Math.Clamp(request.Height ?? 1024, 256, MaxDimension);
			form.Add(new StringContent(width.ToString()), "width");
			form.Add(new StringContent(height.ToString()), "height");
			form.Add(new StringContent("png"), "output_format");

			using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
			httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
			httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/*"));
			httpRequest.Content = form;

			_logger.Information("Calling Stability agent - Width: {Width}, Height: {Height}", width, height);

			using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
			if (!response.IsSuccessStatusCode)
			{
				var body = await response.Content.ReadAsStringAsync(cancellationToken);
				_logger.Error("Stability generation failed - Status: {Status}, Body: {Body}",
					(int)response.StatusCode, Truncate(body, 1000));
				return new ImageGenerationResult
				{
					Success = false,
					ErrorMessage = $"Image provider error ({(int)response.StatusCode})"
				};
			}

			var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
			if (bytes == null || bytes.Length == 0)
				return new ImageGenerationResult { Success = false, ErrorMessage = "Image provider returned an empty response" };

			var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/png";

			_logger.Information("Stability generation succeeded - Bytes: {Bytes}, Type: {Type}", bytes.Length, contentType);

			return new ImageGenerationResult
			{
				Success = true,
				ImageBytes = bytes,
				ContentType = contentType,
				ModelUsed = _options.Model
			};
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			return new ImageGenerationResult { Success = false, ErrorMessage = "Image generation timed out" };
		}
		catch (Exception ex)
		{
			_logger.Error(ex, "Stability agent exception");
			return new ImageGenerationResult { Success = false, ErrorMessage = "Image generation failed" };
		}
	}

	private static string Truncate(string value, int max)
		=> string.IsNullOrEmpty(value) ? string.Empty
			: value.Length <= max ? value : value[..max];
}
