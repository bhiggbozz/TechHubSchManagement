using System.Text;

namespace TechHub.Service.Service.ImageGeneration;

/// <summary>
/// Builds the text prompt handed to the image-generation agent from the
/// lesson's aim + objectives and the school context. A teacher-supplied
/// prompt always wins over the auto-generated one.
/// </summary>
public static class TeachingPromptBuilder
{
	public static string BuildLessonImagePrompt(
		string? schoolName,
		string? subjectName,
		string? topicName,
		string? className,
		string aim,
		string objectives,
		string? materialWords,
		string? style)
	{
		var sb = new StringBuilder();

		sb.AppendLine("You are an instructional illustration generator for an African EdTech platform.");
		sb.AppendLine("Generate ONE high-quality, culturally appropriate, classroom-safe educational image for a lesson.");
		sb.AppendLine("The image must contain NO text, no words, no letters, no numbers, no watermarks and no logos.");
		sb.AppendLine("It will be projected to a secondary-school class, so keep the content clear, age-appropriate and easy to read at a distance.");
		sb.AppendLine();

		if (!string.IsNullOrWhiteSpace(schoolName))
			sb.AppendLine($"School: {schoolName.Trim()}");

		if (!string.IsNullOrWhiteSpace(subjectName))
			sb.AppendLine($"Subject: {subjectName.Trim()}");

		if (!string.IsNullOrWhiteSpace(topicName))
			sb.AppendLine($"Topic: {topicName.Trim()}");

		if (!string.IsNullOrWhiteSpace(className))
			sb.AppendLine($"Class: {className.Trim()}");

		sb.AppendLine();
		sb.AppendLine("LESSON AIM:");
		sb.AppendLine(aim.Trim());

		sb.AppendLine();
		sb.AppendLine("LESSON OBJECTIVES:");
		sb.AppendLine(objectives.Trim());

		if (!string.IsNullOrWhiteSpace(materialWords))
		{
			sb.AppendLine();
			sb.AppendLine("REQUIRED MATERIALS / VISUAL ELEMENTS (teacher's words):");
			sb.AppendLine(materialWords.Trim());
		}

		if (!string.IsNullOrWhiteSpace(style))
		{
			sb.AppendLine();
			sb.AppendLine($"STYLE: {style.Trim()}");
		}

		sb.AppendLine();
		sb.AppendLine("Illustrate the core concept of this lesson, not a generic scene.");

		return sb.ToString().Trim();
	}
}
