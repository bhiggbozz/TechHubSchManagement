using Serilog;
using TechHub.Core.Entities;
using TechHub.Core.Entities.Performance;
using TechHub.Service.Interface;
using TechHub.Service.Service.DatabaseService;

namespace TechHub.Service.Service;

public class PerformanceAggregationService : IPerformanceAggregationService
{
    private readonly IPerformanceRepository _perfRepo;
    private readonly IQueryRepository<QuizAttempt> _attemptQuery;
    private readonly ICommandRespository<PerformanceAggregationLog> _logCommand;
    private readonly ILogger _logger;

    public PerformanceAggregationService(
        IPerformanceRepository perfRepo,
        IQueryRepository<QuizAttempt> attemptQuery,
        ICommandRespository<PerformanceAggregationLog> logCommand,
        ILogger logger)
    {
        _perfRepo = perfRepo;
        _attemptQuery = attemptQuery;
        _logCommand = logCommand;
        _logger = logger;
    }

    public async Task AggregateAllSchoolsAsync()
    {
        _logger.Information("Starting performance aggregation for all schools");

        var schools = await _attemptQuery.QueryAsync<Guid>($@"
            SELECT DISTINCT SchoolId
            FROM QuizAttempt WITH(NOLOCK)
            WHERE Status IN ('Submitted','PartiallyGraded','FullyGraded')",
            new Dictionary<string, object>());

        foreach (var schoolId in schools)
        {
            try
            {
                await AggregateSchoolAsync(schoolId);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to aggregate school {SchoolId}", schoolId);
            }
        }

        _logger.Information("Performance aggregation complete for {Count} schools", schools.Count());
    }

    public async Task AggregateSchoolAsync(Guid schoolId)
    {
        _logger.Information("Aggregating performance for school {SchoolId}", schoolId);

        var now = DateTime.UtcNow;
        var nowStr = now.ToString("yyyy-MM-dd HH:mm:ss");
        var logId = Guid.NewGuid();

        var logged = await TryCreateLog(logId, schoolId, nowStr);

        try
        {
            var rows = await FetchAttemptData(schoolId);

            if (!rows.Any())
            {
                _logger.Information("No attempt data for school {SchoolId}, skipping", schoolId);
                await TryCompleteLog(logId, nowStr, "Success", null, 0);
                return;
            }

            var snapshots = new List<PerformanceSnapshot>();

            snapshots.AddRange(AggregateByClassroomSubject(rows, now));
            snapshots.AddRange(AggregateByClassroomSubjectTopic(rows, now));
            snapshots.AddRange(AggregateByClassroomSubjectSubTopic(rows, now));
            snapshots.AddRange(AggregateByStudent(rows, now));
            snapshots.AddRange(AggregateByStudentSubject(rows, now));
            snapshots.AddRange(AggregateByStudentSubTopic(rows, now));
            snapshots.AddRange(AggregateByTeacher(rows, now));
            snapshots.Add(AggregateSchool(rows, schoolId, now));

            foreach (var snapshot in snapshots)
            {
                await _perfRepo.UpsertSnapshotAsync(snapshot);
            }

            await TryCompleteLog(logId, nowStr, "Success", null, snapshots.Count);

            _logger.Information(
                "Aggregated {SnapshotCount} snapshots for school {SchoolId}",
                snapshots.Count, schoolId);
        }
        catch (Exception ex)
        {
            await TryCompleteLog(logId, nowStr, "Failed", ex.ToString(), null);
            _logger.Error(ex, "Failed to aggregate school {SchoolId}", schoolId);
            throw;
        }
    }

    private async Task<bool> TryCreateLog(Guid logId, Guid schoolId, string nowStr)
    {
        try
        {
            var attemptNumber = await GetNextAttemptNumber(schoolId);
            await _logCommand.Create(new PerformanceAggregationLog
            {
                Id = logId,
                SchoolId = schoolId,
                RunStartedAt = nowStr,
                Status = "Running",
                AttemptNumber = attemptNumber
            });
            return true;
        }
        catch (Exception ex)
        {
            _logger.Warning(ex,
                "Could not create aggregation log for school {SchoolId} (table may not exist yet)", schoolId);
            return false;
        }
    }

