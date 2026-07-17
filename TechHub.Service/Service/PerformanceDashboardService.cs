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

public class PerformanceDashboardService : IPerformanceDashboardService
{
    private readonly IPerformanceRepository _perfRepo;
    private readonly IQueryRepository<TeacherClassroom> _teacherClassroomQuery;
    private readonly IQueryRepository<TeacherSubject> _teacherSubjectQuery;
    private readonly IQueryRepository<ClassroomTeacher> _classroomTeacherQuery;
    private readonly IQueryRepository<QuizAttempt> _attemptQuery;
    private readonly IQueryRepository<QuizAttemptAnswer> _answerQuery;
    private readonly IQueryRepository<ApprovalRequests> _approvalQuery;
    private readonly ILogger _logger;

    public PerformanceDashboardService(
        IPerformanceRepository perfRepo,
        IQueryRepository<TeacherClassroom> teacherClassroomQuery,
        IQueryRepository<TeacherSubject> teacherSubjectQuery,
        IQueryRepository<ClassroomTeacher> classroomTeacherQuery,
        IQueryRepository<QuizAttempt> attemptQuery,
        IQueryRepository<QuizAttemptAnswer> answerQuery,
        IQueryRepository<ApprovalRequests> approvalQuery,
        ILogger logger)
    {
        _perfRepo = perfRepo;
        _teacherClassroomQuery = teacherClassroomQuery;
        _teacherSubjectQuery = teacherSubjectQuery;
        _classroomTeacherQuery = classroomTeacherQuery;
        _attemptQuery = attemptQuery;
        _answerQuery = answerQuery;
        _approvalQuery = approvalQuery;
        _logger = logger;
    }

    public async Task<BaseResponse> GetNavbarAsync(AuthenticatedUserClaims claims)
    {
        if (!Guid.TryParse(claims.SchoolId, out var schoolId))
            return Unauthorized();
        if (!Guid.TryParse(claims.UserId, out var userId))
            return Unauthorized();
        if (!Enum.TryParse<UserRole>(claims.Role, ignoreCase: true, out var role))
            return Unauthorized();

        try
        {
            return role switch
            {
                UserRole.Administrator or UserRole.SuperAdministrator
                    => await GetAdminNavbar(schoolId),
                UserRole.HeadTeacher
                    => await GetHeadTeacherNavbar(userId, schoolId),
                UserRole.SubjectTeacher
                    => await GetSubjectTeacherNavbar(userId, schoolId),
                UserRole.ClassTeacher
                    => await GetClassTeacherNavbar(userId, schoolId),
                UserRole.Student
                    => await GetStudentNavbar(userId, schoolId),
                _ => Forbidden()
            };
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting navbar for user {UserId}", userId);
            return Error();
        }
    }

