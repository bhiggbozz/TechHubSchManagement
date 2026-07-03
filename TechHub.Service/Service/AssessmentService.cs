using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Context;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core;
using TechHub.Core.Entities;
using TechHub.Core.Model;
using TechHub.Core.ViewModel.classroom;
using TechHub.Service.Interface;
using TechHub.Service.Service.DatabaseService;

namespace TechHub.Service.Service;

public class AssessmentService : IAssessmentService
{
	private readonly ICommandRespository<Assessment> _assessmentCommand;
	private readonly ICommandRespository<AssessmentConfig> _configCommand;
	private readonly ICommandRespository<AssessmentQuestion> _questionCommand;
	private readonly ICommandRespository<AssessmentAssignment> _assignmentCommand;
	private readonly ICommandRespository<AssessmentAttempt> _attemptCommand;
	private readonly ICommandRespository<AssessmentAttemptAnswer> _answerCommand;
	private readonly IQueryRepository<Assessment> _assessmentQuery;
	private readonly IQueryRepository<AssessmentConfig> _configQuery;
	private readonly IQueryRepository<AssessmentAttempt> _attemptQuery;
	private readonly IQueryRepository<AssessmentAttemptAnswer> _answerQuery;
	private readonly IDbTransactionScopeFactory _dbTransactionScopeFactory;
	private readonly IConfiguration _configuration;
	private readonly ILogger _logger;
	private readonly string _connString;

