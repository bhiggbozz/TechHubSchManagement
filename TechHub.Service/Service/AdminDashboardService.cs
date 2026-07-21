using Serilog;
using TechHub.Core;
using TechHub.Core.Entities;
using TechHub.Core.Entities.Performance;
using TechHub.Core.Enum;
using TechHub.Core.Model;
using TechHub.Core.ViewModel.classroom;
using TechHub.Service.Interface;
using TechHub.Service.Service.DatabaseService;

namespace TechHub.Service.Service;

public class AdminDashboardService : IAdminDashboardService
{
    private readonly IAdminDashboardRepository _dashboardRepo;
    private readonly IPerformanceRepository _perfRepo;
    private readonly IQueryRepository<LessonContent> _lessonQuery;
    private readonly IQueryRepository<TeacherTrustScore> _trustScoreQuery;
    private readonly IQueryRepository<Users> _userQuery;
    private readonly ILogger _logger;

    public AdminDashboardService(
        IAdminDashboardRepository dashboardRepo,
        IPerformanceRepository perfRepo,
        IQueryRepository<LessonContent> lessonQuery,
        IQueryRepository<TeacherTrustScore> trustScoreQuery,
        IQueryRepository<Users> userQuery,
        ILogger logger)
    {
        _dashboardRepo = dashboardRepo;
        _perfRepo = perfRepo;
        _lessonQuery = lessonQuery;
        _trustScoreQuery = trustScoreQuery;
        _userQuery = userQuery;
        _logger = logger;
    }

    // ═════════════════════════════════════════════════════════
    // FAST ENDPOINT — SINGLE MongoDB READ
    // ═════════════════════════════════════════════════════════

    public async Task<BaseResponse> GetDashboardAsync(AuthenticatedUserClaims claims)
    {
        if (!Guid.TryParse(claims.SchoolId, out var schoolId))
            return Unauthorized();
        if (!Enum.TryParse<UserRole>(claims.Role, ignoreCase: true, out var role)
            || (role != UserRole.Administrator && role != UserRole.SuperAdministrator))
            return Forbidden();

        var data = await _dashboardRepo.GetBySchoolAsync(schoolId);
        if (data == null)
            return Success(new AdminDashboardResponseDto { ComputedAt = DateTime.UtcNow });

        var dto = new AdminDashboardResponseDto
        {
            ComputedAt = data.ComputedAt,
            Overview = new AdminOverviewDto
            {
                TotalStudents = data.TotalStudents,
                TotalTeachers = data.TotalTeachers,
                TotalClassrooms = data.TotalClassrooms,
                TotalSubjects = data.TotalSubjects,
                TotalLessonsCreated = data.TotalLessonsCreated,
                TotalLessonsPublished = data.TotalLessonsPublished,
                OverallAverageScore = data.OverallAverageScore,
                OverallPassRate = data.OverallPassRate,
                TotalLessonWatches = data.TotalLessonWatches,
                StudentsWhoWatchedLessons = data.StudentsWhoWatchedLessons
            },
            TeacherActivities = data.TeacherActivities.Select(MapTeacherActivity).ToList(),
            ClassroomPerformances = data.ClassroomPerformances.Select(MapClassroomPerformance).ToList(),
            SubjectPerformances = data.SubjectPerformances.Select(MapSubjectPerformance).ToList()
        };

        return Success(dto);
    }

    public async Task<BaseResponse> GetTeacherActivityAsync(AuthenticatedUserClaims claims, Guid? teacherId = null)
    {
        if (!Guid.TryParse(claims.SchoolId, out var schoolId))
            return Unauthorized();
        if (!Enum.TryParse<UserRole>(claims.Role, ignoreCase: true, out var role)
            || (role != UserRole.Administrator && role != UserRole.SuperAdministrator))
            return Forbidden();

        var data = await _dashboardRepo.GetBySchoolAsync(schoolId);
        if (data == null)
            return Success(new List<AdminTeacherActivityDto>());

        var activities = data.TeacherActivities
            .Where(t => teacherId == null || t.TeacherId == teacherId.Value)
            .Select(MapTeacherActivity)
            .ToList();

        return Success(activities);
    }

