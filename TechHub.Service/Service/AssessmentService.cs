using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Context;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TechHub.Core;
using TechHub.Core.Entities;
using TechHub.Core.Enum;
using TechHub.Core.Model;
using TechHub.Core.ViewModel.classroom;
using TechHub.Service.Interface;
using TechHub.Service.Service.DatabaseService;

namespace TechHub.Service.Service;

public class AssessmentService : IAssessmentService
{
    private readonly ICommandRespository<Assessments> _assessmentCommand;
    private readonly ICommandRespository<AssessmentConfig> _configCommand;
    private readonly ICommandRespository<AssessmentQuestion> _questionCommand;
    private readonly ICommandRespository<AssessmentAssignment> _assignmentCommand;
    private readonly ICommandRespository<AssessmentAttempt> _attemptCommand;
    private readonly ICommandRespository<AssessmentAttemptAnswer> _answerCommand;
    private readonly ICommandRespository<AssessmentAttemptAnswerBoard> _answerBoardCommand;
    private readonly IQueryRepository<Assessments> _assessmentQuery;
    private readonly IQueryRepository<AssessmentConfig> _configQuery;
    private readonly IQueryRepository<AssessmentAttempt> _attemptQuery;
    private readonly IQueryRepository<AssessmentAttemptAnswer> _answerQuery;
    private readonly IDbTransactionScopeFactory _dbTransactionScopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger _logger;
    private readonly string _connString;

    public AssessmentService(
        ICommandRespository<Assessments> assessmentCommand,
        ICommandRespository<AssessmentConfig> configCommand,
        ICommandRespository<AssessmentQuestion> questionCommand,
        ICommandRespository<AssessmentAssignment> assignmentCommand,
        ICommandRespository<AssessmentAttempt> attemptCommand,
        ICommandRespository<AssessmentAttemptAnswer> answerCommand,
        ICommandRespository<AssessmentAttemptAnswerBoard> answerBoardCommand,
        IQueryRepository<Assessments> assessmentQuery,
        IQueryRepository<AssessmentConfig> configQuery,
        IQueryRepository<AssessmentAttempt> attemptQuery,
        IQueryRepository<AssessmentAttemptAnswer> answerQuery,
        IDbTransactionScopeFactory dbTransactionScopeFactory,
        IConfiguration configuration,
        ILogger logger)
    {
        _assessmentCommand = assessmentCommand;
        _configCommand = configCommand;
        _questionCommand = questionCommand;
        _assignmentCommand = assignmentCommand;
        _attemptCommand = attemptCommand;
        _answerCommand = answerCommand;
        _answerBoardCommand = answerBoardCommand;
        _assessmentQuery = assessmentQuery;
        _configQuery = configQuery;
        _attemptQuery = attemptQuery;
        _answerQuery = answerQuery;
        _dbTransactionScopeFactory = dbTransactionScopeFactory;
        _configuration = configuration;
        _logger = logger;
        _connString = _configuration.GetConnectionString("DbConnectionString") ?? string.Empty;
    }

    private static BaseResponse Ok(string message, object? data = null) => new()
    {
        ResponseCode = ResponseCode.successful,
        ResponseMessage = message,
        Status = "successful",
        Data = data
    };

    private static BaseResponse Bad(string message, string code = ResponseCode.BadRequest) => new()
    {
        ResponseCode = code,
        ResponseMessage = message,
        Status = "failed",
        Data = null
    };

    public async Task<BaseResponse> CreateAssessment(CreateAssessmentViewModel model, AuthenticatedUserClaims claims)
    {
        using (LogContext.PushProperty("RequestedBy", claims.UserId))
        {
            try
            {
                if (model is null)
                    return Bad("Assessment data is required");

                if (!Guid.TryParse(claims.UserId, out var teacherId))
                    return Bad("Invalid authentication", ResponseCode.Unauthorized);
                if (!Guid.TryParse(claims.SchoolId, out var schoolId))
                    return Bad("Invalid authentication", ResponseCode.Unauthorized);

                var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
                var assessmentId = Guid.NewGuid();

                // Generate unique code
                string code;
                int retries = 0;
                do
                {
                    code = QuizService.QuizCodeGenerator.Generate("AS");
                    retries++;

                    var existing = await _assessmentQuery.SelectByColumns(
                        "SELECT TOP 1 Id FROM Assessments WHERE Code = @Code",
                        new Dictionary<string, object> { { "Code", code } });

                    if (existing is null) break;
                    if (retries > 10)
                        return Bad("Could not generate unique assessment code. Please try again.");
                }
                while (true);

                using var scope = _dbTransactionScopeFactory.Create("DbConnectionString");
                try
                {
                    await _assessmentCommand.Create(scope.Transaction, scope.Connection, new Dictionary<string, object>
                    {
                        { "Id", assessmentId },
                        { "Code", code },
                        { "Title", model.Title.Trim() },
                        { "Description", model.Description?.Trim() ?? (object)DBNull.Value },
                        { "SchoolId", schoolId },
                        { "CreatedByUserId", teacherId },
                        { "CreatedAt", now },
                        { "UpdatedAt", now }
                    });

                    var configId = Guid.NewGuid();
                    await _configCommand.Create(scope.Transaction, scope.Connection, new Dictionary<string, object>
                    {
                        { "Id", configId },
                        { "AssessmentId", assessmentId },
                        { "TimeLimitMinutes", model.TimeLimitMinutes },
                        { "ShuffleQuestions", model.ShuffleQuestions },
                        { "PassMarkPercent", model.PassMarkPercent },
                        { "ShowResultImmediately", model.ShowResultImmediately },
                        { "ShowCorrectAnswers", model.ShowCorrectAnswers },
                        { "ExpiresAt", (object?)model.ExpiresAt ?? DBNull.Value },
                        { "EasyMarks", model.EasyMarks },
                        { "MediumMarks", model.MediumMarks },
                        { "HardMarks", model.HardMarks },
                        { "ExamLevelMarks", model.ExamLevelMarks },
                        { "CreatedBy", teacherId },
                        { "CreationDate", now },
                        { "ModifiedDate", now },
                        { "IsActive", true }
                    });

                    if (model.QuestionIds.Any())
                    {
                        var distinctQuestionIds = model.QuestionIds.Distinct().ToList();
                        var questions = distinctQuestionIds.Select((qid, idx) => new Dictionary<string, object>
                        {
                            { "Id", Guid.NewGuid() },
                            { "AssessmentId", assessmentId },
                            { "QuestionId", qid },
                            { "SchoolId", schoolId },
                            { "DisplayOrder", idx + 1 },
                            { "CreatedAt", now },
                            { "IsActive", true }
                        }).ToList();

                        await _questionCommand.CreateBatchAsync(scope.Transaction, scope.Connection, questions);
                    }

                    await scope.CommitAsync();
                }
                catch (Exception ex)
                {
                    try { await scope.RollbackAsync(); } catch { }
                    throw;
                }

                _logger.Information(
                    "Assessment created - Id: {Id}, Code: {Code}, Title: {Title}",
                    assessmentId, code, model.Title);

                return Ok("Assessment created successfully", new
                {
                    AssessmentId = assessmentId,
                    Code = code,
                    Title = model.Title,
                    QuestionCount = model.QuestionIds.Distinct().Count()
                });
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error creating assessment");
                return Bad("An error occurred while creating assessment", ResponseCode.ErrorOccured);
            }
        }
    }

