using Serilog;
using TechHub.Core.Entities;
using TechHub.Core.Entities.Performance;
using TechHub.Service.Interface;
using TechHub.Service.Service.DatabaseService;

namespace TechHub.Service.Service;

public class PerformanceIncrementalService : IPerformanceIncrementalService
{
    private readonly IPerformanceRepository _perfRepo;
    private readonly IQueryRepository<LessonContent> _lessonQuery;
    private readonly IQueryRepository<QuizAttempt> _attemptQuery;
    private readonly ILogger _logger;

    public PerformanceIncrementalService(
        IPerformanceRepository perfRepo,
        IQueryRepository<LessonContent> lessonQuery,
        IQueryRepository<QuizAttempt> attemptQuery,
        ILogger logger)
    {
        _perfRepo = perfRepo;
        _lessonQuery = lessonQuery;
        _attemptQuery = attemptQuery;
        _logger = logger;
    }

    public async Task OnAttemptCompletedAsync(
        Guid schoolId, Guid lessonId, Guid studentId, string? quizCode,
        decimal finalScorePercent, bool isPassed)
    {
        try
        {
            var lesson = await _lessonQuery.Get($@"
                SELECT TOP 1 Id, ClassroomId, SubjectId, TopicId, SubTopic, CreatedBy
                FROM LessonContent WITH(NOLOCK)
                WHERE Id = '{lessonId}' AND SchoolId = '{schoolId}'");

            if (lesson is null)
            {
                _logger.Warning("Lesson {LessonId} not found for incremental update", lessonId);
                return;
            }

            var now = DateTime.UtcNow;

            // Update classroom_subject snapshot
            await IncrementSnapshot("classroom_subject", schoolId,
                lesson.ClassroomId, lesson.SubjectId, null, null,
                null, null, finalScorePercent, isPassed, now);

            // Update topic snapshot if applicable
            if (lesson.TopicId != Guid.Empty)
            {
                var topicName = await GetTopicName(lesson.TopicId);
                await IncrementSnapshot("classroom_subject_topic", schoolId,
                    lesson.ClassroomId, lesson.SubjectId, lesson.TopicId, topicName,
                    null, null, finalScorePercent, isPassed, now);
            }

            // Update subtopic snapshot if applicable
            if (lesson.TopicId != Guid.Empty && !string.IsNullOrWhiteSpace(lesson.SubTopic))
            {
                var topicName = await GetTopicName(lesson.TopicId);
                await IncrementSnapshot("classroom_subject_subtopic", schoolId,
                    lesson.ClassroomId, lesson.SubjectId, lesson.TopicId, topicName,
                    lesson.SubTopic, null, finalScorePercent, isPassed, now);
            }

            // Update student snapshot
            await IncrementSnapshot("student", schoolId,
                null, null, null, null,
                null, studentId, finalScorePercent, isPassed, now);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed incremental performance update for attempt");
        }
    }

    public async Task OnAttemptScoreChangedAsync(
        Guid schoolId, Guid lessonId, Guid studentId, string? quizCode,
        decimal oldScore, decimal newScore, bool newIsPassed)
    {
        try
        {
            var lesson = await _lessonQuery.Get($@"
                SELECT TOP 1 Id, ClassroomId, SubjectId, TopicId, SubTopic, CreatedBy
                FROM LessonContent WITH(NOLOCK)
                WHERE Id = '{lessonId}' AND SchoolId = '{schoolId}'");

            if (lesson is null)
            {
                _logger.Warning("Lesson {LessonId} not found for incremental regrade", lessonId);
                return;
            }

            var now = DateTime.UtcNow;
            var delta = newScore - oldScore;

            // Update classroom_subject
            await AdjustSnapshot("classroom_subject", schoolId,
                lesson.ClassroomId, lesson.SubjectId, null, null,
                null, null, delta, newIsPassed, now);

            // Update topic
            if (lesson.TopicId != Guid.Empty)
            {
                var topicName = await GetTopicName(lesson.TopicId);
                await AdjustSnapshot("classroom_subject_topic", schoolId,
                    lesson.ClassroomId, lesson.SubjectId, lesson.TopicId, topicName,
                    null, null, delta, newIsPassed, now);
            }

            // Update subtopic
            if (lesson.TopicId != Guid.Empty && !string.IsNullOrWhiteSpace(lesson.SubTopic))
            {
                var topicName = await GetTopicName(lesson.TopicId);
                await AdjustSnapshot("classroom_subject_subtopic", schoolId,
                    lesson.ClassroomId, lesson.SubjectId, lesson.TopicId, topicName,
                    lesson.SubTopic, null, delta, newIsPassed, now);
            }

            // Update student
            await AdjustSnapshot("student", schoolId,
                null, null, null, null,
                null, studentId, delta, newIsPassed, now);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed incremental performance update for regrade");
        }
    }

    private async Task IncrementSnapshot(
        string docType, Guid schoolId, Guid? classroomId, Guid? subjectId,
        Guid? topicId, string? topicName, string? subTopicName, Guid? studentId,
        decimal score, bool isPassed, DateTime now)
    {
        var existing = await _perfRepo.FindSnapshotAsync(
            docType, schoolId, classroomId, subjectId,
            topicId, subTopicName, null, studentId);

        if (existing is not null)
        {
            var newCount = existing.CompletedAttempts + 1;
            existing.TotalScoreSum += score;
            existing.AverageScorePercent = newCount > 0
                ? Math.Round(existing.TotalScoreSum / newCount, 1)
                : 0m;
            existing.CompletedAttempts = newCount;
            existing.TotalAttempts += 1;
            if (isPassed) existing.PassRate = CalculatePassRate(existing.CompletedAttempts, existing.PassRate, true);
            existing.ComputedAt = now;
            await _perfRepo.UpsertSnapshotAsync(existing);
        }
        else
        {
            await _perfRepo.UpsertSnapshotAsync(new PerformanceSnapshot
            {
                DocType = docType,
                SchoolId = schoolId,
                ClassroomId = classroomId,
                SubjectId = subjectId,
                TopicId = topicId,
                TopicName = topicName,
                SubTopicName = subTopicName,
                StudentId = studentId,
                TotalAttempts = 1,
                CompletedAttempts = 1,
                AverageScorePercent = Math.Round(score, 1),
                TotalScoreSum = score,
                PassRate = isPassed ? 100m : 0m,
                StudentCount = 1,
                TotalMarksSum = 0,
                ObtainedMarksSum = 0,
                ComputedAt = now
            });
        }
    }

    private async Task AdjustSnapshot(
        string docType, Guid schoolId, Guid? classroomId, Guid? subjectId,
        Guid? topicId, string? topicName, string? subTopicName, Guid? studentId,
        decimal scoreDelta, bool newIsPassed, DateTime now)
    {
        var existing = await _perfRepo.FindSnapshotAsync(
            docType, schoolId, classroomId, subjectId,
            topicId, subTopicName, null, studentId);

        if (existing is null) return;

        existing.TotalScoreSum += scoreDelta;
        existing.AverageScorePercent = existing.CompletedAttempts > 0
            ? Math.Round(existing.TotalScoreSum / existing.CompletedAttempts, 1)
            : 0m;
        existing.ComputedAt = now;
        await _perfRepo.UpsertSnapshotAsync(existing);
    }

    private static decimal CalculatePassRate(int completedCount, decimal currentPassRate, bool newAttemptPassed)
    {
        var currentPassed = (int)Math.Round(currentPassRate / 100m * completedCount);
        var newPassed = currentPassed + (newAttemptPassed ? 1 : 0);
        var newCount = completedCount + 1;
        return newCount > 0 ? Math.Round((decimal)newPassed / newCount * 100, 1) : 0m;
    }

    private async Task<string?> GetTopicName(Guid topicId)
    {
        try
        {
            var names = await _attemptQuery.QueryAsync<string>($@"
                SELECT TOP 1 Name FROM Topic WITH(NOLOCK)
                WHERE Id = '{topicId}'",
                new Dictionary<string, object>());
            return names.FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }
}