    public async Task<BaseResponse> GetClassroomPerformanceAsync(AuthenticatedUserClaims claims, Guid? classroomId = null)
    {
        if (!Guid.TryParse(claims.SchoolId, out var schoolId))
            return Unauthorized();
        if (!Enum.TryParse<UserRole>(claims.Role, ignoreCase: true, out var role)
            || (role != UserRole.Administrator && role != UserRole.SuperAdministrator))
            return Forbidden();

        var data = await _dashboardRepo.GetBySchoolAsync(schoolId);
        if (data == null)
            return Success(new List<AdminClassroomPerformanceDto>());

        var classrooms = data.ClassroomPerformances
            .Where(c => classroomId == null || c.ClassroomId == classroomId.Value)
            .Select(MapClassroomPerformance)
            .ToList();

        return Success(classrooms);
    }

    public async Task<BaseResponse> GetSubjectPerformanceAsync(AuthenticatedUserClaims claims, Guid? subjectId = null)
    {
        if (!Guid.TryParse(claims.SchoolId, out var schoolId))
            return Unauthorized();
        if (!Enum.TryParse<UserRole>(claims.Role, ignoreCase: true, out var role)
            || (role != UserRole.Administrator && role != UserRole.SuperAdministrator))
            return Forbidden();

        var data = await _dashboardRepo.GetBySchoolAsync(schoolId);
        if (data == null)
            return Success(new List<AdminSubjectPerformanceDto>());

        var subjects = data.SubjectPerformances
            .Where(s => subjectId == null || s.SubjectId == subjectId.Value)
            .Select(MapSubjectPerformance)
            .ToList();

        return Success(subjects);
    }

    // ═════════════════════════════════════════════════════════
    // BACKGROUND AGGREGATION
    // ═════════════════════════════════════════════════════════

    public async Task AggregateAllSchoolsAsync()
    {
        _logger.Information("Starting admin dashboard aggregation for all schools");

        var schoolIds = await _lessonQuery.QueryAsync<Guid>($@"
            SELECT DISTINCT SchoolId FROM LessonContent WITH(NOLOCK) WHERE IsActive = 1",
            new Dictionary<string, object>());

        foreach (var schoolId in schoolIds)
        {
            try
            {
                await AggregateSchoolAsync(schoolId);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to aggregate admin dashboard for school {SchoolId}", schoolId);
            }
        }

        _logger.Information("Admin dashboard aggregation complete for {Count} schools", schoolIds.Count());
    }