    public async Task<BaseResponse> AssignAssessment(AssignAssessmentViewModel model, AuthenticatedUserClaims claims)
    {
        using (LogContext.PushProperty("RequestedBy", claims.UserId))
        {
            try
            {
                if (!Guid.TryParse(claims.UserId, out var userId))
                    return Bad("Invalid authentication", ResponseCode.Unauthorized);
                if (!Guid.TryParse(claims.SchoolId, out var schoolId))
                    return Bad("Invalid authentication", ResponseCode.Unauthorized);

                if (model.TargetType != "Student" && model.TargetType != "Subject" && model.TargetType != "Classroom")
                    return Bad("TargetType must be Student, Subject, or Classroom");

                if (!model.TargetIds.Any())
                    return Bad("At least one target ID is required");

                var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

                var assignments = model.TargetIds.Select(targetId => new Dictionary<string, object>
                {
                    { "Id", Guid.NewGuid() },
                    { "AssessmentId", model.AssessmentId },
                    { "TargetType", model.TargetType },
                    { "TargetId", targetId },
                    { "SchoolId", schoolId },
                    { "CreatedBy", userId },
                    { "CreationDate", now },
                    { "IsActive", true }
                }).ToList();

                using var scope = _dbTransactionScopeFactory.Create("DbConnectionString");
                try
                {
                    await _assignmentCommand.CreateBatchAsync(scope.Transaction, scope.Connection, assignments);
                    await scope.CommitAsync();
                }
                catch
                {
                    try { await scope.RollbackAsync(); } catch { }
                    throw;
                }

                return Ok($"Assessment assigned to {assignments.Count} {model.TargetType}(s)");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error assigning assessment");
                return Bad("An error occurred while assigning assessment", ResponseCode.ErrorOccured);
            }
        }
    }

    public async Task<BaseResponse> GetStudentAssessments(AuthenticatedUserClaims claims)
    {
        using (LogContext.PushProperty("RequestedBy", claims.UserId))
        {
            try
            {
                if (!Guid.TryParse(claims.UserId, out var studentId))
                    return Bad("Invalid authentication", ResponseCode.Unauthorized);
                if (!Guid.TryParse(claims.SchoolId, out var schoolId))
                    return Bad("Invalid authentication", ResponseCode.Unauthorized);

                var sql = "SELECT DISTINCT " +
                    "a.Id AS AssessmentId, " +
                    "a.Code, " +
                    "a.Title, " +
                    "a.Description, " +
                    "ac.TimeLimitMinutes, " +
                    "(SELECT COUNT(*) FROM AssessmentQuestion WHERE AssessmentId = a.Id AND IsActive = 1) AS QuestionCount, " +
                    "at2.AttemptNumber, " +
                    "at2.FinalScorePercent, " +
                    "at2.IsPassed, " +
                    "CASE WHEN at2.Id IS NULL THEN 'NotStarted' " +
                    "     WHEN at2.Status = 'InProgress' THEN 'InProgress' " +
                    "     ELSE 'Completed' END AS Status " +
                    "FROM Assessments a " +
                    "JOIN AssessmentConfig ac ON ac.AssessmentId = a.Id AND ac.IsActive = 1 " +
                    "JOIN AssessmentAssignment aa ON aa.AssessmentId = a.Id AND aa.IsActive = 1 " +
                    "LEFT JOIN ( " +
                    "    SELECT AssessmentId, AttemptNumber, FinalScorePercent, IsPassed, Status, Id, " +
                    "           ROW_NUMBER() OVER (PARTITION BY AssessmentId ORDER BY AttemptNumber DESC) AS rn " +
                    "    FROM AssessmentAttempt " +
                    "    WHERE StudentId = @StudentId AND SchoolId = @SchoolId " +
                    ") at2 ON at2.AssessmentId = a.Id AND at2.rn = 1 " +
                    "WHERE a.SchoolId = @SchoolId " +
                    "AND ( " +
                    "    aa.TargetType = 'Student' AND aa.TargetId = @StudentId " +
                    "    OR aa.TargetType = 'Subject' AND aa.TargetId IN ( " +
                    "        SELECT cs.SubjectId " +
                    "        FROM StudentClassroom sc " +
                    "        JOIN ClassroomSubject cs ON cs.ClassroomId = sc.ClassroomId " +
                    "        WHERE sc.StudentId = @StudentId AND sc.IsActive = 1 AND cs.IsActive = 1 " +
                    "        UNION " +
                    "        SELECT sms.SubjectId " +
                    "        FROM StudentMinorSubject sms " +
                    "        WHERE sms.StudentId = @StudentId AND sms.IsActive = 1 " +
                    "    ) " +
                    "    OR aa.TargetType = 'Classroom' AND aa.TargetId IN ( " +
                    "        SELECT ClassroomId FROM StudentClassroom WHERE StudentId = @StudentId AND IsActive = 1 " +
                    "    ) " +
                    ") " +
                    "ORDER BY a.Title ASC";

                var parameters = new Dictionary<string, object>
                {
                    { "StudentId", studentId },
                    { "SchoolId", schoolId }
                };

                var rows = await _assessmentQuery.QueryAsync<AssessmentListItemDto>(sql, parameters);

                return Ok($"{rows.Count()} assessment(s) found", rows.ToList());
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error fetching student assessments");
                return Bad("An error occurred while fetching assessments", ResponseCode.ErrorOccured);
            }
        }
    }