    private async Task TryCompleteLog(Guid logId, string nowStr, string status, string? errorMessage, int? itemsUpserted)
    {
        try
        {
            var dict = new Dictionary<string, object>
            {
                { "Status", status },
                { "RunCompletedAt", nowStr },
                { "Id", logId }
            };

            if (errorMessage != null)
                dict["ErrorMessage"] = errorMessage;
            if (itemsUpserted.HasValue)
                dict["ItemsUpserted"] = itemsUpserted.Value;

            var setClauses = new List<string> { "Status = @Status", "RunCompletedAt = @RunCompletedAt" };
            if (errorMessage != null) setClauses.Add("ErrorMessage = @ErrorMessage");
            if (itemsUpserted.HasValue) setClauses.Add("ItemsUpserted = @ItemsUpserted");

            var sql = $"UPDATE PerformanceAggregationLog SET {string.Join(", ", setClauses)} WHERE Id = @Id";
            await _logCommand.UpdateAsync(sql, dict);

            _logger.Information(
                "Aggregation log {LogId} for school completed: {Status}" +
                (errorMessage != null ? $", Error: {errorMessage}" : ""),
                logId, status);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Could not update aggregation log {LogId}", logId);
        }
    }

    private async Task<int> GetNextAttemptNumber(Guid schoolId)
    {
        try
        {
            var result = await _attemptQuery.QueryAsync<int>(
                "SELECT ISNULL(MAX(AttemptNumber), 0) + 1 FROM PerformanceAggregationLog WHERE SchoolId = @SchoolId",
                new Dictionary<string, object> { { "SchoolId", schoolId } });
            return result.FirstOrDefault();
        }
        catch
        {
            return 1;
        }
    }

    private async Task<List<AttemptRawRow>> FetchAttemptData(Guid schoolId)
    {
        var sql = $@"
            SELECT
                qa.SchoolId,
                lc.ClassroomId,
                ISNULL(c.Name, 'Unknown')    AS ClassroomName,
                lc.SubjectId,
                ISNULL(s.Subject, 'Unknown') AS SubjectName,
                lc.TopicId,
                ISNULL(t.Name, 'Unknown')    AS TopicName,
                st.Id                        AS SubTopicId,
                ISNULL(lc.SubTopic, '')      AS SubTopicName,
                lc.CreatedBy                 AS TeacherId,
                ISNULL(CONCAT(tchr.FirstName, ' ', tchr.LastName), 'Unknown')
                                             AS TeacherName,
                qa.Id                        AS AttemptId,
                qa.StudentId,
                ISNULL(CONCAT(stud.FirstName, ' ', stud.LastName), 'Unknown')
                                             AS StudentName,
                qa.QuizCode,
                lc.Id                        AS LessonId,
                ISNULL(lc.Aim, '')           AS LessonTitle,
                qa.Status,
                qa.FinalScorePercent,
                qa.IsPassed,
                ISNULL(qa.TotalMarks, 0)     AS TotalMarks,
                ISNULL(qa.AutoMarksObtained, 0) + ISNULL(qa.ManualMarksObtained, 0)
                                             AS ObtainedMarks,
                qa.TimeTakenSeconds,
                qa.SubmittedAt,
                qa.AttemptNumber
            FROM QuizAttempt          qa WITH(NOLOCK)
            JOIN LessonContent        lc  WITH(NOLOCK) ON lc.Id  = qa.LessonId
            JOIN Classroom            c   WITH(NOLOCK) ON c.Id   = lc.ClassroomId
            LEFT JOIN Subjects        s   WITH(NOLOCK) ON s.Id   = lc.SubjectId
            LEFT JOIN Topic           t   WITH(NOLOCK) ON t.Id   = lc.TopicId
            LEFT JOIN SubTopic         st    WITH(NOLOCK) ON st.Name   = lc.SubTopic AND st.SchoolId = lc.SchoolId
            LEFT JOIN Users           tchr  WITH(NOLOCK) ON tchr.Id  = lc.CreatedBy
            LEFT JOIN Users           stud  WITH(NOLOCK) ON stud.Id  = qa.StudentId
            WHERE qa.SchoolId = '{schoolId}'
            ORDER BY qa.SubmittedAt DESC";

        return (await _attemptQuery.QueryAsync<AttemptRawRow>(sql, new Dictionary<string, object>())).ToList();
    }