    public async Task AggregateSchoolAsync(Guid schoolId)
    {
        _logger.Information("Aggregating admin dashboard for school {SchoolId}", schoolId);

        var now = DateTime.UtcNow;

        // ── 1. Counts from SQL ────────────────────────────────
        var totalStudents = await CountStudents(schoolId);
        var totalTeachers = await CountTeachers(schoolId);
        var totalClassrooms = await CountClassrooms(schoolId);
        var totalSubjects = await CountSubjects(schoolId);

        // ── 2. Lesson stats ───────────────────────────────────
        var lessonStats = await GetLessonStats(schoolId);

        // ── 3. Lesson watch stats ─────────────────────────────
        var (totalWatches, studentsWatched) = await GetLessonWatchStats(schoolId);

        // ── 4. Teacher activity breakdown from SQL ────────────
        var teacherActivities = await GetTeacherActivityBreakdown(schoolId);

        // ── 5. School-level performance from existing MongoDB ─
        var schoolAgg = await _perfRepo.GetSchoolAggregateAsync(schoolId);

        // ── 6. Classroom/subject performance from MongoDB ─────
        var snapshots = await _perfRepo.GetBySchoolAsync(schoolId);
        var classSubjectSnapshots = snapshots
            .Where(s => s.DocType == "classroom_subject")
            .ToList();

        var classroomPerf = classSubjectSnapshots
            .GroupBy(s => new { s.ClassroomId, s.ClassroomName })
            .Select(g => new AdminClassroomPerformanceItem
            {
                ClassroomId = g.Key.ClassroomId ?? Guid.Empty,
                ClassroomName = g.Key.ClassroomName ?? "Unknown",
                StudentCount = g.Sum(s => s.StudentCount),
                TotalAttempts = g.Sum(s => s.TotalAttempts),
                CompletedAttempts = g.Sum(s => s.CompletedAttempts),
                AverageScorePercent = g.Where(s => s.TotalAttempts > 0)
                    .Select(s => s.AverageScorePercent).DefaultIfEmpty(0).Average(),
                PassRate = g.Where(s => s.TotalAttempts > 0)
                    .Select(s => s.PassRate).DefaultIfEmpty(0).Average(),
                LessonWatchCount = 0
            })
            .ToList();

        var subjectPerf = classSubjectSnapshots
            .GroupBy(s => new { s.SubjectId, s.SubjectName })
            .Select(g => new AdminSubjectPerformanceItem
            {
                SubjectId = g.Key.SubjectId ?? Guid.Empty,
                SubjectName = g.Key.SubjectName ?? "Unknown",
                StudentCount = g.Sum(s => s.StudentCount),
                TotalAttempts = g.Sum(s => s.TotalAttempts),
                CompletedAttempts = g.Sum(s => s.CompletedAttempts),
                AverageScorePercent = g.Where(s => s.TotalAttempts > 0)
                    .Select(s => s.AverageScorePercent).DefaultIfEmpty(0).Average(),
                PassRate = g.Where(s => s.TotalAttempts > 0)
                    .Select(s => s.PassRate).DefaultIfEmpty(0).Average()
            })
            .ToList();

        // ── 7. Build & store dashboard document ───────────────
        var dashboard = new AdminDashboardData
        {
            SchoolId = schoolId,
            ComputedAt = now,
            TotalStudents = totalStudents,
            TotalTeachers = totalTeachers,
            TotalClassrooms = totalClassrooms,
            TotalSubjects = totalSubjects,
            TotalLessonsCreated = lessonStats.TotalCreated,
            TotalLessonsPublished = lessonStats.TotalPublished,
            OverallAverageScore = schoolAgg?.AverageScorePercent ?? 0,
            OverallPassRate = schoolAgg?.PassRate ?? 0,
            TotalLessonWatches = totalWatches,
            StudentsWhoWatchedLessons = studentsWatched,
            TeacherActivities = teacherActivities,
            ClassroomPerformances = classroomPerf,
            SubjectPerformances = subjectPerf
        };

        await _dashboardRepo.UpsertDashboardAsync(dashboard);

        _logger.Information("Admin dashboard aggregated for school {SchoolId}", schoolId);
    }

    // ═════════════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ═════════════════════════════════════════════════════════

    private async Task<int> CountStudents(Guid schoolId)
    {
        var result = await _userQuery.QueryAsync<int>($@"
            SELECT COUNT(*) FROM Users WITH(NOLOCK)
            WHERE SchoolId = '{schoolId}' AND IsActive = 1
            AND RoleId IN (SELECT Id FROM Role WHERE Name = 'Student')",
            new Dictionary<string, object>());
        return result.FirstOrDefault();
    }

    private async Task<int> CountTeachers(Guid schoolId)
    {
        var result = await _userQuery.QueryAsync<int>($@"
            SELECT COUNT(DISTINCT TeacherId) FROM TeacherSubject WITH(NOLOCK)
            WHERE SchoolId = '{schoolId}' AND IsActive = 1",
            new Dictionary<string, object>());
        return result.FirstOrDefault();
    }