    public async Task<BaseResponse> GetAssessmentDetailByCode(string code, AuthenticatedUserClaims claims)
    {
        using (LogContext.PushProperty("RequestedBy", claims.UserId))
        {
            try
            {
                if (!Guid.TryParse(claims.SchoolId, out var schoolId))
                    return Bad("Invalid authentication", ResponseCode.Unauthorized);

                if (string.IsNullOrWhiteSpace(code))
                    return Bad("Assessment code is required");

                var assessmentSql = "SELECT TOP 1 a.Id, a.Code, a.Title, a.Description, " +
                    "ac.TimeLimitMinutes, ac.PassMarkPercent, ac.ShuffleQuestions, ac.ShowResultImmediately, " +
                    "ac.ShowCorrectAnswers, ac.ExpiresAt " +
                    "FROM Assessments a " +
                    "JOIN AssessmentConfig ac ON ac.AssessmentId = a.Id AND ac.IsActive = 1 " +
                    "WHERE a.Code = @Code AND a.SchoolId = @SchoolId";

                var assessmentParams = new Dictionary<string, object>
                {
                    { "Code", code.Trim() },
                    { "SchoolId", schoolId }
                };

                var assessment = await _assessmentQuery.SelectByColumns(assessmentSql, assessmentParams);
                if (assessment is null)
                    return Bad("Assessment not found", ResponseCode.NotFound);

                using var conn = new SqlConnection(_connString);
                conn.Open();

                var questionSql = "SELECT q.Id AS QuestionId, q.Title, q.TextContent, q.QuestionType, " +
                    "q.DifficultyLevel, q.MarksAllocation, aq.DisplayOrder " +
                    "FROM AssessmentQuestion aq " +
                    "JOIN Questions q ON q.Id = aq.QuestionId " +
                    "WHERE aq.AssessmentId = @AssessmentId " +
                    "AND aq.SchoolId = @SchoolId " +
                    "AND aq.IsActive = 1 " +
                    "ORDER BY aq.DisplayOrder ASC";

                var questionRows = await conn.QueryAsync<AssessmentQuestionDto>(
                    questionSql, new { AssessmentId = assessment.Id, SchoolId = schoolId });

                var questionList = questionRows.ToList();

                if (questionList.Any())
                {
                    var qIds = questionList.Select(q => q.QuestionId).ToList();
                    var optionSql = "SELECT QuestionId, Id AS OptionId, OptionLabel, OptionText " +
                        "FROM QuestionOptions " +
                        "WHERE QuestionId IN @QuestionIds";

                    var options = await conn.QueryAsync<AssessmentOptionDto>(
                        optionSql, new { QuestionIds = qIds });

                    var optionsLookup = options.GroupBy(o => o.QuestionId)
                        .ToDictionary(g => g.Key, g => g.ToList());

                    foreach (var q in questionList)
                    {
                        if (optionsLookup.TryGetValue(q.QuestionId, out var opts))
                            q.Options = opts;
                    }
                }

                var configSql = "SELECT TOP 1 * FROM AssessmentConfig WHERE AssessmentId = @AssessmentId AND IsActive = 1";
                var config = await _configQuery.SelectByColumns(configSql, new Dictionary<string, object> { { "AssessmentId", assessment.Id } });

                var dto = new AssessmentDetailDto
                {
                    AssessmentId = assessment.Id,
                    Code = assessment.Code,
                    Title = assessment.Title,
                    Description = assessment.Description,
                    TimeLimitMinutes = config?.TimeLimitMinutes ?? 0,
                    PassMarkPercent = config?.PassMarkPercent ?? 50,
                    ShuffleQuestions = config?.ShuffleQuestions ?? false,
                    ShowResultImmediately = config?.ShowResultImmediately ?? true,
                    ShowCorrectAnswers = config?.ShowCorrectAnswers ?? false,
                    ExpiresAt = config?.ExpiresAt,
                    QuestionCount = questionList.Count,
                    TotalMarks = (int)questionList.Sum(q => q.MarksAllocation),
                    Questions = questionList
                };

                return Ok("Assessment detail retrieved", dto);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error fetching assessment detail by code - Code: {Code}", code);
                return Bad("An error occurred while fetching assessment detail", ResponseCode.ErrorOccured);
            }
        }
    }