	public AssessmentService(
		ICommandRespository<Assessment> assessmentCommand,
		ICommandRespository<AssessmentConfig> configCommand,
		ICommandRespository<AssessmentQuestion> questionCommand,
		ICommandRespository<AssessmentAssignment> assignmentCommand,
		ICommandRespository<AssessmentAttempt> attemptCommand,
		ICommandRespository<AssessmentAttemptAnswer> answerCommand,
		IQueryRepository<Assessment> assessmentQuery,
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
		_assessmentQuery = assessmentQuery;
		_configQuery = configQuery;
		_attemptQuery = attemptQuery;
		_answerQuery = answerQuery;
		_dbTransactionScopeFactory = dbTransactionScopeFactory;
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

	public async Task<BaseResponse> CreateAssessment(CreateAssessmentViewModel model, AuthenticatedUserClaims claims)
	{
		using (LogContext.PushProperty("RequestedBy", claims.UserId))
		{
			try
			{
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
					var existing = await _assessmentQuery.Get($@"
                        SELECT TOP 1 Id FROM Assessment
                        WHERE Code = '{code}' AND IsActive = 1");
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
						{ "Title", model.Title },
						{ "Description", model.Description },
						{ "SchoolId", schoolId },
						{ "CreatedBy", teacherId },
						{ "CreationDate", now },
						{ "ModifiedDate", now },
						{ "IsActive", true }
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
						var questions = model.QuestionIds.Select((qid, idx) => new Dictionary<string, object>
						{
							{ "Id", Guid.NewGuid() },
							{ "AssessmentId", assessmentId },
							{ "QuestionId", qid },
							{ "SchoolId", schoolId },
							{ "DisplayOrder", idx + 1 },
							{ "CreationDate", now },
							{ "IsActive", true }
						}).ToList();

						await _questionCommand.CreateBatchAsync(scope.Transaction, scope.Connection, questions);
					}

					await scope.CommitAsync();
				}
				catch
				{
					await scope.RollbackAsync();
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
					QuestionCount = model.QuestionIds.Count
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

				using var conn = new SqlConnection(_connString);
				conn.Open();

				using var tx = conn.BeginTransaction();
				try
				{
					await _assignmentCommand.CreateBatchAsync(tx, conn, assignments);
					tx.Commit();
				}
				catch
				{
					tx.Rollback();
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

				var sql = $@"
                    SELECT DISTINCT
                        a.Id       AS AssessmentId,
                        a.Code,
                        a.Title,
                        a.Description,
                        ac.TimeLimitMinutes,
                        (SELECT COUNT(*) FROM AssessmentQuestion WHERE AssessmentId = a.Id) AS QuestionCount,
                        at2.AttemptNumber,
                        at2.FinalScorePercent,
                        at2.IsPassed,
                        CASE
                            WHEN at2.Id IS NULL THEN 'NotStarted'
                            WHEN at2.Status = 'InProgress' THEN 'InProgress'
                            ELSE 'Completed'
                        END AS Status
                    FROM Assessment a
                    JOIN AssessmentConfig ac ON ac.AssessmentId = a.Id AND ac.IsActive = 1
                    JOIN AssessmentAssignment aa ON aa.AssessmentId = a.Id AND aa.IsActive = 1
                    LEFT JOIN (
                        SELECT AssessmentId, AttemptNumber, FinalScorePercent, IsPassed, Status, Id,
                               ROW_NUMBER() OVER (PARTITION BY AssessmentId ORDER BY AttemptNumber DESC) AS rn
                        FROM AssessmentAttempt
                        WHERE StudentId = '{studentId}' AND SchoolId = '{schoolId}'
                    ) at2 ON at2.AssessmentId = a.Id AND at2.rn = 1
                    WHERE a.SchoolId = '{schoolId}'
                    AND   a.IsActive = 1
                    AND   (
                        aa.TargetType = 'Student' AND aa.TargetId = '{studentId}'
                        OR aa.TargetType = 'Subject' AND aa.TargetId IN (
                            SELECT SubjectId FROM TeacherSubject WHERE TeacherId = '{studentId}' AND IsActive = 1
                        )
                        OR aa.TargetType = 'Classroom' AND aa.TargetId IN (
                            SELECT ClassroomId FROM StudentClassroom WHERE StudentId = '{studentId}'
                        )
                    )
                    ORDER BY a.Title ASC";

				using var conn = new SqlConnection(_connString);
				conn.Open();
				var rows = await conn.QueryAsync<AssessmentListItemDto>(sql);

				return Ok($"{rows.Count()} assessment(s) found", rows.ToList());
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Error fetching student assessments");
				return Bad("An error occurred while fetching assessments", ResponseCode.ErrorOccured);
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

				var assessment = await _assessmentQuery.Get($@"
                    SELECT TOP 1 a.Id, a.Code, a.Title, a.Description,
                           ac.TimeLimitMinutes, ac.PassMarkPercent, ac.ShuffleQuestions, ac.ShowResultImmediately
                    FROM Assessment a
                    JOIN AssessmentConfig ac ON ac.AssessmentId = a.Id AND ac.IsActive = 1
                    WHERE a.Id = '{assessmentId}' AND a.SchoolId = '{schoolId}' AND a.IsActive = 1");

				if (assessment is null)
					return Bad("Assessment not found", ResponseCode.NotFound);

				using var conn = new SqlConnection(_connString);
				conn.Open();

				var questionRows = await conn.QueryAsync<AssessmentQuestionDto>($@"
                    SELECT
                        q.Id          AS QuestionId,
                        q.Title,
                        q.TextContent,
                        q.QuestionType,
                        q.DifficultyLevel,
                        q.MarksAllocation,
                        aq.DisplayOrder
                    FROM AssessmentQuestion aq
                    JOIN Questions q ON q.Id = aq.QuestionId
                    WHERE aq.AssessmentId = '{assessmentId}'
                    AND   aq.SchoolId    = '{schoolId}'
                    AND   aq.IsActive    = 1
                    ORDER BY aq.DisplayOrder ASC");

				var questionList = questionRows.ToList();

				// Fetch options for objective questions
				if (questionList.Any())
				{
					var questionIds = string.Join(",", questionList.Select(q => $"'{q.QuestionId}'"));
					var options = await conn.QueryAsync<AssessmentOptionDto>($@"
                        SELECT QuestionId, Id AS OptionId, OptionLabel, OptionText
                        FROM QuestionOptions
                        WHERE QuestionId IN ({questionIds})");

					var optionsLookup = options.GroupBy(o => o.QuestionId)
						.ToDictionary(g => g.Key, g => g.ToList());

					foreach (var q in questionList)
					{
						if (optionsLookup.TryGetValue(q.QuestionId, out var opts))
							q.Options = opts;
					}
				}

				var config = await _configQuery.Get($@"
                    SELECT TOP 1 * FROM AssessmentConfig
                    WHERE AssessmentId = '{assessmentId}' AND IsActive = 1");

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

				// Check if there's already an InProgress attempt
				var inProgress = await _attemptQuery.Get($@"
                    SELECT TOP 1 Id FROM AssessmentAttempt
                    WHERE AssessmentId = '{assessmentId}'
                    AND   StudentId   = '{studentId}'
                    AND   SchoolId    = '{schoolId}'
                    AND   Status      = 'InProgress'");

				if (inProgress is not null)
					return Ok("You have an active attempt. Resume it.", new { AttemptId = inProgress.Id, Resume = true });

				// Count existing attempts
				var existingCount = await _attemptQuery.Get($@"
                    SELECT COUNT(*) FROM AssessmentAttempt
                    WHERE AssessmentId = '{assessmentId}'
                    AND   StudentId   = '{studentId}'
                    AND   SchoolId    = '{schoolId}'
                    AND   Status     IN ('Submitted','PartiallyGraded','FullyGraded')");

				int existingAttempts = existingCount is not null ? Convert.ToInt32(existingCount) : 0;
				int attemptNumber = existingAttempts + 1;
				bool isOfficial = existingAttempts == 0; // First attempt = official

				// Verify assessment exists and get config
				var config = await _configQuery.Get($@"
                    SELECT TOP 1 TimeLimitMinutes, ShuffleQuestions FROM AssessmentConfig
                    WHERE AssessmentId = '{assessmentId}' AND IsActive = 1");

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
				var questions = (await conn.QueryAsync<AssessmentQuestionDto>($@"
                    SELECT
                        q.Id          AS QuestionId,
                        q.Title,
                        q.TextContent,
                        q.QuestionType,
                        q.DifficultyLevel,
                        q.MarksAllocation,
                        aq.DisplayOrder
                    FROM AssessmentQuestion aq
                    JOIN Questions q ON q.Id = aq.QuestionId
                    WHERE aq.AssessmentId = '{assessmentId}'
                    AND   aq.SchoolId    = '{schoolId}'
                    AND   aq.IsActive    = 1
                    ORDER BY aq.DisplayOrder ASC")).ToList();

				if (shuffle)
				{
					var rng = new Random();
					questions = questions.OrderBy(_ => rng.Next()).ToList();
				}

				// Fetch options
				if (questions.Any())
				{
					var qIds = string.Join(",", questions.Select(q => $"'{q.QuestionId}'"));
					var options = await conn.QueryAsync<AssessmentOptionDto>($@"
                        SELECT QuestionId, Id AS OptionId, OptionLabel, OptionText
                        FROM QuestionOptions
                        WHERE QuestionId IN ({qIds})");

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

				var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

				if (!Guid.TryParse(claims.UserId, out var studentId))
					return Bad("Invalid authentication", ResponseCode.Unauthorized);

				var attempt = await _attemptQuery.Get($@"
                    SELECT TOP 1 Id, TotalMarks FROM AssessmentAttempt
                    WHERE Id        = '{model.AttemptId}'
                    AND   StudentId = '{studentId}'
                    AND   SchoolId  = '{schoolId}'
                    AND   Status    = 'InProgress'");

				if (attempt is null)
					return Bad("Attempt not found or already submitted", ResponseCode.NotFound);

				using var conn = new SqlConnection(_connString);
				conn.Open();

				// Auto-grade for objective questions
				decimal autoMarks = 0;
				bool? isCorrect = null;

				if (model.SelectedOptionId.HasValue)
				{
					var correctOption = await conn.QueryFirstOrDefaultAsync<Guid?>($@"
                        SELECT Id FROM QuestionOptions
                        WHERE QuestionId = '{model.QuestionId}' AND IsCorrect = 1");

					isCorrect = model.SelectedOptionId == correctOption;

					var maxMarks = await conn.QueryFirstOrDefaultAsync<decimal>($@"
                        SELECT MarksAllocation FROM Questions
                        WHERE Id = '{model.QuestionId}'");

					if (isCorrect == true)
						autoMarks = maxMarks;
				}

				var maxMarksValue = await conn.QueryFirstOrDefaultAsync<decimal>($@"
                    SELECT MarksAllocation FROM Questions
                    WHERE Id = '{model.QuestionId}'");

				// Upsert answer
				var existing = await _answerQuery.Get($@"
                    SELECT TOP 1 Id FROM AssessmentAttemptAnswer
                    WHERE AttemptId  = '{model.AttemptId}'
                    AND   QuestionId = '{model.QuestionId}'");

				if (existing is null)
				{
					await _answerCommand.Create(new Dictionary<string, object>
					{
						{ "Id", Guid.NewGuid() },
						{ "AttemptId", model.AttemptId },
						{ "QuestionId", model.QuestionId },
						{ "SchoolId", schoolId },
						{ "QuestionType", 0 },
						{ "SelectedOptionId", model.SelectedOptionId },
						{ "IsCorrect", isCorrect },
						{ "AutoMarksObtained", autoMarks > 0 ? (object)autoMarks : null },
						{ "TypedAnswer", model.TypedAnswer },
						{ "BoardSessionId", model.BoardSessionId },
						{ "AudioUrl", model.AudioUrl },
						{ "MaxMarks", maxMarksValue },
						{ "IsSkipped", model.IsSkipped },
						{ "CreationDate", now },
						{ "ModifiedDate", now }
					});
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

				var attempt = await _attemptQuery.Get($@"
                    SELECT TOP 1 Id, StartedAt FROM AssessmentAttempt
                    WHERE Id        = '{attemptId}'
                    AND   StudentId = '{studentId}'
                    AND   SchoolId  = '{schoolId}'
                    AND   Status    = 'InProgress'");

				if (attempt is null)
					return Bad("Attempt not found or already submitted", ResponseCode.NotFound);

				using var conn = new SqlConnection(_connString);
				conn.Open();

				// Calculate scores
				var scores = await conn.QueryFirstOrDefaultAsync($@"
                    SELECT
                        ISNULL(SUM(AutoMarksObtained), 0) + ISNULL(SUM(ManualMarksObtained), 0) AS Obtained,
                        ISNULL(SUM(MaxMarks), 0) AS Total
                    FROM AssessmentAttemptAnswer
                    WHERE AttemptId = '{attemptId}' AND IsSkipped = 0");

				decimal obtained = (decimal)(scores?.Obtained ?? 0);
				decimal total = (decimal)(scores?.Total ?? 0);

				// Get pass mark
				var config = await _configQuery.Get($@"
                    SELECT TOP 1 a.PassMarkPercent, a.ShowResultImmediately
                    FROM AssessmentConfig a
                    JOIN AssessmentAttempt at2 ON at2.AssessmentId = a.AssessmentId
                    WHERE at2.Id = '{attemptId}' AND a.IsActive = 1");

				int passMark = config?.PassMarkPercent ?? 50;
				bool showResult = config?.ShowResultImmediately ?? true;

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
					var answers = await conn.QueryAsync<AssessmentAnswerResultDto>($@"
                        SELECT
                            QuestionId,
                            QuestionType,
                            MaxMarks,
                            ISNULL(AutoMarksObtained, 0) + ISNULL(ManualMarksObtained, 0) AS MarksObtained,
                            IsCorrect,
                            TypedAnswer,
                            TeacherFeedback,
                            IsSkipped
                        FROM AssessmentAttemptAnswer
                        WHERE AttemptId = '{attemptId}'
                        ORDER BY CreationDate ASC");

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
						Answers = answers.ToList()
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

				var attempt = await conn.QueryFirstOrDefaultAsync($@"
                    SELECT
                        at2.Id, at2.AttemptNumber, at2.IsOfficial, at2.Status,
                        at2.AutoMarksObtained, at2.ManualMarksObtained, at2.TotalMarks,
                        at2.FinalScorePercent, at2.IsPassed, at2.SubmittedAt,
                        a.Code AS AssessmentCode, a.Title
                    FROM AssessmentAttempt at2
                    JOIN Assessment a ON a.Id = at2.AssessmentId
                    WHERE at2.Id        = '{attemptId}'
                    AND   at2.StudentId = '{studentId}'
                    AND   at2.SchoolId  = '{schoolId}'");

				if (attempt is null)
					return Bad("Attempt not found", ResponseCode.NotFound);

				var answers = await conn.QueryAsync<AssessmentAnswerResultDto>($@"
                    SELECT
                        QuestionId, QuestionType, MaxMarks,
                        ISNULL(AutoMarksObtained, 0) + ISNULL(ManualMarksObtained, 0) AS MarksObtained,
                        IsCorrect, TypedAnswer, TeacherFeedback, IsSkipped
                    FROM AssessmentAttemptAnswer
                    WHERE AttemptId = '{attemptId}'
                    ORDER BY CreationDate ASC");

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
					Answers = answers.ToList()
				});
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Error fetching assessment result");
				return Bad("An error occurred while fetching result", ResponseCode.ErrorOccured);
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

				var attempts = await conn.QueryAsync<AssessmentAttemptDto>($@"
                    SELECT
                        Id AS AttemptId, AttemptNumber, IsOfficial, Status,
                        FinalScorePercent, IsPassed, TimeTakenSeconds, SubmittedAt
                    FROM AssessmentAttempt
                    WHERE AssessmentId = '{assessmentId}'
                    AND   StudentId   = '{studentId}'
                    AND   SchoolId    = '{schoolId}'
                    ORDER BY AttemptNumber DESC");

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