    private async Task<int> CountClassrooms(Guid schoolId)
    {
        var result = await _userQuery.QueryAsync<int>($@"
            SELECT COUNT(*) FROM Classroom WITH(NOLOCK)
            WHERE SchoolId = '{schoolId}' AND IsActive = 1",
            new Dictionary<string, object>());
        return result.FirstOrDefault();
    }

    private async Task<int> CountSubjects(Guid schoolId)
    {
        var result = await _userQuery.QueryAsync<int>($@"
            SELECT COUNT(*) FROM Subjects WITH(NOLOCK)
            WHERE SchoolId = '{schoolId}' AND IsActive = 1",
            new Dictionary<string, object>());
        return result.FirstOrDefault();
    }

    private async Task<(int TotalCreated, int TotalPublished)> GetLessonStats(Guid schoolId)
    {
        var result = await _lessonQuery.QueryAsync<LessonStatsRow>($@"
            SELECT
                COUNT(*) AS TotalCreated,
                SUM(CASE WHEN Status = 'Published' THEN 1 ELSE 0 END) AS TotalPublished
            FROM LessonContent WITH(NOLOCK)
            WHERE SchoolId = '{schoolId}' AND IsActive = 1",
            new Dictionary<string, object>());
        var row = result.FirstOrDefault();
        return (row?.TotalCreated ?? 0, row?.TotalPublished ?? 0);
    }

    private async Task<(int TotalWatches, int StudentsWatched)> GetLessonWatchStats(Guid schoolId)
    {
        var result = await _lessonQuery.QueryAsync<LessonWatchStatsRow>($@"
            SELECT
                COUNT(*) AS TotalWatches,
                COUNT(DISTINCT StudentId) AS StudentsWatched
            FROM StudentLessonProgress WITH(NOLOCK)
            WHERE SchoolId = '{schoolId}'",
            new Dictionary<string, object>());
        var row = result.FirstOrDefault();
        return (row?.TotalWatches ?? 0, row?.StudentsWatched ?? 0);
    }

    private async Task<List<AdminTeacherActivityItem>> GetTeacherActivityBreakdown(Guid schoolId)
    {
        var rawActivities = await _lessonQuery.QueryAsync<TeacherActivityRawRow>($@"
            SELECT
                lc.CreatedBy AS TeacherId,
                ISNULL(CONCAT(u.FirstName, ' ', u.LastName), 'Unknown') AS TeacherName,
                COUNT(*) AS TotalLessons,
                SUM(CASE WHEN lc.Status = 'Published' THEN 1 ELSE 0 END) AS Published,
                SUM(CASE WHEN lc.Status = 'Draft' THEN 1 ELSE 0 END) AS Draft,
                SUM(CASE WHEN lc.Status = 'PendingApproval' THEN 1 ELSE 0 END) AS PendingApproval,
                SUM(CASE WHEN lc.Status = 'Rejected' THEN 1 ELSE 0 END) AS Rejected,
                MAX(lc.CreatedAt) AS LastLessonCreated
            FROM LessonContent lc WITH(NOLOCK)
            JOIN Users u WITH(NOLOCK) ON u.Id = lc.CreatedBy
            WHERE lc.SchoolId = '{schoolId}' AND lc.IsActive = 1
            GROUP BY lc.CreatedBy, u.FirstName, u.LastName",
            new Dictionary<string, object>());

        var rawList = rawActivities.ToList();
        var teacherIds = rawList.Select(r => r.TeacherId).Distinct().ToList();

        var trustScores = new Dictionary<Guid, TeacherTrustScore>();
        foreach (var tid in teacherIds)
        {
            try
            {
                var scores = await _trustScoreQuery.QueryAsync<TeacherTrustScore>($@"
                    SELECT TOP 1 * FROM TeacherTrustScore WITH(NOLOCK)
                    WHERE TeacherId = '{tid}' AND SchoolId = '{schoolId}'",
                    new Dictionary<string, object>());
                var score = scores.FirstOrDefault();
                if (score != null)
                    trustScores[tid] = score;
            }
            catch { }
        }

        var activities = rawList.Select(r =>
        {
            trustScores.TryGetValue(r.TeacherId, out var ts);
            var score = ts?.TrustScore ?? 0;
            return new AdminTeacherActivityItem
            {
                TeacherId = r.TeacherId,
                TeacherName = r.TeacherName,
                TotalLessonsCreated = r.TotalLessons,
                PublishedLessons = r.Published,
                DraftLessons = r.Draft,
                PendingApprovalLessons = r.PendingApproval,
                RejectedLessons = r.Rejected,
                TrustScore = score,
                TrustLevel = GetTrustLevel(score),
                LastLessonCreated = r.LastLessonCreated
            };
        }).ToList();

        return activities;
    }