    public async Task<BaseResponse> GetAssessmentDetail(Guid assessmentId, AuthenticatedUserClaims claims)
    {
        using (LogContext.PushProperty("RequestedBy", claims.UserId))
        {
            try
            {
                if (!Guid.TryParse(claims.SchoolId, out var schoolId))
                    return Bad("Invalid authentication", ResponseCode.Unauthorized);

                var assessmentSql = "SELECT TOP 1 a.Id, a.Code, a.Title, a.Description, " +
                    "ac.TimeLimitMinutes, ac.PassMarkPercent, ac.ShuffleQuestions, ac.ShowResultImmediately, " +
                    "ac.ShowCorrectAnswers, ac.ExpiresAt " +
                    "FROM Assessments a " +
                    "JOIN AssessmentConfig ac ON ac.AssessmentId = a.Id AND ac.IsActive = 1 " +
                    "WHERE a.Id = @AssessmentId AND a.SchoolId = @SchoolId";

                var assessmentParams = new Dictionary<string, object>
                {
                    { "AssessmentId", assessmentId },
                    { "SchoolId", schoolId }
                };

                var assessment = await _assessmentQuery.SelectByColumns(assessmentSql, assessmentParams);
                if (assessment is null)
                    return Bad("Assessment not found", ResponseCode.NotFound);

                using var conn = new SqlConnection(_connString);
                conn.Open();

                var questionSql = "SELECT q.Id AS QuestionId, q.Title, q.TextContent, q.QuestionType, " +
                    "q.DifficultyLevel, q.MarksAllocation, aq.DisplayOrder, " +
                    "q.ImageUrl, q.SnapshotUrl, q.BoardSessionId, q.HasBoardSession, q.HasMedia, q.HasAudio " +
                    "FROM AssessmentQuestion aq " +
                    "JOIN Questions q ON q.Id = aq.QuestionId " +
                    "WHERE aq.AssessmentId = @AssessmentId " +
                    "AND aq.SchoolId = @SchoolId " +
                    "AND aq.IsActive = 1 " +
                    "ORDER BY aq.DisplayOrder ASC";

                var questionRows = await conn.QueryAsync<AssessmentQuestionDto>(
                    questionSql, new { AssessmentId = assessmentId, SchoolId = schoolId });

                var questionList = questionRows.ToList();

                // Fetch options for objective questions
                if (questionList.Any())
                {
                    var qIds = questionList.Select(q => q.QuestionId).ToList();
                    var optionSql = "SELECT QuestionId, Id AS OptionId, OptionLabel, OptionText " +
                        "FROM QuestionOptions " +
                        "WHERE QuestionId IN @QuestionIds";

                    var options = await conn.QueryAsync<AssessmentOptionDto>(
                        optionSql, new { QuestionIds = qIds });

                    var optionsLookup = options.GroupBy(o => o.QuestionId)
                        .ToDictionary(g => g.Key, g => g.ToList());

                    foreach (var q in questionList)
                    {
                        if (optionsLookup.TryGetValue(q.QuestionId, out var opts))
                            q.Options = opts;
                    }
                }

                var configSql = "SELECT TOP 1 * FROM AssessmentConfig WHERE AssessmentId = @AssessmentId AND IsActive = 1";
                var config = await _configQuery.SelectByColumns(configSql, new Dictionary<string, object> { { "AssessmentId", assessmentId } });

                var dto = new AssessmentDetailDto
                {
                    AssessmentId = assessment.Id,
                    Code = assessment.Code,
                    Title = assessment.Title,
                    Description = assessment.Description,
                    TimeLimitMinutes = config?.TimeLimitMinutes ?? 0,
                    PassMarkPercent = config?.PassMarkPercent ?? 50,
                    ShuffleQuestions = config?.ShuffleQuestions ?? false,
                    ShowResultImmediately = config?.ShowResultImmediately ?? true,
                    ShowCorrectAnswers = config?.ShowCorrectAnswers ?? false,
                    ExpiresAt = config?.ExpiresAt,
                    QuestionCount = questionList.Count,
                    TotalMarks = (int)questionList.Sum(q => q.MarksAllocation),
                    Questions = questionList
                };

                return Ok("Assessment detail retrieved", dto);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error fetching assessment detail");
                return Bad("An error occurred while fetching assessment detail", ResponseCode.ErrorOccured);
            }
        }
    }

    public async Task<BaseResponse> StartAttempt(Guid assessmentId, AuthenticatedUserClaims claims)
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

                // Check if there is already an InProgress attempt
                var inProgressSql = "SELECT TOP 1 Id FROM AssessmentAttempt " +
                    "WHERE AssessmentId = @AssessmentId " +
                    "AND StudentId = @StudentId " +
                    "AND SchoolId = @SchoolId " +
                    "AND Status = 'InProgress'";

                var inProgress = await _attemptQuery.SelectByColumns(inProgressSql, new Dictionary<string, object>
                {
                    { "AssessmentId", assessmentId },
                    { "StudentId", studentId },
                    { "SchoolId", schoolId }
                });

                if (inProgress is not null)
                    return Ok("You have an active attempt. Resume it.", new { AttemptId = inProgress.Id, Resume = true });

                // Count existing attempts
                var countSql = "SELECT COUNT(*) FROM AssessmentAttempt " +
                    "WHERE AssessmentId = @AssessmentId " +
                    "AND StudentId = @StudentId " +
                    "AND SchoolId = @SchoolId " +
                    "AND Status IN ('Submitted','PartiallyGraded','FullyGraded')";

                var countResult = await _attemptQuery.QueryAsync<int>(countSql, new Dictionary<string, object>
                {
                    { "AssessmentId", assessmentId },
                    { "StudentId", studentId },
                    { "SchoolId", schoolId }
                });

                int existingAttempts = countResult.FirstOrDefault();
                int attemptNumber = existingAttempts + 1;
                bool isOfficial = existingAttempts == 0;

                // Verify assessment exists and get config
                var configSql = "SELECT TOP 1 TimeLimitMinutes, ShuffleQuestions FROM AssessmentConfig " +
                    "WHERE AssessmentId = @AssessmentId AND IsActive = 1";

