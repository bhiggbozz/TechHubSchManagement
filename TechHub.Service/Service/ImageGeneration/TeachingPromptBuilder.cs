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
		string? subTopicName,
		string? className,
		string aim,
		string objectives,
		string? materialWords,
		string? style)
	{
		var sb = new StringBuilder();

		sb.AppendLine("You are an instructional illustration generator for an African EdTech platform.");
		sb.AppendLine("PEDAGOGICAL GOAL: build a correct mental model in the students' minds. The image must help them understand HOW the concept works at a basic, foundational level - not merely show what it looks like.");
		sb.AppendLine("Generate educational images that are culturally appropriate, classroom-safe and age-appropriate.");
		sb.AppendLine("The images must contain NO text, no words, no letters, no numbers, no watermarks and no logos.");
		sb.AppendLine("They will be projected to a secondary-school class, so keep the content clear, simple and easy to read at a distance.");
		sb.AppendLine("BASIC UNDERSTANDING: ground the image in the basics - what the concept is, its key parts, how the parts connect and interact, cause and effect, and one simple everyday example. Prefer simple, correct mechanics over visual flourish.");
		sb.AppendLine();

		if (!string.IsNullOrWhiteSpace(schoolName))
			sb.AppendLine($"School: {schoolName.Trim()}");

		if (!string.IsNullOrWhiteSpace(subjectName))
			sb.AppendLine($"Subject: {subjectName.Trim()}");

		if (!string.IsNullOrWhiteSpace(topicName))
			sb.AppendLine($"Topic: {topicName.Trim()}");

		if (!string.IsNullOrWhiteSpace(subTopicName))
			sb.AppendLine($"Subtopic: {subTopicName.Trim()}");

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
		sb.AppendLine("Illustrate the core concept of this lesson and make its underlying mechanism visible and easy for a learner to reason about.");

		return sb.ToString().Trim();
	}
}
