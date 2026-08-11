using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Microsoft.Extensions.Options;
using Serilog;
using TechHub.Core.Configuration;
using TechHub.Core.Interface;
using TechHub.Core.Model;

namespace TechHub.Service.Service.ImageGeneration;

/// <summary>
/// Redefines a draft teaching-image prompt into a stronger one using Claude.
///
/// The draft prompt already carries the lesson aim + objectives + the teacher's
/// requested material words plus the platform's image constraints. Claude
/// rewrites it into a single, vivid, pedagogically effective prompt that is
/// then saved as the lesson's "last prompt" and handed to the image agent.
///
/// Any failure (no API key, disabled, timeout, empty response) returns a failed
/// result so the caller falls back to the unrefined draft — image generation
/// never breaks because of the refinement step.
/// </summary>
public class ClaudeInstructionalPromptRefiner : IInstructionalPromptRefiner
{
	private readonly AnthropicSettings _settings;
	private readonly ILogger _logger;

	public ClaudeInstructionalPromptRefiner(
		IOptions<AnthropicSettings> settings,
		ILogger logger)
	{
		_settings = settings.Value;
		_logger = logger;
	}

	public async Task<PromptRefinementResult> RefineLessonImagePromptAsync(
		string draftPrompt,
		string? className,
		CancellationToken cancellationToken = default)
	{
		if (!_settings.EnablePromptRefinement)
		{
			_logger.Information("Claude prompt refinement is disabled - using draft prompt");
			return new PromptRefinementResult { Success = false, ErrorMessage = "Prompt refinement disabled" };
		}

		if (string.IsNullOrWhiteSpace(_settings.ApiKey))
		{
			_logger.Warning("Anthropic ApiKey not configured - using draft prompt");
			return new PromptRefinementResult { Success = false, ErrorMessage = "Anthropic ApiKey not configured" };
		}

		try
		{
			var client = new AnthropicClient(_settings.ApiKey);

			var systemPrompt =
				"You are an expert instructional-content designer and prompt engineer for an African EdTech platform.\n" +
				$"Target class (year group / grade): {className ?? "Not specified"}\n" +
				"\n" +
				"Your job: rewrite the provided draft image-generation prompt into ONE optimal, highly detailed, " +
				"pedagogically effective prompt for generating a teaching image for a secondary-school classroom.\n" +
				"\n" +
				"Context you must consider:\n" +
				"- The draft contains the school, subject, topic, class, lesson AIM and OBJECTIVES.\n" +
				"- The 'REQUIRED MATERIALS / VISUAL ELEMENTS (teacher's words)' section lists materials the teacher explicitly requested.\n" +
				"- Keep the content age-appropriate for the target class/year group.\n" +
				"\n" +
				"Validation rule (very important):\n" +
				"- If the teacher's requested materials or visual elements do NOT align with the lesson's subject, aim or objectives, " +
				"you must DECLINE instead of writing a prompt.\n" +
				"- In the decline, explain clearly why it is not suitable, referencing the conflict with the subject/aim/objectives " +
				"and suggesting what would be more appropriate.\n" +
				"- If the materials are absent or align with the lesson, do NOT decline - just refine the prompt.\n" +
				"\n" +
				"Prompt quality (when not declining):\n" +
				"- Keep every constraint from the draft: culturally appropriate, classroom-safe, and the image must contain " +
				"NO text, no words, no letters, no numbers, no watermarks and no logos.\n" +
				"- Make the scene concrete, vivid and specific enough that the image genuinely helps students grasp and " +
				"remember the lesson's core concept. Incorporate the teacher's requested materials/visual elements explicitly " +
				"when they align with the lesson.\n" +
				"\n" +
				"Respond in this strict JSON format only, with no markdown, no preamble and no extra text:\n" +
				"{\"declined\": true/false, \"prompt\": \"the refined prompt, or empty string when declined\", \"reason\": \"empty when accepted, or the decline explanation when declined\"}";

			var messages = new List<Message>
			{
				new Message
				{
					Role = RoleType.User,
					Content = new List<ContentBase>
					{
						new TextContent { Text = draftPrompt }
					}
				}
			};

			var parameters = new MessageParameters
			{
				Model = _settings.Model,
				MaxTokens = _settings.MaxTokens,
				Messages = messages,
				System = new List<SystemMessage> { new SystemMessage(systemPrompt) }
			};

			var response = await client.Messages.GetClaudeMessageAsync(parameters, cancellationToken);

			if (response.StopReason == "max_tokens")
			{
				_logger.Warning("Claude prompt refinement hit max_tokens - using draft prompt");
				return new PromptRefinementResult { Success = false, ErrorMessage = "Claude response truncated" };
			}

			var text = response.Content
				?.OfType<TextContent>()
				.FirstOrDefault()
				?.Text
				?.Trim();

			if (string.IsNullOrWhiteSpace(text))
			{
				_logger.Warning("Claude prompt refinement returned empty text - using draft prompt");
				return new PromptRefinementResult { Success = false, ErrorMessage = "Claude returned empty prompt" };
			}

			// Structured JSON response: { declined, prompt, reason }.
			var parsed = TryParseRefinement(text);
			if (parsed is not null)
			{
				if (parsed.Declined)
				{
					_logger.Warning(
						"Claude declined image prompt - Model: {Model}, Reason: {Reason}",
						_settings.Model, parsed.Reason);

					return new PromptRefinementResult
					{
						Success = false,
						Declined = true,
						DeclineReason = parsed.Reason,
						ModelUsed = _settings.Model
					};
				}

				if (!string.IsNullOrWhiteSpace(parsed.Prompt))
				{
					var refined = ClampLength(parsed.Prompt.Trim());

					_logger.Information(
						"Lesson image prompt refined by Claude - Model: {Model}, RefinedLength: {Length}",
						_settings.Model, refined.Length);

					return new PromptRefinementResult
					{
						Success = true,
						Prompt = refined,
						ModelUsed = _settings.Model
					};
				}
			}

			// Not parseable as the expected shape — fall back to the raw text so
			// generation still works (the draft prompt is used as a last resort).
			_logger.Warning("Claude prompt refinement response was not structured - using raw text as prompt");
			var fallback = ClampLength(text);
			return new PromptRefinementResult
			{
				Success = true,
				Prompt = fallback,
				ModelUsed = _settings.Model
			};
		}
		catch (OperationCanceledException)
		{
			_logger.Warning("Claude prompt refinement cancelled - using draft prompt");
			return new PromptRefinementResult { Success = false, ErrorMessage = "Refinement cancelled" };
		}
		catch (Exception ex)
		{
			_logger.Error(ex, "Claude prompt refinement failed - using draft prompt");
			return new PromptRefinementResult { Success = false, ErrorMessage = ex.Message };
		}
	}

	private string ClampLength(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return string.Empty;

		var trimmed = value.Trim();
		return trimmed.Length > _settings.MaxPromptLength
			? trimmed[.._settings.MaxPromptLength]
			: trimmed;
	}

	/// <summary>
	/// Extracts the structured { declined, prompt, reason } response Claude is
	/// asked to return. Tolerates a surrounding markdown fence / preamble by
	/// locating the outermost JSON object.
	/// </summary>
	private static RefinementResponseDto? TryParseRefinement(string text)
	{
		try
		{
			var start = text.IndexOf('{');
			var end = text.LastIndexOf('}');
			if (start < 0 || end <= start)
				return null;

			var json = text[start..(end + 1)];
			return System.Text.Json.JsonSerializer.Deserialize<RefinementResponseDto>(
				json,
				new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
		}
		catch
		{
			return null;
		}
	}

	/// <summary>Structured response shape Claude is instructed to return.</summary>
	private sealed class RefinementResponseDto
	{
		public bool Declined { get; set; }
		public string? Prompt { get; set; }
		public string? Reason { get; set; }
	}
}