                var config = await _configQuery.SelectByColumns(configSql, new Dictionary<string, object>
                {
                    { "AssessmentId", assessmentId }
                });

                if (config is null)
                    return Bad("Assessment configuration not found", ResponseCode.NotFound);

                // Create attempt
                var attemptId = Guid.NewGuid();
                await _attemptCommand.Create(new Dictionary<string, object>
                {
                    { "Id", attemptId },
                    { "AssessmentId", assessmentId },
                    { "StudentId", studentId },
                    { "SchoolId", schoolId },
                    { "AttemptNumber", attemptNumber },
                    { "IsOfficial", isOfficial },
                    { "AutoMarksObtained", 0m },
                    { "ManualMarksObtained", 0m },
                    { "TotalMarks", 0m },
                    { "Status", "InProgress" },
                    { "StartedAt", now },
                    { "CreationDate", now },
                    { "ModifiedDate", now }
                });

                // Fetch questions
                using var conn = new SqlConnection(_connString);
                conn.Open();

                var shuffle = config.ShuffleQuestions;

                var questionSql = "SELECT q.Id AS QuestionId, q.Title, q.TextContent, q.QuestionType, " +
                    "q.DifficultyLevel, q.MarksAllocation, aq.DisplayOrder, " +
                    "q.ImageUrl, q.SnapshotUrl, q.BoardSessionId, q.HasBoardSession, q.HasMedia, q.HasAudio " +
                    "FROM AssessmentQuestion aq " +
                    "JOIN Questions q ON q.Id = aq.QuestionId " +
                    "WHERE aq.AssessmentId = @AssessmentId " +
                    "AND aq.SchoolId = @SchoolId " +
                    "AND aq.IsActive = 1 " +
                    "ORDER BY aq.DisplayOrder ASC";

                var questions = (await conn.QueryAsync<AssessmentQuestionDto>(
                    questionSql, new { AssessmentId = assessmentId, SchoolId = schoolId })).ToList();

                if (shuffle)
                {
                    var rng = new Random();
                    questions = questions.OrderBy(_ => rng.Next()).ToList();
                }

                // Fetch options
                if (questions.Any())
                {
                    var qIds = questions.Select(q => q.QuestionId).ToList();
                    var optionSql = "SELECT QuestionId, Id AS OptionId, OptionLabel, OptionText " +
                        "FROM QuestionOptions " +
                        "WHERE QuestionId IN @QuestionIds";

                    var options = await conn.QueryAsync<AssessmentOptionDto>(
                        optionSql, new { QuestionIds = qIds });

                    var optionsLookup = options.GroupBy(o => o.QuestionId)
                        .ToDictionary(g => g.Key, g => g.ToList());

                    foreach (var q in questions)
                    {
                        if (optionsLookup.TryGetValue(q.QuestionId, out var opts))
                            q.Options = opts;
                    }
                }