    private static string GetTrustLevel(decimal score) => score switch
    {
        >= 95 => "Excellent",
        >= 80 => "Good",
        >= 60 => "Fair",
        > 0 => "Needs Improvement",
        _ => "N/A"
    };

    private static AdminTeacherActivityDto MapTeacherActivity(AdminTeacherActivityItem item) => new()
    {
        TeacherId = item.TeacherId,
        TeacherName = item.TeacherName,
        TotalLessonsCreated = item.TotalLessonsCreated,
        PublishedLessons = item.PublishedLessons,
        DraftLessons = item.DraftLessons,
        PendingApprovalLessons = item.PendingApprovalLessons,
        RejectedLessons = item.RejectedLessons,
        TrustScore = item.TrustScore,
        TrustLevel = item.TrustLevel,
        LastLessonCreated = item.LastLessonCreated
    };

    private static AdminClassroomPerformanceDto MapClassroomPerformance(AdminClassroomPerformanceItem item) => new()
    {
        ClassroomId = item.ClassroomId,
        ClassroomName = item.ClassroomName,
        StudentCount = item.StudentCount,
        AverageScorePercent = Math.Round(item.AverageScorePercent, 1),
        PassRate = Math.Round(item.PassRate, 1),
        TotalAttempts = item.TotalAttempts,
        CompletedAttempts = item.CompletedAttempts,
        LessonWatchCount = item.LessonWatchCount
    };

    private static AdminSubjectPerformanceDto MapSubjectPerformance(AdminSubjectPerformanceItem item) => new()
    {
        SubjectId = item.SubjectId,
        SubjectName = item.SubjectName,
        AverageScorePercent = Math.Round(item.AverageScorePercent, 1),
        PassRate = Math.Round(item.PassRate, 1),
        TotalAttempts = item.TotalAttempts,
        CompletedAttempts = item.CompletedAttempts,
        StudentCount = item.StudentCount
    };

    private static BaseResponse Success(object? data = null) => new()
    {
        ResponseCode = ResponseCode.successful,
        ResponseMessage = "successful",
        Status = "successful",
        Data = data
    };

    private static BaseResponse Forbidden(string message = "Access denied") => new()
    {
        ResponseCode = ResponseCode.Forbidden,
        ResponseMessage = message,
        Status = "failed"
    };

    private static BaseResponse Unauthorized() => new()
    {
        ResponseCode = ResponseCode.Unauthorized,
        ResponseMessage = "Invalid credentials",
        Status = "failed"
    };

    // ═════════════════════════════════════════════════════════
    // DTO ROW TYPES (Dapper queries)
    // ═════════════════════════════════════════════════════════

    private class LessonStatsRow
    {
        public int TotalCreated { get; set; }
        public int TotalPublished { get; set; }
    }

    private class LessonWatchStatsRow
    {
        public int TotalWatches { get; set; }
        public int StudentsWatched { get; set; }
    }

    private class TeacherActivityRawRow
    {
        public Guid TeacherId { get; set; }
        public string TeacherName { get; set; } = string.Empty;
        public int TotalLessons { get; set; }
        public int Published { get; set; }
        public int Draft { get; set; }
        public int PendingApproval { get; set; }
        public int Rejected { get; set; }
        public DateTime? LastLessonCreated { get; set; }
    }
}
