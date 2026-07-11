using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Context;
using System;
using System.Linq;
using System.Threading.Tasks;
using TechHub.Core;
using TechHub.Core.Model;
using TechHub.Core.ViewModel.classroom;
using TechHub.Service.Interface;

namespace TechHub.Service.Service;

public class StudentDashboardService : IStudentDashboardService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger _logger;
    private readonly string _connString;

    public StudentDashboardService(IConfiguration configuration, ILogger logger)
    {
        _configuration = configuration;
        _logger = logger;
        _connString = _configuration.GetConnectionString("DbConnectionString") ?? string.Empty;
    }

    private BaseResponse Ok(string message, object data = null) => new()
    {
        ResponseCode = ResponseCode.successful,
        ResponseMessage = message,
        Status = "successful",
        Data = data
    };

    private BaseResponse Bad(string message, string code = ResponseCode.BadRequest) => new()
    {
        ResponseCode = code,
        ResponseMessage = message,
        Status = "failed",
        Data = null
    };

    public async Task<BaseResponse> GetStudentSummaryAsync(AuthenticatedUserClaims claims)
    {
        using (LogContext.PushProperty("RequestedBy", claims.UserId))
        {
            try
            {
                if (!Guid.TryParse(claims.UserId, out var studentId))
                    return Bad("Invalid authentication", ResponseCode.Unauthorized);
                if (!Guid.TryParse(claims.SchoolId, out var schoolId))
                    return Bad("Invalid authentication", ResponseCode.Unauthorized);

                using var conn = new SqlConnection(_connString);
                conn.Open();

                // 1. Unattempted assessments
                var unattemptedAssessments = (await conn.QueryAsync<UnattemptedAssessmentDto>($@"
                    SELECT DISTINCT
                        a.Id       AS AssessmentId,
                        a.Code,
                        a.Title
                    FROM Assessment a
                    JOIN AssessmentAssignment aa ON aa.AssessmentId = a.Id AND aa.IsActive = 1
                    LEFT JOIN AssessmentAttempt at2 ON at2.AssessmentId = a.Id
                        AND at2.StudentId = '{studentId}'
                        AND at2.SchoolId  = '{schoolId}'
                        AND at2.Status IN ('Submitted','PartiallyGraded','FullyGraded')
                    WHERE a.SchoolId = '{schoolId}'
                    AND   a.IsActive = 1
                    AND   (
                        aa.TargetType = 'Student'   AND aa.TargetId = '{studentId}'
                        OR aa.TargetType = 'Subject' AND aa.TargetId IN (
                            SELECT SubjectId FROM TeacherSubject WHERE TeacherId = '{studentId}' AND IsActive = 1
                        )
                        OR aa.TargetType = 'Classroom' AND aa.TargetId IN (
                            SELECT ClassroomId FROM StudentClassroom WHERE StudentId = '{studentId}'
                        )
                    )
                    AND at2.Id IS NULL")).ToList();

                // 2. Unattempted quizzes (lessons with QuizCode that have no completed quiz attempt)
                var unattemptedQuizzes = (await conn.QueryAsync<UnattemptedQuizDto>($@"
                    SELECT
                        lc.Id         AS LessonId,
                        lc.Aim        AS LessonTitle,
                        lc.QuizCode,
                        s.Name        AS SubjectName
                    FROM LessonContent lc
                    JOIN Subjects s ON s.Id = lc.SubjectId
                    WHERE lc.ClassroomId IN (
                        SELECT ClassroomId FROM StudentClassroom WHERE StudentId = '{studentId}'
                    )
                    AND lc.SchoolId    = '{schoolId}'
                    AND lc.Status      = 'Published'
                    AND lc.QuizCode IS NOT NULL
                    AND NOT EXISTS (
                        SELECT 1 FROM QuizAttempt qa
                        WHERE qa.QuizCode   = lc.QuizCode
                        AND   qa.StudentId  = '{studentId}'
                        AND   qa.SchoolId   = '{schoolId}'
                        AND   qa.Status IN ('Submitted','PartiallyGraded','FullyGraded')
                    )")).ToList();

                // 3. Unwatched lessons
                var unwatchedLessons = (await conn.QueryAsync<UnwatchedLessonDto>($@"
                    SELECT
                        lc.Id         AS LessonId,
                        lc.Aim        AS LessonTitle,
                        s.Name        AS SubjectName
                    FROM LessonContent lc
                    JOIN Subjects s ON s.Id = lc.SubjectId
                    WHERE lc.ClassroomId IN (
                        SELECT ClassroomId FROM StudentClassroom WHERE StudentId = '{studentId}'
                    )
                    AND lc.SchoolId = '{schoolId}'
                    AND lc.Status   = 'Published'
                    AND NOT EXISTS (
                        SELECT 1 FROM StudentLessonProgress
                        WHERE LessonId  = lc.Id
                        AND   StudentId = '{studentId}'
                        AND   SchoolId  = '{schoolId}'
                    )")).ToList();

                var dto = new StudentSummaryDto
                {
                    UnattemptedAssessments = unattemptedAssessments,
                    UnattemptedQuizzes = unattemptedQuizzes,
                    UnwatchedLessons = unwatchedLessons,
                    Counts = new SummaryCounts
                    {
                        UnattemptedAssessments = unattemptedAssessments.Count,
                        UnattemptedQuizzes = unattemptedQuizzes.Count,
                        UnwatchedLessons = unwatchedLessons.Count
                    }
                };

                return Ok("Student summary retrieved", dto);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error fetching student summary");
                return Bad("An error occurred while fetching student summary", ResponseCode.ErrorOccured);
            }
        }
    }

    public async Task<BaseResponse> MarkLessonAsWatchedAsync(Guid lessonId, AuthenticatedUserClaims claims)
    {
        using (LogContext.PushProperty("RequestedBy", claims.UserId))
        {
            try
            {
                if (!Guid.TryParse(claims.UserId, out var studentId))
                    return Bad("Invalid authentication", ResponseCode.Unauthorized);
                if (!Guid.TryParse(claims.SchoolId, out var schoolId))
                    return Bad("Invalid authentication", ResponseCode.Unauthorized);

                var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

                using var conn = new SqlConnection(_connString);
                conn.Open();

                var existing = await conn.QueryFirstOrDefaultAsync<Guid?>($@"
                    SELECT TOP 1 Id FROM StudentLessonProgress
                    WHERE LessonId  = '{lessonId}'
                    AND   StudentId = '{studentId}'
                    AND   SchoolId  = '{schoolId}'");

                if (existing is not null)
                    return Ok("Lesson already marked as watched");

                await conn.ExecuteAsync($@"
                    INSERT INTO StudentLessonProgress (Id, StudentId, LessonId, SchoolId, WatchedAt, CreationDate)
                    VALUES ('{Guid.NewGuid()}', '{studentId}', '{lessonId}', '{schoolId}', '{now}', '{now}')");

                _logger.Information(
                    "Lesson {LessonId} marked as watched by student {StudentId}",
                    lessonId, studentId);

                return Ok("Lesson marked as watched");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error marking lesson as watched");
                return Bad("An error occurred while marking lesson", ResponseCode.ErrorOccured);
            }
        }
    }
}
