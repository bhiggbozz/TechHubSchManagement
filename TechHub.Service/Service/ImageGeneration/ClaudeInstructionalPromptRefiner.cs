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
		int imageCount = 1,
		CancellationToken cancellationToken = default)
	{
		var target = Math.Clamp(imageCount, 1, 10);

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
				"Core mission: the refined prompt(s) you write must BUILD a correct mental model of the lesson's concept by " +
				"anchoring it in something the student has personally lived through - not by illustrating the concept in its " +
				"formal, textbook form. A student looking at the image should think 'I've seen/felt that before' and use that " +
				"felt experience to grasp the abstract mechanism. Never merely decorate; teach the working through a lived moment.\n" +
				"\n" +
				"MANDATORY FIRST STEP (do this silently before writing any prompt): ask yourself 'What everyday scene, activity, " +
				"food, game or object in an African secondary-school student's daily life involves this EXACT SAME cause-and-effect " +
				"mechanism?' Examples of the kind of thinking required: heat being released outward = the warmth felt standing near " +
				"a wood fire or the heat off a just-parked car engine; heat being absorbed/pulled in = the cold felt licking an ice " +
				"lolly, sweat cooling the skin as it evaporates, or the chill of a clay pot of water left to cool; energy stored then " +
				"released = a stretched catapult/slingshot snapping forward. Commit fully to that lived scene - it becomes the ENTIRE " +
				"image, not a small addition to a lab diagram.\n" +
				"\n" +
				"BANNED unless the concept truly has no everyday equivalent (rare - check hard before concluding this): generic " +
				"classroom/laboratory equipment as the main subject (beakers, test tubes, flasks, thermometers next to containers), " +
				"side-by-side 'before/after' or 'left/right' clinical split-screen layouts, labeled arrows-and-boxes schematics, or " +
				"any composition that looks like it was lifted from a textbook diagram. If a lab tool must appear at all, it should " +
				"be a small, secondary detail inside the lived scene, never the whole picture.\n" +
				"\n" +
				$"Your job: rewrite the provided draft image-generation prompt into {target} optimal, highly detailed, " +
				"pedagogically effective prompt(s) for generating teaching image(s) for the classroom, each built around a " +
				"real, relatable scene as described above.\n" +
				"\n" +
				"Context you must consider:\n" +
				"- The draft contains the school, subject, topic, subtopic, class, lesson AIM and OBJECTIVES.\n" +
				"- The 'REQUIRED MATERIALS / VISUAL ELEMENTS (teacher's words)' section lists materials the teacher explicitly requested.\n" +
				"- Keep the content age-appropriate for the target class/year group.\n" +
				"\n" +
				"When more than one image is requested, split the lesson into that many distinct, complementary lived scenes that " +
				"together scaffold understanding - ordered from foundational to applied - and cover each aspect in ONLY ONE " +
				"image (no repetition):\n" +
				"  1) The everyday moment itself - the relatable scene the student recognizes, showing the concept's effect they can feel or see.\n" +
				"  2) How it works inside that same scene - the mechanism made visible (motion, glow, particles, flow) without switching to lab equipment.\n" +
				"  3) Relationships and nuances - a second, contrasting everyday moment, or a closer look at cause and effect within the scene.\n" +
				"  4) A different everyday application - another familiar situation the students already know.\n" +
				"For a single image, do NOT compress all four into one busy or clinical composition - pick the single strongest " +
				"everyday moment from step 1 and make the mechanism (step 2) visible within it. One vivid, lived scene beats a " +
				"crowded diagram.\n" +
				"\n" +
				"Validation rule (very important):\n" +
				"- If the teacher's requested materials or visual elements do NOT align with the lesson's subject, aim or objectives, " +
				"you must DECLINE instead of writing prompt(s).\n" +
				"- In the decline, explain clearly why it is not suitable, referencing the conflict with the subject/aim/objectives " +
				"and suggesting what would be more appropriate.\n" +
				"- If the materials are absent or align with the lesson, do NOT decline - just refine.\n" +
				"\n" +
				"Prompt quality (when not declining):\n" +
				"- Keep every constraint from the draft: culturally appropriate and classroom-safe, no watermarks and no logos.\n" +
				"- Labels: you may name up to 1-3 short, correctly-spelled key terms as labels directly on the image (e.g. the " +
				"two contrasting terms being taught), only when they add real clarity. Spell out each label's exact text in " +
				"quotes in the prompt so the generator renders it verbatim. Do not add any other text, captions, numbers or " +
				"random words - unlabeled or over-labeled images are both worse than a clean one.\n" +
				"- Keep the science and mechanics simple and correct for a secondary-school class.\n" +
				"- Make the scene concrete, vivid, specific and emotionally familiar - not clinical - so students immediately " +
				"recognize it from their own lives and transfer that recognition to the lesson's concept. Incorporate the " +
				"teacher's requested materials/visual elements explicitly when they align with the lesson.\n" +
				"\n" +
				"Respond in this strict JSON format only, with no markdown, no preamble and no extra text:\n" +
				"{\"declined\": true/false, \"prompts\": [\"prompt 1\", \"prompt 2\", ...], \"reason\": \"empty when accepted, or the decline explanation when declined\"}";

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

				var prompts = new List<string>();
				if (parsed.Prompts is { Count: > 0 })
				{
					foreach (var p in parsed.Prompts)
					{
						if (!string.IsNullOrWhiteSpace(p))
							prompts.Add(ClampLength(p.Trim()));
					}
				}
				else if (!string.IsNullOrWhiteSpace(parsed.Prompt))
				{
					prompts.Add(ClampLength(parsed.Prompt.Trim()));
				}

				if (prompts.Count > 0)
				{
					_logger.Information(
						"Lesson image prompt(s) refined by Claude - Model: {Model}, PromptCount: {Count}, FirstLength: {Length}",
						_settings.Model, prompts.Count, prompts[0].Length);

					return new PromptRefinementResult
					{
						Success = true,
						Prompt = prompts[0],
						Prompts = prompts,
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
				Prompts = new List<string> { fallback },
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
		public List<string>? Prompts { get; set; }
		public string? Reason { get; set; }
	}
}