    public async Task<BaseResponse> GetDashboardAsync(AuthenticatedUserClaims claims)
    {
        if (!Guid.TryParse(claims.SchoolId, out var schoolId))
            return Unauthorized();
        if (!Guid.TryParse(claims.UserId, out var userId))
            return Unauthorized();
        if (!Enum.TryParse<UserRole>(claims.Role, ignoreCase: true, out var role))
            return Unauthorized();

        try
        {
            switch (role)
            {
                case UserRole.Administrator:
                case UserRole.SuperAdministrator:
                    return await GetAdminDashboard(schoolId);

                case UserRole.HeadTeacher:
                    return await GetHeadTeacherDashboard(userId, schoolId);

                case UserRole.SubjectTeacher:
                    return await GetSubjectTeacherDashboard(userId, schoolId);

                case UserRole.ClassTeacher:
                    return await GetClassTeacherDashboard(userId, schoolId);

                case UserRole.Student:
                    return await GetStudentDashboard(userId);

                default:
                    return Forbidden();
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting performance dashboard for user {UserId}", userId);
            return Error();
        }
    }

    public async Task<BaseResponse> GetClassroomDetailAsync(Guid classroomId, AuthenticatedUserClaims claims)
    {
        if (!Guid.TryParse(claims.SchoolId, out var schoolId))
            return Unauthorized();
        if (!Guid.TryParse(claims.UserId, out var userId))
            return Unauthorized();
        if (!Enum.TryParse<UserRole>(claims.Role, ignoreCase: true, out var role))
            return Unauthorized();

        try
        {
            if (!await CanAccessClassroom(userId, classroomId, role))
                return Forbidden("You do not have access to this classroom");

            var snapshots = await _perfRepo.GetByClassroomAsync(schoolId, classroomId);
            var dtos = snapshots.Where(s => s.DocType == "classroom_subject")
                                .Select(MapToDashboardDto).ToList();

            return Success(dtos);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting classroom detail {ClassroomId}", classroomId);
            return Error();
        }
    }

    public async Task<BaseResponse> GetSubjectDetailAsync(Guid subjectId, AuthenticatedUserClaims claims)
    {
        if (!Guid.TryParse(claims.SchoolId, out var schoolId))
            return Unauthorized();
        if (!Guid.TryParse(claims.UserId, out var userId))
            return Unauthorized();
        if (!Enum.TryParse<UserRole>(claims.Role, ignoreCase: true, out var role))
            return Unauthorized();

        try
        {
            if (!await CanAccessSubject(userId, subjectId, role))
                return Forbidden("You do not have access to this subject");

            var snapshots = await _perfRepo.GetBySubjectAsync(schoolId, subjectId);
            var dtos = snapshots.Where(s => s.DocType == "classroom_subject")
                                .Select(MapToDashboardDto).ToList();

            return Success(dtos);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting subject detail {SubjectId}", subjectId);
            return Error();
        }
    }

    private async Task<BaseResponse> GetAdminDashboard(Guid schoolId)
    {
        var allSnapshots = await _perfRepo.GetBySchoolAsync(schoolId);

        var classSubject = allSnapshots
            .Where(s => s.DocType == "classroom_subject").ToList();
        var schoolAgg = allSnapshots
            .FirstOrDefault(s => s.DocType == "school");

        var classroomGroups = classSubject
            .GroupBy(s => s.ClassroomId)
            .Select(g => new PerformanceDashboardDto
            {
                ClassroomId = g.Key,
                ClassroomName = g.First().ClassroomName,
                StudentCount = g.Sum(s => s.StudentCount),
                TotalAttempts = g.Sum(s => s.TotalAttempts),
                CompletedAttempts = g.Sum(s => s.CompletedAttempts),
                AverageScorePercent = g.Where(s => s.TotalAttempts > 0)
                    .Select(s => s.AverageScorePercent).DefaultIfEmpty(0).Average(),
                PassRate = g.Where(s => s.TotalAttempts > 0)
                    .Select(s => s.PassRate).DefaultIfEmpty(0).Average(),
                LastActivityDate = g.Max(s => s.LastActivityDate),
                ComputedAt = g.Max(s => s.ComputedAt)
            }).ToList();

        var subjectGroups = classSubject
            .GroupBy(s => s.SubjectId)
            .Select(g => new PerformanceDashboardDto
            {
                SubjectId = g.Key,
                SubjectName = g.First().SubjectName,
                StudentCount = g.Sum(s => s.StudentCount),
                TotalAttempts = g.Sum(s => s.TotalAttempts),
                CompletedAttempts = g.Sum(s => s.CompletedAttempts),
                AverageScorePercent = g.Where(s => s.TotalAttempts > 0)
                    .Select(s => s.AverageScorePercent).DefaultIfEmpty(0).Average(),
                PassRate = g.Where(s => s.TotalAttempts > 0)
                    .Select(s => s.PassRate).DefaultIfEmpty(0).Average(),
                LastActivityDate = g.Max(s => s.LastActivityDate),
                ComputedAt = g.Max(s => s.ComputedAt)
            }).ToList();

        var overview = new SchoolPerformanceOverviewDto
        {
            TotalStudents = classSubject.Sum(s => s.StudentCount),
            TotalAttempts = classSubject.Sum(s => s.TotalAttempts),
            CompletedAttempts = classSubject.Sum(s => s.CompletedAttempts),
            OverallAverageScore = schoolAgg?.AverageScorePercent ?? 0,
            OverallPassRate = schoolAgg?.PassRate ?? 0,
            ClassroomCount = classroomGroups.Count,
            SubjectCount = subjectGroups.Count,
            ComputedAt = schoolAgg?.ComputedAt ?? DateTime.UtcNow,
            ClassroomBreakdown = classroomGroups,
            SubjectBreakdown = subjectGroups
        };

        return Success(overview);
    }

    // ═════════════════════════════════════════════════════════
    // NAVBAR METHODS
    // ═════════════════════════════════════════════════════════

    private async Task<BaseResponse> GetAdminNavbar(Guid schoolId)
    {
        var agg = await _perfRepo.GetSchoolAggregateAsync(schoolId);
        var allSnapshots = await _perfRepo.GetBySchoolAsync(schoolId);

        var classSubject = allSnapshots
            .Where(s => s.DocType == "classroom_subject").ToList();

        var totalStudents = classSubject.Sum(s => s.StudentCount);
        var totalTeachers = classSubject.Select(s => s.TeacherId).Distinct().Count();
        var totalClassrooms = classSubject.Select(s => s.ClassroomId).Distinct().Count();
        var pendingGrades = await CountPendingGrading(schoolId, null);

        return Success(new AdminNavbarDto
        {
            TotalStudents = totalStudents,
            TotalTeachers = totalTeachers,
            TotalClassrooms = totalClassrooms,
            TotalAttempts = agg?.TotalAttempts ?? 0,
            OverallAverageScore = agg?.AverageScorePercent ?? 0,
            OverallPassRate = agg?.PassRate ?? 0,
            PendingGradingItems = pendingGrades
        });
    }

    private async Task<BaseResponse> GetSubjectTeacherNavbar(Guid teacherId, Guid schoolId)
    {
        var myClassroomIds = (await _teacherClassroomQuery.QueryAsync<Guid>($@"
            SELECT ClassroomId FROM TeacherClassroom
            WHERE TeacherId = '{teacherId}' AND SchoolId = '{schoolId}' AND IsActive = 1",
            new Dictionary<string, object>())).ToList();

        var mySubjectIds = (await _teacherSubjectQuery.QueryAsync<Guid>($@"
            SELECT SubjectId FROM TeacherSubject
            WHERE TeacherId = '{teacherId}' AND SchoolId = '{schoolId}' AND IsActive = 1",
            new Dictionary<string, object>())).ToList();

        var allSnapshots = await _perfRepo.GetBySchoolAsync(schoolId);

        var filtered = allSnapshots
            .Where(s => s.DocType == "classroom_subject"
                     && myClassroomIds.Contains(s.ClassroomId ?? Guid.Empty)
                     && mySubjectIds.Contains(s.SubjectId ?? Guid.Empty))
            .ToList();

        var scores = filtered.Where(s => s.TotalAttempts > 0)
            .Select(s => s.AverageScorePercent).DefaultIfEmpty(0).ToList();
        var passRates = filtered.Where(s => s.TotalAttempts > 0)
            .Select(s => s.PassRate).DefaultIfEmpty(0).ToList();

        var pendingGrades = await CountPendingGrading(schoolId, teacherId);

        return Success(new SubjectTeacherNavbarDto
        {
            ClassCount = filtered.Select(s => s.ClassroomId).Distinct().Count(),
            SubjectCount = filtered.Select(s => s.SubjectId).Distinct().Count(),
            TotalStudents = filtered.Sum(s => s.StudentCount),
            OverallAverageScore = scores.Any() ? Math.Round(scores.Average(), 1) : 0m,
            OverallPassRate = passRates.Any() ? Math.Round(passRates.Average(), 1) : 0m,
            PendingGradingItems = pendingGrades
        });
    }

    private async Task<BaseResponse> GetClassTeacherNavbar(Guid teacherId, Guid schoolId)
    {
        var myClassroomIds = (await _classroomTeacherQuery.QueryAsync<Guid>($@"
            SELECT ClassroomId FROM ClassroomTeacher
            WHERE TeacherId = '{teacherId}' AND SchoolId = '{schoolId}' AND IsActive = 1",
            new Dictionary<string, object>())).ToList();

        var allSnapshots = await _perfRepo.GetBySchoolAsync(schoolId);

        var filtered = allSnapshots
            .Where(s => s.DocType == "classroom_subject"
                     && myClassroomIds.Contains(s.ClassroomId ?? Guid.Empty))
            .ToList();

        var scores = filtered.Where(s => s.TotalAttempts > 0)
            .Select(s => s.AverageScorePercent).DefaultIfEmpty(0).ToList();
        var passRates = filtered.Where(s => s.TotalAttempts > 0)
            .Select(s => s.PassRate).DefaultIfEmpty(0).ToList();

        var pendingGrades = await CountPendingGrading(schoolId, teacherId);

        return Success(new ClassTeacherNavbarDto
        {
            ClassCount = filtered.Select(s => s.ClassroomId).Distinct().Count(),
            TotalStudents = filtered.Sum(s => s.StudentCount),
            OverallAverageScore = scores.Any() ? Math.Round(scores.Average(), 1) : 0m,
            OverallPassRate = passRates.Any() ? Math.Round(passRates.Average(), 1) : 0m,
            PendingGradingItems = pendingGrades
        });
    }

    private async Task<BaseResponse> GetHeadTeacherNavbar(Guid teacherId, Guid schoolId)
    {
        var myClassroomIds = (await _teacherClassroomQuery.QueryAsync<Guid>($@"
            SELECT ClassroomId FROM TeacherClassroom
            WHERE TeacherId = '{teacherId}' AND SchoolId = '{schoolId}' AND IsActive = 1",
            new Dictionary<string, object>())).ToList();

        var allSnapshots = await _perfRepo.GetBySchoolAsync(schoolId);

        var filtered = allSnapshots
            .Where(s => s.DocType == "classroom_subject"
                     && myClassroomIds.Contains(s.ClassroomId ?? Guid.Empty))
            .ToList();

        var scores = filtered.Where(s => s.TotalAttempts > 0)
            .Select(s => s.AverageScorePercent).DefaultIfEmpty(0).ToList();
        var passRates = filtered.Where(s => s.TotalAttempts > 0)
            .Select(s => s.PassRate).DefaultIfEmpty(0).ToList();

        var pendingGrades = await CountPendingGrading(schoolId, teacherId);

        return Success(new ClassTeacherNavbarDto
        {
            ClassCount = filtered.Select(s => s.ClassroomId).Distinct().Count(),
            TotalStudents = filtered.Sum(s => s.StudentCount),
            OverallAverageScore = scores.Any() ? Math.Round(scores.Average(), 1) : 0m,
            OverallPassRate = passRates.Any() ? Math.Round(passRates.Average(), 1) : 0m,
            PendingGradingItems = pendingGrades
        });
    }

    private async Task<BaseResponse> GetStudentNavbar(Guid studentId, Guid schoolId)
    {
        var snapshots = await _perfRepo.GetByStudentAsync(studentId);
        var snapshot = snapshots.FirstOrDefault();

        var pendingQuizCount = (await _attemptQuery.QueryAsync<int>($@"
            SELECT COUNT(*) FROM QuizAttempt
            WHERE StudentId = '{studentId}'
            AND   SchoolId  = '{schoolId}'
            AND   Status    = 'InProgress'",
            new Dictionary<string, object>())).FirstOrDefault();

        return Success(new StudentNavbarDto
        {
            TotalAttempts = snapshot?.TotalAttempts ?? 0,
            CompletedAttempts = snapshot?.CompletedAttempts ?? 0,
            AverageScorePercent = snapshot?.AverageScorePercent ?? 0,
            PassRate = snapshot?.PassRate ?? 0,
            PendingQuizzes = pendingQuizCount
        });
    }

    private async Task<int> CountPendingGrading(Guid schoolId, Guid? teacherId)
    {
        var teacherFilter = teacherId.HasValue
            ? $"AND lc.CreatedBy = '{teacherId.Value}'"
            : "";

        var result = await _answerQuery.QueryAsync<int>($@"
            SELECT COUNT(DISTINCT qaa.Id)
            FROM QuizAttemptAnswer qaa
            JOIN QuizAttempt qa ON qa.Id = qaa.AttemptId
            JOIN LessonContent lc ON lc.Id = qa.LessonId
            WHERE qaa.ManualMarksObtained IS NULL
            AND   (qaa.TypedAnswer IS NOT NULL OR qaa.BoardSessionId IS NOT NULL)
            AND   qaa.IsSkipped = 0
            AND   qa.SchoolId = '{schoolId}'
            {teacherFilter}",
            new Dictionary<string, object>());

        return result.FirstOrDefault();
    }

    private async Task<BaseResponse> GetSubjectTeacherDashboard(Guid teacherId, Guid schoolId)
    {
        var myClassroomIds = await _teacherClassroomQuery.QueryAsync<Guid>($@"
            SELECT ClassroomId FROM TeacherClassroom
            WHERE TeacherId = '{teacherId}' AND SchoolId = '{schoolId}' AND IsActive = 1",
            new Dictionary<string, object>());

        var mySubjectIds = await _teacherSubjectQuery.QueryAsync<Guid>($@"
            SELECT SubjectId FROM TeacherSubject
            WHERE TeacherId = '{teacherId}' AND SchoolId = '{schoolId}' AND IsActive = 1",
            new Dictionary<string, object>());

        var myClassrooms = myClassroomIds.ToList();
        var mySubjects = mySubjectIds.ToList();

        var allSnapshots = await _perfRepo.GetBySchoolAsync(schoolId);

        var filtered = allSnapshots
            .Where(s => s.DocType == "classroom_subject"
                     && myClassrooms.Contains(s.ClassroomId ?? Guid.Empty)
                     && mySubjects.Contains(s.SubjectId ?? Guid.Empty))
            .ToList();

        var dashboards = filtered.Select(MapToDashboardDto).ToList();

        var totalStudents = filtered.Sum(s => s.StudentCount);
        var scores = filtered.Where(s => s.TotalAttempts > 0)
            .Select(s => s.AverageScorePercent).DefaultIfEmpty(0).ToList();
        var passRates = filtered.Where(s => s.TotalAttempts > 0)
            .Select(s => s.PassRate).DefaultIfEmpty(0).ToList();

        var result = new TeacherPerformanceDashboardDto
        {
            TeacherId = teacherId,
            TeacherName = "Subject Teacher",
            TotalStudents = totalStudents,
            OverallAverageScore = scores.Any() ? Math.Round(scores.Average(), 1) : 0m,
            OverallPassRate = passRates.Any() ? Math.Round(passRates.Average(), 1) : 0m,
            Classrooms = dashboards
        };

        return Success(result);
    }

    private async Task<BaseResponse> GetClassTeacherDashboard(Guid teacherId, Guid schoolId)
    {
        var myClassroomIds = await _classroomTeacherQuery.QueryAsync<Guid>($@"
            SELECT ClassroomId FROM ClassroomTeacher
            WHERE TeacherId = '{teacherId}' AND SchoolId = '{schoolId}' AND IsActive = 1",
            new Dictionary<string, object>());

        var myClassrooms = myClassroomIds.ToList();

        var allSnapshots = await _perfRepo.GetBySchoolAsync(schoolId);

        var filtered = allSnapshots
            .Where(s => s.DocType == "classroom_subject"
                     && myClassrooms.Contains(s.ClassroomId ?? Guid.Empty))
            .ToList();

        var dashboards = filtered.Select(MapToDashboardDto).ToList();

        var totalStudents = filtered.Sum(s => s.StudentCount);
        var scores = filtered.Where(s => s.TotalAttempts > 0)
            .Select(s => s.AverageScorePercent).DefaultIfEmpty(0).ToList();
        var passRates = filtered.Where(s => s.TotalAttempts > 0)
            .Select(s => s.PassRate).DefaultIfEmpty(0).ToList();

        var result = new TeacherPerformanceDashboardDto
        {
            TeacherId = teacherId,
            TeacherName = "Class Teacher",
            TotalStudents = totalStudents,
            OverallAverageScore = scores.Any() ? Math.Round(scores.Average(), 1) : 0m,
            OverallPassRate = passRates.Any() ? Math.Round(passRates.Average(), 1) : 0m,
            Classrooms = dashboards
        };

        return Success(result);
    }

    private async Task<BaseResponse> GetHeadTeacherDashboard(Guid teacherId, Guid schoolId)
    {
        var myClassroomIds = await _teacherClassroomQuery.QueryAsync<Guid>($@"
            SELECT ClassroomId FROM TeacherClassroom
            WHERE TeacherId = '{teacherId}' AND SchoolId = '{schoolId}' AND IsActive = 1",
            new Dictionary<string, object>());

        var myClassrooms = myClassroomIds.ToList();

        var allSnapshots = await _perfRepo.GetBySchoolAsync(schoolId);

        var filtered = allSnapshots
            .Where(s => s.DocType == "classroom_subject"
                     && myClassrooms.Contains(s.ClassroomId ?? Guid.Empty))
            .ToList();

        var dashboards = filtered.Select(MapToDashboardDto).ToList();

        var totalStudents = filtered.Sum(s => s.StudentCount);
        var scores = filtered.Where(s => s.TotalAttempts > 0)
            .Select(s => s.AverageScorePercent).DefaultIfEmpty(0).ToList();
        var passRates = filtered.Where(s => s.TotalAttempts > 0)
            .Select(s => s.PassRate).DefaultIfEmpty(0).ToList();

        var pendingApprovalsCount = (await _approvalQuery.QueryAsync<int>($@"
            SELECT COUNT(*)
            FROM ApprovalRequests
            WHERE ApproverId = '{teacherId}'
            AND   SchoolId   = '{schoolId}'
            AND   Status     = 'Pending'
            AND   ExpiresAt  > GETUTCDATE()",
            new Dictionary<string, object>())).FirstOrDefault();

        var result = new TeacherPerformanceDashboardDto
        {
            TeacherId = teacherId,
            TeacherName = "Head Teacher",
            TotalStudents = totalStudents,
            OverallAverageScore = scores.Any() ? Math.Round(scores.Average(), 1) : 0m,
            OverallPassRate = passRates.Any() ? Math.Round(passRates.Average(), 1) : 0m,
            Classrooms = dashboards,
            PendingApprovalsCount = pendingApprovalsCount
        };

        return Success(result);
    }

    private async Task<BaseResponse> GetStudentDashboard(Guid studentId)
    {
        var snapshots = await _perfRepo.GetByStudentAsync(studentId);

        if (!snapshots.Any())
            return Success(new List<StudentPerformanceDetailDto>());

        var dtos = snapshots.Select(s => new StudentPerformanceDetailDto
        {
            StudentId = s.StudentId ?? Guid.Empty,
            StudentName = s.StudentName ?? "Unknown",
            TotalAttempts = s.TotalAttempts,
            CompletedAttempts = s.CompletedAttempts,
            AverageScorePercent = s.AverageScorePercent,
            PassRate = s.PassRate,
            BestScorePercent = s.AverageScorePercent,
            RecentAttempts = new List<StudentAttemptItemDto>()
        }).ToList();

        return Success(dtos);
    }

    public async Task<BaseResponse> GetSubjectClassroomsAsync(Guid subjectId, AuthenticatedUserClaims claims)
    {
        if (!Guid.TryParse(claims.SchoolId, out var schoolId))
            return Unauthorized();
        if (!Guid.TryParse(claims.UserId, out var userId))
            return Unauthorized();
        if (!Enum.TryParse<UserRole>(claims.Role, ignoreCase: true, out var role))
            return Unauthorized();

        try
        {
            if (!await CanAccessSubject(userId, subjectId, role))
                return Forbidden("You do not have access to this subject");

            var snapshots = await _perfRepo.GetBySubjectAsync(schoolId, subjectId);
            var dtos = snapshots
                .Where(s => s.DocType == "classroom_subject")
                .Select(s => new SubjectClassroomPerformanceDto
                {
                    ClassroomId = s.ClassroomId ?? Guid.Empty,
                    ClassroomName = s.ClassroomName ?? "Unknown",
                    StudentCount = s.StudentCount,
                    TotalAttempts = s.TotalAttempts,
                    CompletedAttempts = s.CompletedAttempts,
                    AverageScorePercent = s.AverageScorePercent,
                    PassRate = s.PassRate,
                    LastActivityDate = s.LastActivityDate,
                    ComputedAt = s.ComputedAt
                })
                .OrderByDescending(d => d.AverageScorePercent)
                .ToList();

            return Success(dtos);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting subject classrooms {SubjectId}", subjectId);
            return Error();
        }
    }

    public async Task<BaseResponse> GetStudentQuizPerformanceAsync(Guid studentId, AuthenticatedUserClaims claims)
    {
        if (!Guid.TryParse(claims.SchoolId, out var schoolId))
            return Unauthorized();
        if (!Guid.TryParse(claims.UserId, out var userId))
            return Unauthorized();
        if (!Enum.TryParse<UserRole>(claims.Role, ignoreCase: true, out var role))
            return Unauthorized();

        try
        {
            if (role is not (UserRole.Administrator or UserRole.SuperAdministrator
                or UserRole.HeadTeacher or UserRole.ClassTeacher or UserRole.SubjectTeacher))
                return Forbidden();

            var snapshot = (await _perfRepo.GetByStudentAsync(studentId))
                .FirstOrDefault(s => s.DocType == "student" && s.SchoolId == schoolId);

            var sql = $@"
                SELECT
                    att.Id               AS AttemptId,
                    att.QuizCode,
                    att.LessonId,
                    lc.Aim               AS LessonTitle,
                    ISNULL(c.Name, '')   AS ClassroomName,
                    ISNULL(s.Subject, '') AS SubjectName,
                    att.AttemptNumber,
                    att.FinalScorePercent,
                    att.IsPassed,
                    att.Status,
                    att.SubmittedAt
                FROM QuizAttempt att
                JOIN LessonContent lc ON lc.Id = att.LessonId
                LEFT JOIN Classroom c ON c.Id = lc.ClassroomId
                LEFT JOIN Subjects s ON s.Id = lc.SubjectId
                WHERE att.StudentId = '{studentId}'
                AND   att.SchoolId  = '{schoolId}'
                AND   att.Status IN ('Submitted','PartiallyGraded','FullyGraded')
                ORDER BY att.SubmittedAt DESC";

            var attempts = (await _attemptQuery.QueryAsync<StudentAttemptItemDto>(sql, new Dictionary<string, object>())).ToList();

            var dto = new StudentPerformanceDetailDto
            {
                StudentId = studentId,
                StudentName = snapshot?.StudentName ?? attempts.FirstOrDefault()?.LessonTitle ?? "Unknown",
                TotalAttempts = snapshot?.TotalAttempts ?? attempts.Count,
                CompletedAttempts = snapshot?.CompletedAttempts ?? attempts.Count(a => a.Status is "Submitted" or "PartiallyGraded" or "FullyGraded"),
                AverageScorePercent = snapshot?.AverageScorePercent ?? (attempts.Any(a => a.FinalScorePercent > 0) ? Math.Round(attempts.Where(a => a.FinalScorePercent > 0).Average(a => a.FinalScorePercent), 1) : 0m),
                PassRate = snapshot?.PassRate ?? (attempts.Any() ? Math.Round((decimal)attempts.Count(a => a.IsPassed == true) / attempts.Count * 100, 1) : 0m),
                BestScorePercent = attempts.Any() ? attempts.Max(a => a.FinalScorePercent) : 0m,
                RecentAttempts = attempts
            };

            return Success(dto);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting quiz performance for student {StudentId}", studentId);
            return Error();
        }
    }

    public async Task<BaseResponse> GetSubjectTopicsAsync(Guid subjectId, Guid? classroomId, AuthenticatedUserClaims claims)
    {
        if (!Guid.TryParse(claims.SchoolId, out var schoolId))
            return Unauthorized();
        if (!Guid.TryParse(claims.UserId, out var userId))
            return Unauthorized();
        if (!Enum.TryParse<UserRole>(claims.Role, ignoreCase: true, out var role))
            return Unauthorized();

        try
        {
            if (!await CanAccessSubject(userId, subjectId, role))
                return Forbidden("You do not have access to this subject");

            List<PerformanceSnapshot> topicSnapshots;
            List<PerformanceSnapshot> subTopicSnapshots;

            if (classroomId.HasValue)
            {
                topicSnapshots = await _perfRepo.GetByClassroomSubjectTopicAsync(schoolId, classroomId.Value, subjectId);
                subTopicSnapshots = await _perfRepo.GetByClassroomSubjectSubTopicAsync(schoolId, classroomId.Value, subjectId);
            }
            else
            {
                topicSnapshots = await _perfRepo.GetBySubjectTopicAsync(schoolId, subjectId);
                subTopicSnapshots = await _perfRepo.GetBySubjectSubTopicAsync(schoolId, subjectId);
            }

            var subTopicLookup = subTopicSnapshots
                .GroupBy(s => s.TopicId)
                .ToDictionary(
                    g => g.Key ?? Guid.Empty,
                    g => g.Select(s => new SubjectSubTopicPerformanceDto
                    {
                        SubTopicName = s.SubTopicName ?? "Unknown",
                        StudentCount = s.StudentCount,
                        TotalAttempts = s.TotalAttempts,
                        CompletedAttempts = s.CompletedAttempts,
                        AverageScorePercent = s.AverageScorePercent,
                        PassRate = s.PassRate
                    }).ToList()
                );

            var dtos = topicSnapshots
                .GroupBy(s => new { s.TopicId, s.TopicName })
                .Select(g =>
                {
                    var first = g.First();
                    return new SubjectTopicPerformanceDto
                    {
                        TopicId = g.Key.TopicId ?? Guid.Empty,
                        TopicName = g.Key.TopicName ?? "Unknown",
                        ClassroomId = first.ClassroomId,
                        ClassroomName = first.ClassroomName,
                        StudentCount = g.Sum(s => s.StudentCount),
                        TotalAttempts = g.Sum(s => s.TotalAttempts),
                        CompletedAttempts = g.Sum(s => s.CompletedAttempts),
                        AverageScorePercent = g.Where(s => s.TotalAttempts > 0)
                            .Select(s => s.AverageScorePercent).DefaultIfEmpty(0).Average(),
                        PassRate = g.Where(s => s.TotalAttempts > 0)
                            .Select(s => s.PassRate).DefaultIfEmpty(0).Average(),
                        ComputedAt = g.Max(s => s.ComputedAt),
                        SubTopics = subTopicLookup.GetValueOrDefault(g.Key.TopicId ?? Guid.Empty, new())
                    };
                })
                .OrderByDescending(d => d.AverageScorePercent)
                .ToList();

            return Success(dtos);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting subject topics {SubjectId}", subjectId);
            return Error();
        }
    }

    private async Task<bool> CanAccessClassroom(Guid userId, Guid classroomId, UserRole role)
    {
        if (role is UserRole.Administrator or UserRole.SuperAdministrator or UserRole.HeadTeacher)
            return true;

        if (role == UserRole.SubjectTeacher)
        {
            var rows = await _teacherClassroomQuery.QueryAsync<int>($@"
                SELECT TOP 1 1 FROM TeacherClassroom
                WHERE TeacherId = '{userId}' AND ClassroomId = '{classroomId}' AND IsActive = 1",
                new Dictionary<string, object>());
            return rows.Any();
        }

        if (role == UserRole.ClassTeacher)
        {
            var rows = await _classroomTeacherQuery.QueryAsync<int>($@"
                SELECT TOP 1 1 FROM ClassroomTeacher
                WHERE TeacherId = '{userId}' AND ClassroomId = '{classroomId}' AND IsActive = 1",
                new Dictionary<string, object>());
            return rows.Any();
        }

        return false;
    }

    private async Task<bool> CanAccessSubject(Guid userId, Guid subjectId, UserRole role)
    {
        if (role is UserRole.Administrator or UserRole.SuperAdministrator or UserRole.HeadTeacher)
            return true;

        if (role == UserRole.SubjectTeacher)
        {
            var rows = await _teacherSubjectQuery.QueryAsync<int>($@"
                SELECT TOP 1 1 FROM TeacherSubject
                WHERE TeacherId = '{userId}' AND SubjectId = '{subjectId}' AND IsActive = 1",
                new Dictionary<string, object>());
            return rows.Any();
        }

        return false;
    }

    private static PerformanceDashboardDto MapToDashboardDto(PerformanceSnapshot s)
    {
        return new PerformanceDashboardDto
        {
            ClassroomId = s.ClassroomId,
            ClassroomName = s.ClassroomName,
            SubjectId = s.SubjectId,
            SubjectName = s.SubjectName,
            StudentCount = s.StudentCount,
            TotalAttempts = s.TotalAttempts,
            CompletedAttempts = s.CompletedAttempts,
            AverageScorePercent = s.AverageScorePercent,
            PassRate = s.PassRate,
            AverageTimeTakenSeconds = s.AverageTimeTakenSeconds,
            LastActivityDate = s.LastActivityDate,
            ComputedAt = s.ComputedAt,
            QuizBreakdown = s.QuizBreakdown.Select(q => new QuizPerformanceItemDto
            {
                QuizCode = q.QuizCode,
                LessonTitle = q.LessonTitle,
                LessonId = q.LessonId,
                AttemptCount = q.AttemptCount,
                AverageScore = q.AverageScore,
                PassRate = q.PassRate
            }).ToList()
        };
    }

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

    private static BaseResponse Error() => new()
    {
        ResponseCode = ResponseCode.ErrorOccured,
        ResponseMessage = "An error occurred",
        Status = "failed"
    };

}