    private List<PerformanceSnapshot> AggregateByClassroomSubject(List<AttemptRawRow> rows, DateTime now)
    {
        var groups = rows
            .Where(r => r.ClassroomId != Guid.Empty && r.SubjectId != Guid.Empty)
            .GroupBy(r => new { r.ClassroomId, r.ClassroomName, r.SubjectId, r.SubjectName });

        var snapshots = new List<PerformanceSnapshot>();

        foreach (var g in groups)
        {
            var completed = g.Where(r => r.Status != "InProgress").ToList();
            var passed = completed.Count(r => r.IsPassed == true);
            var completedWithScore = completed.Where(r => r.FinalScorePercent.HasValue).ToList();
            var scores = completedWithScore.Select(r => r.FinalScorePercent!.Value).ToList();
            var avgTime = completed.Where(r => r.TimeTakenSeconds.HasValue)
                                   .Select(r => (double)r.TimeTakenSeconds!.Value).ToList();

            var breakdown = g.GroupBy(r => new { r.QuizCode, r.LessonId, r.LessonTitle })
                             .Select(qg =>
                             {
                                 var qCompleted = qg.Where(r => r.Status != "InProgress").ToList();
                                 var qPassed = qCompleted.Count(r => r.IsPassed == true);
                                 var qScores = qCompleted.Where(r => r.FinalScorePercent.HasValue)
                                                          .Select(r => r.FinalScorePercent!.Value).ToList();
                                 return new QuizPerformanceItem
                                 {
                                     QuizCode = qg.Key.QuizCode ?? "",
                                     LessonId = qg.Key.LessonId,
                                     LessonTitle = qg.Key.LessonTitle ?? "",
                                     AttemptCount = qg.Count(),
                                     CompletedCount = qCompleted.Count,
                                     AverageScore = qScores.Any() ? Math.Round(qScores.Average(), 1) : 0m,
                                     PassRate = qCompleted.Any() ? Math.Round((decimal)qPassed / qCompleted.Count * 100, 1) : 0m,
                                     StudentCount = qg.Select(r => r.StudentId).Distinct().Count()
                                 };
                             }).ToList();

            snapshots.Add(new PerformanceSnapshot
            {
                DocType = "classroom_subject",
                SchoolId = g.First().SchoolId,
                ClassroomId = g.Key.ClassroomId,
                ClassroomName = g.Key.ClassroomName,
                SubjectId = g.Key.SubjectId,
                SubjectName = g.Key.SubjectName,
                TotalAttempts = g.Count(),
                CompletedAttempts = completed.Count,
                InProgressAttempts = g.Count(r => r.Status == "InProgress"),
                PartiallyGradedAttempts = g.Count(r => r.Status == "PartiallyGraded"),
                AverageScorePercent = scores.Any() ? Math.Round(scores.Average(), 1) : 0m,
                TotalScoreSum = scores.Sum(),
                PassRate = completed.Any() ? Math.Round((decimal)passed / completed.Count * 100, 1) : 0m,
                StudentCount = g.Select(r => r.StudentId).Distinct().Count(),
                TotalMarksSum = completed.Sum(r => r.TotalMarks),
                ObtainedMarksSum = completed.Sum(r => r.ObtainedMarks),
                AverageTimeTakenSeconds = avgTime.Any() ? Math.Round(avgTime.Average(), 0) : null,
                LastActivityDate = completed.Any() && completed.Max(r => r.SubmittedAtParsed).HasValue
                    ? completed.Max(r => r.SubmittedAtParsed)!.Value
                    : now,
                ComputedAt = now,
                QuizBreakdown = breakdown
            });
        }

        return snapshots;
    }

