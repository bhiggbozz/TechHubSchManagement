namespace TechHub.Core.Constant;

/// <summary>
/// Shared timing rule for when a still-"InProgress" QuizAttempt/AssessmentAttempt
/// should stop being treated as active. Used by both UserService.Login()'s
/// login-block check (blocks a second login while an attempt is genuinely within
/// its window) and StaleAttemptCleanupJob's daily sweep (marks it Abandoned once
/// it isn't) — kept as one shared definition so the two can't drift apart.
/// </summary>
public static class AttemptExpiryPolicy
{
	/// <summary>Minutes added on top of the quiz/assessment's own configured time
	/// limit before an InProgress attempt is treated as abandoned.</summary>
	public const int GraceMinutes = 5;

	/// <summary>Fallback window (minutes) for a quiz with no resolvable time limit
	/// (untimed, or the Quiz/AssessmentSet/teacher-default chain can't be resolved
	/// from a raw SQL check). Generous enough to cover a real supervised session.</summary>
	public const int UntimedQuizFallbackMinutes = 120;
}
