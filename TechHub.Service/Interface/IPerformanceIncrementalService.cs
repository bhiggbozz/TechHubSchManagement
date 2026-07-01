namespace TechHub.Service.Interface;

public interface IPerformanceIncrementalService
{
    Task OnAttemptCompletedAsync(
        Guid schoolId, Guid lessonId, Guid studentId, string? quizCode,
        decimal finalScorePercent, bool isPassed);

    Task OnAttemptScoreChangedAsync(
        Guid schoolId, Guid lessonId, Guid studentId, string? quizCode,
        decimal oldScore, decimal newScore, bool newIsPassed);
}