    private List<PerformanceSnapshot> AggregateByClassroomSubjectTopic(List<AttemptRawRow> rows, DateTime now)
    {
        var groups = rows
            .Where(r => r.ClassroomId != Guid.Empty && r.SubjectId != Guid.Empty && r.TopicId != Guid.Empty)
            .GroupBy(r => new
            {
                r.ClassroomId,
                r.ClassroomName,
                r.SubjectId,
                r.SubjectName,
                r.TopicId,
                r.TopicName
            });

        var snapshots = new List<PerformanceSnapshot>();

        foreach (var g in groups)
        {
            var completed = g.Where(r => r.Status != "InProgress").ToList();
            var passed = completed.Count(r => r.IsPassed == true);
            var completedWithScore = completed.Where(r => r.FinalScorePercent.HasValue).ToList();
            var scores = completedWithScore.Select(r => r.FinalScorePercent!.Value).ToList();
            var avgTime = completed.Where(r => r.TimeTakenSeconds.HasValue)
                                   .Select(r => (double)r.TimeTakenSeconds!.Value).ToList();

            snapshots.Add(new PerformanceSnapshot
            {
                DocType = "classroom_subject_topic",
                SchoolId = g.First().SchoolId,
                ClassroomId = g.Key.ClassroomId,
                ClassroomName = g.Key.ClassroomName,
                SubjectId = g.Key.SubjectId,
                SubjectName = g.Key.SubjectName,
                TopicId = g.Key.TopicId,
                TopicName = g.Key.TopicName,
                TotalAttempts = g.Count(),
                CompletedAttempts = completed.Count,
                InProgressAttempts = g.Count(r => r.Status == "InProgress"),
                PartiallyGradedAttempts = g.Count(r => r.Status == "PartiallyGraded"),
                AverageScorePercent = scores.Any() ? Math.Round(scores.Average(), 1) : 0m,
                TotalScoreSum = scores.Sum(),
                PassRate = completed.Any() ? Math.Round((decimal)passed / completed.Count * 100, 1) : 0m,
                StudentCount = g.Select(r => r.StudentId).Distinct().Count(),
                TotalMarksSum = completed.Sum(r => r.TotalMarks),
                ObtainedMarksSum = completed.Sum(r => r.ObtainedMarks),
                AverageTimeTakenSeconds = avgTime.Any() ? Math.Round(avgTime.Average(), 0) : null,
                LastActivityDate = completed.Any() && completed.Max(r => r.SubmittedAtParsed).HasValue
                    ? completed.Max(r => r.SubmittedAtParsed)!.Value
                    : now,
                ComputedAt = now
            });
        }

        return snapshots;
    }

