using System;
using System.Collections.Generic;

namespace TechHub.Core.ViewModel.classroom;

public class DifficultySelectionViewModel
{
	public int DifficultyLevel { get; set; }
	public int Count { get; set; }
}

public class ParentQuickAssessmentViewModel
{
	public List<Guid> TopicIds { get; set; } = new();
	public List<DifficultySelectionViewModel> DifficultySelections { get; set; } = new();
	public string? Title { get; set; }
	public int? TimeLimitMinutes { get; set; }
	public int? PassMarkPercent { get; set; }
	public bool? ShowResultImmediately { get; set; }
	public bool? ShowCorrectAnswers { get; set; }
	public DateTime? ExpiresAt { get; set; }
}

public class QuestionAvailabilityViewModel
{
	public List<Guid> TopicIds { get; set; } = new();
}
