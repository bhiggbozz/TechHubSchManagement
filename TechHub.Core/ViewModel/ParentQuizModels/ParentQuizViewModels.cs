using System;
using System.Collections.Generic;

namespace TechHub.Core.ViewModel.ParentQuizModels
{
	/// <summary>A parent's saved defaults. Saving replaces all of them.</summary>
	public class ParentQuizSettingsViewModel
	{
		/// <summary>5 to 30.</summary>
		public int QuestionCount { get; set; } = 10;
		/// <summary>Mix | Easy | Medium | Hard | ExamLevel</summary>
		public string Mode { get; set; } = "Mix";
		/// <summary>Null or 0 = no time limit.</summary>
		public int? TimeLimitMinutes { get; set; }
		/// <summary>1 to 100.</summary>
		public int PassMarkPercent { get; set; } = 50;
		public bool ShuffleQuestions { get; set; } = true;
	}

	/// <summary>
	/// Only the child and subject are needed; everything else falls back to the parent's saved
	/// settings (or the built-in defaults).
	/// </summary>
	public class CreateParentQuizViewModel
	{
		public Guid StudentId { get; set; }
		public Guid SubjectId { get; set; }
		/// <summary>Empty or null = the whole subject.</summary>
		public List<Guid>? TopicIds { get; set; }
		/// <summary>Pick specific sub-topics from the syllabus (independent of lessons). Combined with TopicIds if both are sent.</summary>
		public List<Guid>? SubTopicIds { get; set; }
		public string? Title { get; set; }
		public int? QuestionCount { get; set; }
		public string? Mode { get; set; }
		/// <summary>Null = use the saved default; 0 = no time limit for this quiz.</summary>
		public int? TimeLimitMinutes { get; set; }
		public int? PassMarkPercent { get; set; }
	}

	public class ParentQuizAnswerInput
	{
		public Guid QuestionId { get; set; }
		/// <summary>Null = skipped.</summary>
		public Guid? SelectedOptionId { get; set; }
	}

	public class SubmitParentQuizViewModel
	{
		public List<ParentQuizAnswerInput> Answers { get; set; } = new();
	}
}