    private List<PerformanceSnapshot> AggregateByClassroomSubjectSubTopic(List<AttemptRawRow> rows, DateTime now)
    {
        var groups = rows
            .Where(r => r.ClassroomId != Guid.Empty && r.SubjectId != Guid.Empty && r.TopicId != Guid.Empty
                        && !string.IsNullOrEmpty(r.SubTopicName))
            .GroupBy(r => new
            {
                r.ClassroomId,
                r.ClassroomName,
                r.SubjectId,
                r.SubjectName,
                r.TopicId,
                r.TopicName,
                r.SubTopicName
            });

        var snapshots = new List<PerformanceSnapshot>();

        foreach (var g in groups)
        {
            var completed = g.Where(r => r.Status != "InProgress").ToList();
            var passed = completed.Count(r => r.IsPassed == true);
            var completedWithScore = completed.Where(r => r.FinalScorePercent.HasValue).ToList();
            var scores = completedWithScore.Select(r => r.FinalScorePercent!.Value).ToList();

            snapshots.Add(new PerformanceSnapshot
            {
                DocType = "classroom_subject_subtopic",
                SchoolId = g.First().SchoolId,
                ClassroomId = g.Key.ClassroomId,
                ClassroomName = g.Key.ClassroomName,
                SubjectId = g.Key.SubjectId,
                SubjectName = g.Key.SubjectName,
                TopicId = g.Key.TopicId,
                TopicName = g.Key.TopicName,
                SubTopicName = g.Key.SubTopicName,
                TotalAttempts = g.Count(),
                CompletedAttempts = completed.Count,
                InProgressAttempts = g.Count(r => r.Status == "InProgress"),
                PartiallyGradedAttempts = g.Count(r => r.Status == "PartiallyGraded"),
                AverageScorePercent = scores.Any() ? Math.Round(scores.Average(), 1) : 0m,
                TotalScoreSum = scores.Sum(),
                PassRate = completed.Any() ? Math.Round((decimal)passed / completed.Count * 100, 1) : 0m,
                StudentCount = g.Select(r => r.StudentId).Distinct().Count(),
                TotalMarksSum = completed.Sum(r => r.TotalMarks),
                ObtainedMarksSum = completed.Sum(r => r.ObtainedMarks),
                ComputedAt = now
            });
        }

        return snapshots;
    }

    private List<PerformanceSnapshot> AggregateByStudent(List<AttemptRawRow> rows, DateTime now)
    {
        var groups = rows.GroupBy(r => new { r.StudentId, r.StudentName });

        var snapshots = new List<PerformanceSnapshot>();

        foreach (var g in groups)
        {
            var completed = g.Where(r => r.Status != "InProgress").ToList();
            var passed = completed.Count(r => r.IsPassed == true);
            var completedWithScore = completed.Where(r => r.FinalScorePercent.HasValue).ToList();
            var scores = completedWithScore.Select(r => r.FinalScorePercent!.Value).ToList();

            snapshots.Add(new PerformanceSnapshot
            {
                DocType = "student",
                SchoolId = g.First().SchoolId,
                StudentId = g.Key.StudentId,
                StudentName = g.Key.StudentName,
                TotalAttempts = g.Count(),
                CompletedAttempts = completed.Count,
                InProgressAttempts = g.Count(r => r.Status == "InProgress"),
                AverageScorePercent = scores.Any() ? Math.Round(scores.Average(), 1) : 0m,
                TotalScoreSum = scores.Sum(),
                PassRate = completed.Any() ? Math.Round((decimal)passed / completed.Count * 100, 1) : 0m,
                StudentCount = 1,
                TotalMarksSum = completed.Sum(r => r.TotalMarks),
                ObtainedMarksSum = completed.Sum(r => r.ObtainedMarks),
                ComputedAt = now
            });
        }

        return snapshots;
    }