                return Ok("Attempt started", new
                {
                    AttemptId = attemptId,
                    AttemptNumber = attemptNumber,
                    IsOfficial = isOfficial,
                    TimeLimitMinutes = config.TimeLimitMinutes,
                    Questions = questions
                });
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error starting assessment attempt");
                return Bad("An error occurred while starting attempt", ResponseCode.ErrorOccured);
            }
        }
    }

    public async Task<BaseResponse> SubmitAnswer(SubmitAssessmentAnswerViewModel model, AuthenticatedUserClaims claims)
    {
        using (LogContext.PushProperty("RequestedBy", claims.UserId))
        {
            try
            {
                if (!Guid.TryParse(claims.SchoolId, out var schoolId))
                    return Bad("Invalid authentication", ResponseCode.Unauthorized);
                if (!Guid.TryParse(claims.UserId, out var studentId))
                    return Bad("Invalid authentication", ResponseCode.Unauthorized);

                var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

                var attemptSql = "SELECT TOP 1 Id, TotalMarks FROM AssessmentAttempt " +
                    "WHERE Id = @AttemptId " +
                    "AND StudentId = @StudentId " +
                    "AND SchoolId = @SchoolId " +
                    "AND Status = 'InProgress'";

                var attempt = await _attemptQuery.SelectByColumns(attemptSql, new Dictionary<string, object>
                {
                    { "AttemptId", model.AttemptId },
                    { "StudentId", studentId },
                    { "SchoolId", schoolId }
                });

                if (attempt is null)
                    return Bad("Attempt not found or already submitted", ResponseCode.NotFound);

                using var conn = new SqlConnection(_connString);
                conn.Open();

                // Auto-grade for objective questions
                decimal autoMarks = 0;
                bool? isCorrect = null;

                if (model.SelectedOptionId.HasValue)
                {
                    var correctOption = await conn.QueryFirstOrDefaultAsync<Guid?>(
                        "SELECT Id FROM QuestionOptions WHERE QuestionId = @QuestionId AND IsCorrect = 1",
                        new { QuestionId = model.QuestionId });

                    isCorrect = model.SelectedOptionId == correctOption;

                    var maxMarks = await conn.QueryFirstOrDefaultAsync<decimal>(
                        "SELECT MarksAllocation FROM Questions WHERE Id = @QuestionId",
                        new { QuestionId = model.QuestionId });

                    if (isCorrect == true)
                        autoMarks = maxMarks;
                }

                var maxMarksValue = await conn.QueryFirstOrDefaultAsync<decimal>(
                    "SELECT MarksAllocation FROM Questions WHERE Id = @QuestionId",
                    new { QuestionId = model.QuestionId });

                // Upsert answer
                var existingSql = "SELECT TOP 1 Id FROM AssessmentAttemptAnswer " +
                    "WHERE AttemptId = @AttemptId AND QuestionId = @QuestionId";

                var existing = await _answerQuery.SelectByColumns(existingSql, new Dictionary<string, object>
                {
                    { "AttemptId", model.AttemptId },
                    { "QuestionId", model.QuestionId }
                });

                if (existing is null)
                {
                    var answerId = Guid.NewGuid();
                    await _answerCommand.Create(new Dictionary<string, object>
                    {
                        { "Id", answerId },
                        { "AttemptId", model.AttemptId },
                        { "QuestionId", model.QuestionId },
                        { "SchoolId", schoolId },
                        { "QuestionType", 0 },
                        { "SelectedOptionId", (object?)model.SelectedOptionId ?? DBNull.Value },
                        { "IsCorrect", (object?)isCorrect ?? DBNull.Value },
                        { "AutoMarksObtained", autoMarks > 0 ? (object)autoMarks : DBNull.Value },
                        { "TypedAnswer", (object?)model.TypedAnswer ?? DBNull.Value },
                        { "BoardSessionId", (object?)model.BoardSessionId ?? DBNull.Value },
                        { "AudioUrl", (object?)model.AudioUrl ?? DBNull.Value },
                        { "MaxMarks", maxMarksValue },
                        { "IsSkipped", model.IsSkipped },
                        { "CreationDate", now },
                        { "ModifiedDate", now }
                    });

                    // Save multiple board session references (essay / short-answer)
                    var boardsToSave = new List<AnswerBoardInput>();
                    if (!string.IsNullOrWhiteSpace(model.BoardSessionId))
                        boardsToSave.Add(new AnswerBoardInput { BoardSessionId = model.BoardSessionId });
                    if (model.Boards?.Any() == true)
                        boardsToSave.AddRange(model.Boards.Where(b => !string.IsNullOrWhiteSpace(b.BoardSessionId)));

                    if (boardsToSave.Any())
                    {
                        foreach (var b in boardsToSave)
                        {
                            await _answerBoardCommand.Create(new Dictionary<string, object>
                            {
                                { "Id", Guid.NewGuid() },
                                { "AnswerId", answerId },
                                { "BoardSessionId", b.BoardSessionId.Trim() },
                                { "BoardIndex", (object?)b.BoardIndex ?? DBNull.Value },
                                { "BoardLabel", (object?)b.BoardLabel ?? DBNull.Value },
                                { "CreatedAt", now }
                            });
                        }
                    }
                }

                return Ok("Answer saved");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error submitting answer");
                return Bad("An error occurred while submitting answer", ResponseCode.ErrorOccured);
            }
        }
    }

    public async Task<BaseResponse> SubmitAttempt(Guid attemptId, AuthenticatedUserClaims claims)
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

                var attemptSql = "SELECT TOP 1 Id, StartedAt FROM AssessmentAttempt " +
                    "WHERE Id = @AttemptId " +
                    "AND StudentId = @StudentId " +
                    "AND SchoolId = @SchoolId " +
                    "AND Status = 'InProgress'";

                var attempt = await _attemptQuery.SelectByColumns(attemptSql, new Dictionary<string, object>
                {
                    { "AttemptId", attemptId },
                    { "StudentId", studentId },
                    { "SchoolId", schoolId }
                });

                if (attempt is null)
                    return Bad("Attempt not found or already submitted", ResponseCode.NotFound);

                using var conn = new SqlConnection(_connString);
                conn.Open();

                // Calculate scores
                var scoreSql = "SELECT " +
                    "ISNULL(SUM(AutoMarksObtained), 0) + ISNULL(SUM(ManualMarksObtained), 0) AS Obtained, " +
                    "ISNULL(SUM(MaxMarks), 0) AS Total " +
                    "FROM AssessmentAttemptAnswer " +
                    "WHERE AttemptId = @AttemptId AND IsSkipped = 0";

                var scores = await conn.QueryFirstOrDefaultAsync(scoreSql, new { AttemptId = attemptId });

                decimal obtained = (decimal)(scores?.Obtained ?? 0);
                decimal total = (decimal)(scores?.Total ?? 0);

                // Get pass mark
                var configSql = "SELECT TOP 1 ac.PassMarkPercent, ac.ShowResultImmediately, ac.ShowCorrectAnswers " +
                    "FROM AssessmentConfig ac " +
                    "JOIN AssessmentAttempt at2 ON at2.AssessmentId = ac.AssessmentId " +
                    "WHERE at2.Id = @AttemptId AND ac.IsActive = 1";

                var config = await _configQuery.SelectByColumns(configSql, new Dictionary<string, object>
                {
                    { "AttemptId", attemptId }
                });

                int passMark = config?.PassMarkPercent ?? 50;
                bool showResult = config?.ShowResultImmediately ?? true;
                bool showCorrectAnswers = config?.ShowCorrectAnswers ?? false;

                decimal finalScore = total > 0
                    ? Math.Round((obtained / total) * 100, 2)
                    : 0m;

                bool isPassed = finalScore >= passMark;

                // Calculate time taken
                int timeTaken = 0;
                if (DateTime.TryParse(attempt.StartedAt, out var started))
                {
                    timeTaken = (int)(DateTime.UtcNow - started).TotalSeconds;
                }

                await _attemptCommand.UpdateTableColumnById(
                    new Dictionary<string, object>
                    {
                        { "AutoMarksObtained", obtained },
                        { "TotalMarks", total },
                        { "FinalScorePercent", finalScore },
                        { "IsPassed", isPassed },
                        { "Status", "Submitted" },
                        { "SubmittedAt", now },
                        { "TimeTakenSeconds", timeTaken },
                        { "ModifiedDate", now }
                    },
                    new KeyValuePair<string, object>("Id", attemptId));

                if (showResult)
                {
                    var answers = new List<AssessmentAnswerResultDto>();
                    if (showCorrectAnswers)
                    {
                        var answerSql = "SELECT QuestionId, QuestionType, MaxMarks, " +
                            "ISNULL(AutoMarksObtained, 0) + ISNULL(ManualMarksObtained, 0) AS MarksObtained, " +
                            "IsCorrect, TypedAnswer, TeacherFeedback, IsSkipped " +
                            "FROM AssessmentAttemptAnswer " +
                            "WHERE AttemptId = @AttemptId " +
                            "ORDER BY CreationDate ASC";

                        var answerRows = await conn.QueryAsync<AssessmentAnswerResultDto>(
                            answerSql, new { AttemptId = attemptId });
                        answers = answerRows.ToList();
                    }

                    return Ok("Assessment submitted", new AssessmentResultDto
                    {
                        AttemptId = attempt.Id,
                        AssessmentCode = "",
                        Title = "",
                        AttemptNumber = 0,
                        IsOfficial = false,
                        TotalMarks = total,
                        AutoMarksObtained = obtained,
                        ManualMarksObtained = 0,
                        FinalScorePercent = finalScore,
                        IsPassed = isPassed,
                        Status = "Submitted",
                        SubmittedAt = now,
                        Answers = answers
                    });
                }

                return Ok("Assessment submitted successfully");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error submitting assessment attempt");
                return Bad("An error occurred while submitting attempt", ResponseCode.ErrorOccured);
            }
        }
    }

    public async Task<BaseResponse> GetResult(Guid attemptId, AuthenticatedUserClaims claims)
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

                var attemptSql = "SELECT " +
                    "at2.Id, at2.AttemptNumber, at2.IsOfficial, at2.Status, " +
                    "at2.AutoMarksObtained, at2.ManualMarksObtained, at2.TotalMarks, " +
                    "at2.FinalScorePercent, at2.IsPassed, at2.SubmittedAt, " +
                    "a.Code AS AssessmentCode, a.Title " +
                    "FROM AssessmentAttempt at2 " +
                    "JOIN Assessments a ON a.Id = at2.AssessmentId " +
                    "WHERE at2.Id = @AttemptId " +
                    "AND at2.StudentId = @StudentId " +
                    "AND at2.SchoolId = @SchoolId";

                var attempt = await conn.QueryFirstOrDefaultAsync(
                    attemptSql, new { AttemptId = attemptId, StudentId = studentId, SchoolId = schoolId });

                if (attempt is null)
                    return Bad("Attempt not found", ResponseCode.NotFound);

                var configSql = "SELECT TOP 1 ShowCorrectAnswers FROM AssessmentConfig " +
                    "WHERE AssessmentId = @AssessmentId AND IsActive = 1";

                bool showCorrectAnswers = false;
                try
                {
                    var config = await _configQuery.SelectByColumns(configSql, new Dictionary<string, object>
                    {
                        { "AssessmentId", (Guid)attempt.AssessmentId }
                    });
                    showCorrectAnswers = config?.ShowCorrectAnswers ?? false;
                }
                catch { /* ignore config read failures */ }

                var answers = new List<AssessmentAnswerResultDto>();
                if (showCorrectAnswers)
                {
                    var answerSql = "SELECT QuestionId, QuestionType, MaxMarks, " +
                        "ISNULL(AutoMarksObtained, 0) + ISNULL(ManualMarksObtained, 0) AS MarksObtained, " +
                        "IsCorrect, TypedAnswer, TeacherFeedback, IsSkipped " +
                        "FROM AssessmentAttemptAnswer " +
                        "WHERE AttemptId = @AttemptId " +
                        "ORDER BY CreationDate ASC";

                    var answerRows = await conn.QueryAsync<AssessmentAnswerResultDto>(
                        answerSql, new { AttemptId = attemptId });
                    answers = answerRows.ToList();
                }

                return Ok("Result retrieved", new AssessmentResultDto
                {
                    AttemptId = attempt.Id,
                    AssessmentCode = attempt.AssessmentCode,
                    Title = attempt.Title,
                    AttemptNumber = attempt.AttemptNumber,
                    IsOfficial = attempt.IsOfficial,
                    TotalMarks = attempt.TotalMarks,
                    AutoMarksObtained = attempt.AutoMarksObtained,
                    ManualMarksObtained = attempt.ManualMarksObtained,
                    FinalScorePercent = attempt.FinalScorePercent,
                    IsPassed = attempt.IsPassed,
                    Status = attempt.Status,
                    SubmittedAt = attempt.SubmittedAt?.ToString(),
                    Answers = answers
                });
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error fetching assessment result");
                return Bad("An error occurred while fetching result", ResponseCode.ErrorOccured);
            }
        }
    }

    public async Task<BaseResponse> GetAssignedAssessments(AuthenticatedUserClaims claims)
    {
        using (LogContext.PushProperty("RequestedBy", claims.UserId))
        {
            try
            {
                if (!Guid.TryParse(claims.SchoolId, out var schoolId))
                    return Bad("Invalid authentication", ResponseCode.Unauthorized);

                using var conn = new SqlConnection(_connString);
                conn.Open();

                var sql = "SELECT " +
                    "a.Id AS AssessmentId, a.Code, a.Title, a.Description, ac.ExpiresAt, " +
                    "aa.Id AS AssignmentId, aa.TargetType, aa.TargetId, aa.CreationDate AS AssignedAt " +
                    "FROM Assessments a " +
                    "JOIN AssessmentConfig ac ON ac.AssessmentId = a.Id AND ac.IsActive = 1 " +
                    "JOIN AssessmentAssignment aa ON aa.AssessmentId = a.Id AND aa.IsActive = 1 " +
                    "WHERE a.SchoolId = @SchoolId " +
                    "AND EXISTS (SELECT 1 FROM AssessmentAssignment WHERE AssessmentId = a.Id AND IsActive = 1) " +
                    "ORDER BY a.CreatedAt DESC, aa.TargetType";

                var rows = await conn.QueryAsync<dynamic>(sql, new { SchoolId = schoolId });

                var grouped = new List<AssignedAssessmentItemDto>();
                foreach (var row in rows)
                {
                    var existing = grouped.FirstOrDefault(g => g.AssessmentId == (Guid)row.AssessmentId);
                    if (existing == null)
                    {
                        existing = new AssignedAssessmentItemDto
                        {
                            AssessmentId = row.AssessmentId,
                            Code = row.Code,
                            Title = row.Title,
                            Description = row.Description,
                            ExpiresAt = row.ExpiresAt
                        };
                        grouped.Add(existing);
                    }

                    existing.Targets.Add(new AssignmentTargetDto
                    {
                        AssignmentId = row.AssignmentId,
                        TargetType = row.TargetType,
                        TargetId = row.TargetId,
                        AssignedAt = row.AssignedAt
                    });
                }

                return Ok($"{grouped.Count} assessment(s) with assignments found", grouped);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error fetching assigned assessments");
                return Bad("An error occurred while fetching assigned assessments", ResponseCode.ErrorOccured);
            }
        }
    }

    public async Task<BaseResponse> GetTeacherAssessments(AuthenticatedUserClaims claims)
    {
        using (LogContext.PushProperty("RequestedBy", claims.UserId))
        {
            try
            {
                if (!Guid.TryParse(claims.SchoolId, out var schoolId))
                    return Bad("Invalid authentication", ResponseCode.Unauthorized);
                if (!Guid.TryParse(claims.UserId, out var teacherId))
                    return Bad("Invalid authentication", ResponseCode.Unauthorized);

                var sql = "SELECT " +
                    "a.Id AS AssessmentId, a.Code, a.Title, a.Description, " +
                    "ac.TimeLimitMinutes, ac.PassMarkPercent, ac.ShuffleQuestions, " +
                    "ac.ShowResultImmediately, ac.ShowCorrectAnswers, ac.ExpiresAt, " +
                    "(SELECT COUNT(*) FROM AssessmentQuestion WHERE AssessmentId = a.Id AND IsActive = 1) AS QuestionCount, " +
                    "a.CreatedAt " +
                    "FROM Assessments a " +
                    "JOIN AssessmentConfig ac ON ac.AssessmentId = a.Id AND ac.IsActive = 1 " +
                    "WHERE a.SchoolId = @SchoolId " +
                    "AND a.CreatedByUserId = @TeacherId " +
                    "ORDER BY a.CreatedAt DESC";

                var rows = await _assessmentQuery.QueryAsync<TeacherAssessmentDto>(sql, new Dictionary<string, object>
                {
                    { "SchoolId", schoolId },
                    { "TeacherId", teacherId }
                });

                return Ok($"{rows.Count()} assessment(s) found", rows.ToList());
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error fetching teacher assessments");
                return Bad("An error occurred while fetching teacher assessments", ResponseCode.ErrorOccured);
            }
        }
    }

    public async Task<BaseResponse> GetAssessmentAssignments(Guid assessmentId, AuthenticatedUserClaims claims)
    {
        using (LogContext.PushProperty("RequestedBy", claims.UserId))
        {
            try
            {
                if (!Guid.TryParse(claims.SchoolId, out var schoolId))
                    return Bad("Invalid authentication", ResponseCode.Unauthorized);

                using var conn = new SqlConnection(_connString);
                conn.Open();

                var assessmentSql = "SELECT TOP 1 Id, Code, Title " +
                    "FROM Assessments " +
                    "WHERE Id = @AssessmentId AND SchoolId = @SchoolId";

                var assessment = await conn.QueryFirstOrDefaultAsync(
                    assessmentSql, new { AssessmentId = assessmentId, SchoolId = schoolId });

                if (assessment is null)
                    return Bad("Assessment not found", ResponseCode.NotFound);

                var sql = "SELECT " +
                    "aa.Id AS AssignmentId, aa.TargetType, aa.TargetId, aa.CreationDate AS AssignedAt " +
                    "FROM AssessmentAssignment aa " +
                    "WHERE aa.AssessmentId = @AssessmentId " +
                    "AND aa.SchoolId = @SchoolId " +
                    "AND aa.IsActive = 1 " +
                    "ORDER BY aa.TargetType, aa.CreationDate DESC";

                var assignments = await conn.QueryAsync<AssignmentTargetDto>(
                    sql, new { AssessmentId = assessmentId, SchoolId = schoolId });

                var dto = new AssessmentAssignmentsDto
                {
                    AssessmentId = assessment.Id,
                    Code = assessment.Code,
                    Title = assessment.Title,
                    Assignments = assignments.ToList()
                };

                return Ok("Assignments retrieved", dto);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error fetching assessment assignments");
                return Bad("An error occurred while fetching assignments", ResponseCode.ErrorOccured);
            }
        }
    }

    public async Task<BaseResponse> GetAttemptHistory(Guid assessmentId, AuthenticatedUserClaims claims)
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

                var sql = "SELECT " +
                    "Id AS AttemptId, AttemptNumber, IsOfficial, Status, " +
                    "FinalScorePercent, IsPassed, TimeTakenSeconds, SubmittedAt " +
                    "FROM AssessmentAttempt " +
                    "WHERE AssessmentId = @AssessmentId " +
                    "AND StudentId = @StudentId " +
                    "AND SchoolId = @SchoolId " +
                    "ORDER BY AttemptNumber DESC";

                var attempts = await conn.QueryAsync<AssessmentAttemptDto>(
                    sql, new { AssessmentId = assessmentId, StudentId = studentId, SchoolId = schoolId });

                return Ok($"{attempts.Count()} attempt(s) found", attempts.ToList());
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error fetching attempt history");
                return Bad("An error occurred while fetching history", ResponseCode.ErrorOccured);
            }
        }
    }
}