    private List<PerformanceSnapshot> AggregateByStudentSubject(List<AttemptRawRow> rows, DateTime now)
    {
        var groups = rows
            .Where(r => r.SubjectId != Guid.Empty)
            .GroupBy(r => new { r.StudentId, r.StudentName, r.SubjectId, r.SubjectName, r.SchoolId });

        var studentSubjectSnapshots = new List<PerformanceSnapshot>();

        foreach (var g in groups)
        {
            var completed = g.Where(r => r.Status != "InProgress").ToList();
            var completedWithScore = completed.Where(r => r.FinalScorePercent.HasValue).ToList();
            var scores = completedWithScore.Select(r => r.FinalScorePercent!.Value).ToList();

            studentSubjectSnapshots.Add(new PerformanceSnapshot
            {
                DocType = "student_subject",
                SchoolId = g.Key.SchoolId,
                StudentId = g.Key.StudentId,
                StudentName = g.Key.StudentName,
                SubjectId = g.Key.SubjectId,
                SubjectName = g.Key.SubjectName,
                TotalAttempts = g.Count(),
                CompletedAttempts = completed.Count,
                AverageScorePercent = scores.Any() ? Math.Round(scores.Average(), 1) : 0m,
                TotalScoreSum = scores.Sum(),
                PassRate = completed.Any()
                    ? Math.Round((decimal)completed.Count(r => r.IsPassed == true) / completed.Count * 100, 1)
                    : 0m,
                ComputedAt = now
            });
        }

        // Second pass: rank students within each subject
        var subjectGroups = studentSubjectSnapshots
            .GroupBy(s => new { s.SubjectId, s.SubjectName, s.SchoolId });

        var ranked = new List<PerformanceSnapshot>();

        foreach (var sg in subjectGroups)
        {
            var ordered = sg
                .OrderByDescending(s => s.AverageScorePercent)
                .ThenBy(s => s.StudentName)
                .ToList();

            int total = ordered.Count;
            for (int i = 0; i < ordered.Count; i++)
            {
                ordered[i].SubjectRank = i + 1;
                ordered[i].TotalStudentsInSubject = total;
            }

            ranked.AddRange(ordered);
        }

        return ranked;
    }

    private List<PerformanceSnapshot> AggregateByStudentSubTopic(List<AttemptRawRow> rows, DateTime now)
    {
        var groups = rows
            .Where(r => r.SubjectId != Guid.Empty && !string.IsNullOrEmpty(r.SubTopicName))
            .GroupBy(r => new
            {
                r.StudentId,
                r.StudentName,
                r.SubjectId,
                r.SubjectName,
                r.SubTopicName,
                r.SchoolId
            });

        var studentSubTopicSnapshots = new List<PerformanceSnapshot>();

        foreach (var g in groups)
        {
            var firstAttempts = g.Where(r => r.AttemptNumber == 1).ToList();
            var completedFirst = firstAttempts.Where(r => r.Status != "InProgress").ToList();
            var scores = completedFirst.Where(r => r.FinalScorePercent.HasValue)
                                       .Select(r => r.FinalScorePercent!.Value).ToList();

            var subTopicId = g.Select(r => r.SubTopicId).FirstOrDefault(id => id.HasValue);

            studentSubTopicSnapshots.Add(new PerformanceSnapshot
            {
                DocType = "student_subtopic",
                SchoolId = g.Key.SchoolId,
                StudentId = g.Key.StudentId,
                StudentName = g.Key.StudentName,
                SubjectId = g.Key.SubjectId,
                SubjectName = g.Key.SubjectName,
                SubTopicId = subTopicId,
                SubTopicName = g.Key.SubTopicName,
                TotalAttempts = g.Count(),
                CompletedAttempts = completedFirst.Count,
                AverageScorePercent = scores.Any() ? Math.Round(scores.Average(), 1) : 0m,
                TotalScoreSum = scores.Sum(),
                PassRate = completedFirst.Any()
                    ? Math.Round((decimal)completedFirst.Count(r => r.IsPassed == true) / completedFirst.Count * 100, 1)
                    : 0m,
                ComputedAt = now
            });
        }

        // Second pass: rank students within each subtopic
        var subTopicGroups = studentSubTopicSnapshots
            .GroupBy(s => new { s.SubjectId, s.SubTopicName, s.SchoolId });

        var ranked = new List<PerformanceSnapshot>();

        foreach (var sg in subTopicGroups)
        {
            var ordered = sg
                .OrderByDescending(s => s.AverageScorePercent)
                .ThenBy(s => s.StudentName)
                .ToList();

            int total = ordered.Count;
            for (int i = 0; i < ordered.Count; i++)
            {
                ordered[i].SubTopicRank = i + 1;
                ordered[i].TotalStudentsInSubTopic = total;
            }

            ranked.AddRange(ordered);
        }

        return ranked;
    }

    private List<PerformanceSnapshot> AggregateByTeacher(List<AttemptRawRow> rows, DateTime now)
    {
        var groups = rows.GroupBy(r => new { r.TeacherId, r.TeacherName });

        var snapshots = new List<PerformanceSnapshot>();

        foreach (var g in groups)
        {
            var completed = g.Where(r => r.Status != "InProgress").ToList();
            var passed = completed.Count(r => r.IsPassed == true);
            var completedWithScore = completed.Where(r => r.FinalScorePercent.HasValue).ToList();
            var scores = completedWithScore.Select(r => r.FinalScorePercent!.Value).ToList();

            snapshots.Add(new PerformanceSnapshot
            {
                DocType = "teacher",
                SchoolId = g.First().SchoolId,
                TeacherId = g.Key.TeacherId,
                TeacherName = g.Key.TeacherName,
                TotalAttempts = g.Count(),
                CompletedAttempts = completed.Count,
                AverageScorePercent = scores.Any() ? Math.Round(scores.Average(), 1) : 0m,
                TotalScoreSum = scores.Sum(),
                PassRate = completed.Any() ? Math.Round((decimal)passed / completed.Count * 100, 1) : 0m,
                StudentCount = g.Select(r => r.StudentId).Distinct().Count(),
                TotalMarksSum = completed.Sum(r => r.TotalMarks),
                ObtainedMarksSum = completed.Sum(r => r.ObtainedMarks),
                ComputedAt = now
            });
        }

        return snapshots;
    }

    private PerformanceSnapshot AggregateSchool(List<AttemptRawRow> rows, Guid schoolId, DateTime now)
    {
        var completed = rows.Where(r => r.Status != "InProgress").ToList();
        var passed = completed.Count(r => r.IsPassed == true);
        var completedWithScore = completed.Where(r => r.FinalScorePercent.HasValue).ToList();
        var scores = completedWithScore.Select(r => r.FinalScorePercent!.Value).ToList();

        return new PerformanceSnapshot
        {
            DocType = "school",
            SchoolId = schoolId,
            TotalAttempts = rows.Count(),
            CompletedAttempts = completed.Count,
            InProgressAttempts = rows.Count(r => r.Status == "InProgress"),
            PartiallyGradedAttempts = rows.Count(r => r.Status == "PartiallyGraded"),
            AverageScorePercent = scores.Any() ? Math.Round(scores.Average(), 1) : 0m,
            TotalScoreSum = scores.Sum(),
            PassRate = completed.Any() ? Math.Round((decimal)passed / completed.Count * 100, 1) : 0m,
            StudentCount = rows.Select(r => r.StudentId).Distinct().Count(),
            TotalMarksSum = completed.Sum(r => r.TotalMarks),
            ObtainedMarksSum = completed.Sum(r => r.ObtainedMarks),
            ComputedAt = now
        };
    }

    private class AttemptRawRow
    {
        public Guid SchoolId { get; set; }
        public Guid ClassroomId { get; set; }
        public string ClassroomName { get; set; } = string.Empty;
        public Guid SubjectId { get; set; }
        public string SubjectName { get; set; } = string.Empty;
        public Guid TopicId { get; set; }
        public string TopicName { get; set; } = string.Empty;
        public Guid? SubTopicId { get; set; }
        public string SubTopicName { get; set; } = string.Empty;
        public Guid TeacherId { get; set; }
        public string TeacherName { get; set; } = string.Empty;
        public Guid AttemptId { get; set; }
        public Guid StudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string? QuizCode { get; set; }
        public Guid LessonId { get; set; }
        public string LessonTitle { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal? FinalScorePercent { get; set; }
        public bool? IsPassed { get; set; }
        public decimal TotalMarks { get; set; }
        public decimal ObtainedMarks { get; set; }
        public int? TimeTakenSeconds { get; set; }
        public string? SubmittedAt { get; set; }
        public int AttemptNumber { get; set; }

        public DateTime? SubmittedAtParsed =>
            DateTime.TryParse(SubmittedAt, out var dt) ? dt : null;
    }
}
