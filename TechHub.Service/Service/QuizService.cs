using Serilog;
using Serilog.Context;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using TechHub.Core;
using TechHub.Core.Entities;
using TechHub.Core.Model;
using TechHub.Core.ViewModel.classroom;
using TechHub.Service.Interface;
using TechHub.Service.Service.DatabaseService;

namespace TechHub.Service.Service;

public class QuizService : IQuizService
{
	private readonly ICommandRespository<Quiz> _quizCommand;
	private readonly ICommandRespository<QuizQuestion> _quizQuestionCommand;
	private readonly IQueryRepository<Quiz> _quizQuery;
	private readonly IQueryRepository<QuizQuestion> _quizQuestionQuery;
	private readonly IQueryRepository<LessonContent> _lessonQuery;
	private readonly ICommandRespository<LessonContent> _lessonCommand;

	private readonly IQueryRepository<QuizAttempt> _attemptQuery;
	private readonly ICommandRespository<QuizAttempt> _attemptCommand;
	private readonly IQueryRepository<QuizAttemptAnswer> _answerQuery;
	private readonly ICommandRespository<QuizAttemptAnswer> _answerCommand;
	private readonly IQueryRepository<QuizConfig> _configQuery;
	private readonly ICommandRespository<QuizConfig> _configCommand;

	private readonly IDbTransactionScopeFactory _scopeFactory;
	private readonly ILogger _logger;

	public QuizService(
		ICommandRespository<Quiz> quizCommand,
		ICommandRespository<QuizQuestion> quizQuestionCommand,
		IQueryRepository<Quiz> quizQuery,
		IQueryRepository<QuizQuestion> quizQuestionQuery,
		IQueryRepository<LessonContent> lessonQuery,
		ICommandRespository<LessonContent> lessonCommand,
		IQueryRepository<QuizAttempt> attemptQuery,
		ICommandRespository<QuizAttempt> attemptCommand,
		IQueryRepository<QuizAttemptAnswer> answerQuery,
		ICommandRespository<QuizAttemptAnswer> answerCommand,
		IQueryRepository<QuizConfig> configQuery,
		ICommandRespository<QuizConfig> configCommand,
		IDbTransactionScopeFactory scopeFactory,
		ILogger logger)
	{
		_quizCommand = quizCommand;
		_quizQuestionCommand = quizQuestionCommand;
		_quizQuery = quizQuery;
		_quizQuestionQuery = quizQuestionQuery;
		_lessonQuery = lessonQuery;
		_lessonCommand = lessonCommand;
		_attemptQuery = attemptQuery;
		_attemptCommand = attemptCommand;
		_answerQuery = answerQuery;
		_answerCommand = answerCommand;
		_configQuery = configQuery;
		_configCommand = configCommand;
		_scopeFactory = scopeFactory;
		_logger = logger;
	}

	// ═════════════════════════════════════════════════════════════════════════
	// EXISTING METHODS (unchanged)
	// ═════════════════════════════════════════════════════════════════════════

	public async Task<BaseResponse> CreateQuiz(
		CreateQuizViewModel model, AuthenticatedUserClaims claims)
	{
		try
		{
			if (!Guid.TryParse(claims.UserId, out var userId))
				return Unauthorized();
			if (!Guid.TryParse(claims.SchoolId, out var schoolId))
				return Unauthorized();

			if (!model.QuestionIds.Any())
				return new BaseResponse
				{
					ResponseCode = ResponseCode.BadRequest,
					ResponseMessage = "At least one question is required",
					Status = "failed"
				};

			if (model.QuestionIds.Distinct().Count() != model.QuestionIds.Count)
				return new BaseResponse
				{
					ResponseCode = ResponseCode.BadRequest,
					ResponseMessage = "Duplicate questions are not allowed",
					Status = "failed"
				};

			string code;
			int attempts = 0;
			do
			{
				code = QuizCodeGenerator.Generate();
				attempts++;

				var existing = await _quizQuery.Get($@"
                    SELECT TOP 1 Id FROM Quiz
                    WHERE Code     = '{code}'
                    AND   IsActive = 1");

				if (existing is null) break;

				if (attempts > 10)
					return new BaseResponse
					{
						ResponseCode = ResponseCode.ErrorOccured,
						ResponseMessage = "Could not generate unique quiz code. Please try again.",
						Status = "failed"
					};

			} while (true);

			var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
			var quizId = Guid.NewGuid();

			var quizDict = new Dictionary<string, object>
			{
				{ "Id",           quizId   },
				{ "Code",         code     },
				{ "SchoolId",     schoolId },
				{ "CreatedBy",    userId   },
				{ "CreationDate", now      },
				{ "ModifiedDate", now      },
				{ "IsActive",     true     }
			};

			var questionDicts = model.QuestionIds.Select((qId, index) =>
				new Dictionary<string, object>
				{
					{ "Id",           Guid.NewGuid() },
					{ "QuizId",       quizId         },
					{ "QuestionId",   qId            },
					{ "SchoolId",     schoolId       },
					{ "DisplayOrder", index + 1      },
					{ "CreationDate", now            },
					{ "IsActive",     true           }
				}).ToList();

			using var scope = _scopeFactory.Create("DbConnectionString");
			try
			{
				await _quizCommand.Create(
					scope.Transaction, scope.Connection, quizDict);

				await _quizQuestionCommand.CreateBatchAsync(
					scope.Transaction, scope.Connection, questionDicts);

				await scope.CommitAsync();
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Rolling back quiz creation");
				try { await scope.RollbackAsync(); } catch { }
				throw;
			}

			_logger.Information(
				"Quiz created - Code: {Code}, QuestionCount: {Count}, CreatedBy: {UserId}",
				code, model.QuestionIds.Count, userId);

			return new BaseResponse
			{
				ResponseCode = ResponseCode.successful,
				ResponseMessage = "Quiz created successfully",
				Status = "successful",
				Data = new
				{
					QuizCode = code,
					QuestionCount = model.QuestionIds.Count,
					CreatedAt = now
				}
			};
		}
		catch (Exception ex)
		{
			_logger.Error(ex, "Error creating quiz");
			return new BaseResponse
			{
				ResponseCode = ResponseCode.ErrorOccured,
				ResponseMessage = "An error occurred while creating quiz",
				Status = "failed"
			};
		}
	}

	public async Task<BaseResponse> AttachQuizToLesson(Guid lessonId, AttachQuizViewModel model, AuthenticatedUserClaims claims)
	{
		try
		{
			if (!Guid.TryParse(claims.SchoolId, out var schoolId))
				return Unauthorized();

			var quiz = await _quizQuery.Get($@"
                SELECT TOP 1 Id, Code FROM Quiz
                WHERE  Code     = '{model.QuizCode.Trim().ToUpper()}'
                AND    SchoolId = '{schoolId}'
                AND    IsActive = 1");

			if (quiz is null)
				return new BaseResponse
				{
					ResponseCode = ResponseCode.NotFound,
					ResponseMessage = $"Quiz code '{model.QuizCode}' not found",
					Status = "failed"
				};

			var lesson = await _lessonQuery.Get($@"
                SELECT TOP 1 Id FROM LessonContent
                WHERE  Id       = '{lessonId}'
                AND    SchoolId = '{schoolId}'");

			if (lesson is null)
				return new BaseResponse
				{
					ResponseCode = ResponseCode.NotFound,
					ResponseMessage = "Lesson not found",
					Status = "failed"
				};

			await _lessonCommand.UpdateTableColumnById(
				new Dictionary<string, object>
				{
					{ "QuizCode",    model.QuizCode.Trim().ToUpper()          },
					{ "ModifiedAt",  DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") }
				},
				new KeyValuePair<string, object>("Id", lessonId));

			_logger.Information(
				"Quiz attached to lesson - LessonId: {LessonId}, QuizCode: {Code}",
				lessonId, model.QuizCode);

			return new BaseResponse
			{
				ResponseCode = ResponseCode.successful,
				ResponseMessage = "Quiz attached to lesson successfully",
				Status = "successful",
				Data = new
				{
					LessonId = lessonId,
					QuizCode = model.QuizCode.Trim().ToUpper()
				}
			};
		}
		catch (Exception ex)
		{
			_logger.Error(ex,
				"Error attaching quiz to lesson - LessonId: {LessonId}", lessonId);
			return new BaseResponse
			{
				ResponseCode = ResponseCode.ErrorOccured,
				ResponseMessage = "An error occurred while attaching quiz",
				Status = "failed"
			};
		}
	}

	public async Task<BaseResponse> GetQuizByLesson(Guid lessonId, AuthenticatedUserClaims claims)
	{
		try
		{
			if (!Guid.TryParse(claims.SchoolId, out var schoolId))
				return Unauthorized();

			var lesson = await _lessonQuery.Get($@"
                SELECT TOP 1 Id, QuizCode FROM LessonContent
                WHERE  Id       = '{lessonId}'
                AND    SchoolId = '{schoolId}'
                AND    Status   = '{LessonStatus.Published}'");

			if (lesson is null)
				return new BaseResponse
				{
					ResponseCode = ResponseCode.NotFound,
					ResponseMessage = "Lesson not found",
					Status = "failed"
				};

			if (string.IsNullOrWhiteSpace(lesson.QuizCode))
				return new BaseResponse
				{
					ResponseCode = ResponseCode.NotFound,
					ResponseMessage = "This lesson has no quiz attached",
					Status = "failed"
				};

			var sql = $@"
                SELECT
                    q.Id              AS QuestionId,
                    q.Title,
                    q.TextContent,
                    q.QuestionType,
                    q.DifficultyLevel,
                    q.MarksAllocation,
                    s.Subject         AS SubjectName,
                    t.Name            AS TopicName,
                    qq.DisplayOrder
                FROM   Quiz          qz
                JOIN   QuizQuestion  qq ON qq.QuizId     = qz.Id
                JOIN   Questions     q  ON q.Id          = qq.QuestionId
                LEFT JOIN Subjects   s  ON s.Id          = q.SubjectId
                LEFT JOIN Topic      t  ON t.Id          = q.TopicId
                WHERE  qz.Code      = '{lesson.QuizCode}'
                AND    qz.SchoolId  = '{schoolId}'
                AND    qq.IsActive  = 1
                AND    q.IsActive   = 1
                ORDER  BY qq.DisplayOrder ASC";

			var questions = await _lessonQuery
				.QueryAsync<QuizQuestionDto>(sql, new Dictionary<string, object>());

			var questionList = questions.ToList();

			return new BaseResponse
			{
				ResponseCode = ResponseCode.successful,
				ResponseMessage = questionList.Any()
					? $"{questionList.Count} question(s) found"
					: "No questions found for this quiz",
				Status = "successful",
				Data = new
				{
					LessonId = lessonId,
					QuizCode = lesson.QuizCode,
					QuestionCount = questionList.Count,
					Questions = questionList
				}
			};
		}
		catch (Exception ex)
		{
			_logger.Error(ex,
				"Error fetching quiz - LessonId: {LessonId}", lessonId);
			return new BaseResponse
			{
				ResponseCode = ResponseCode.ErrorOccured,
				ResponseMessage = "An error occurred while fetching quiz",
				Status = "failed"
			};
		}
	}

	// ═════════════════════════════════════════════════════════════════════════
	// QUIZ CONFIG
	// ═════════════════════════════════════════════════════════════════════════

	public async Task<BaseResponse> SaveQuizConfig(QuizConfigViewModel model, AuthenticatedUserClaims claims)
	{
		using (LogContext.PushProperty("RequestedBy", claims.UserId))
		{
			try
			{
				if (!Guid.TryParse(claims.UserId, out var teacherId))
					return Unauthorized();
				if (!Guid.TryParse(claims.SchoolId, out var schoolId))
					return Unauthorized();

				var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

				var existing = await _configQuery.Get($@"
                    SELECT TOP 1 Id FROM QuizConfig
                    WHERE TeacherId = '{teacherId}'
                    AND   SchoolId  = '{schoolId}'
                    AND   IsActive  = 1");

				if (existing != null)
				{
					var updateDict = new Dictionary<string, object>
					{
						{ "AllowRetakes", model.AllowRetakes },
						{ "MaxAttempts", model.MaxAttempts },
						{ "PassMarkPercent", model.PassMarkPercent },
						{ "TimeLimitMinutes", (object?)model.TimeLimitMinutes ?? DBNull.Value },
						{ "AutoSubmitOnTimeout", model.AutoSubmitOnTimeout },
						{ "ShuffleQuestions", model.ShuffleQuestions },
						{ "ShowResultImmediately", model.ShowResultImmediately },
						{ "ShowCorrectAnswers", model.ShowCorrectAnswers },
						{ "AllowBoardAnswer", model.AllowBoardAnswer },
						{ "AllowAIAssistance", model.AllowAIAssistance },
						{ "MaxAIAssistancePerQuestion", model.MaxAIAssistancePerQuestion },
						{ "StarMarkConfig", model.StarMarkConfig },
						{ "ModifiedDate", now }
					};

					await _configCommand.UpdateTableColumnById(
						updateDict,
						new KeyValuePair<string, object>("Id", existing.Id));

					_logger.Information(
						"QuizConfig updated - TeacherId: {TeacherId}, SchoolId: {SchoolId}",
						teacherId, schoolId);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage = "Config updated successfully",
						Status = "successful"
					};
				}
				else
				{
					var insertDict = new Dictionary<string, object>
					{
						{ "Id", Guid.NewGuid() },
						{ "TeacherId", teacherId },
						{ "SchoolId", schoolId },
						{ "AllowRetakes", model.AllowRetakes },
						{ "MaxAttempts", model.MaxAttempts },
						{ "PassMarkPercent", model.PassMarkPercent },
						{ "TimeLimitMinutes", (object?)model.TimeLimitMinutes ?? DBNull.Value },
						{ "AutoSubmitOnTimeout", model.AutoSubmitOnTimeout },
						{ "ShuffleQuestions", model.ShuffleQuestions },
						{ "ShowResultImmediately", model.ShowResultImmediately },
						{ "ShowCorrectAnswers", model.ShowCorrectAnswers },
						{ "AllowBoardAnswer", model.AllowBoardAnswer },
						{ "AllowAIAssistance", model.AllowAIAssistance },
						{ "MaxAIAssistancePerQuestion", model.MaxAIAssistancePerQuestion },
						{ "StarMarkConfig", model.StarMarkConfig },
						{ "CreatedBy", teacherId },
						{ "CreationDate", now },
						{ "ModifiedDate", now },
						{ "IsActive", true }
					};

					using var scope = _scopeFactory.Create("DbConnectionString");
					try
					{
						await _configCommand.Create(scope.Transaction, scope.Connection, insertDict);
						await scope.CommitAsync();
					}
					catch (Exception ex)
					{
						_logger.Error(ex, "Rolling back quiz config creation");
						try { await scope.RollbackAsync(); } catch { }
						throw;
					}

					_logger.Information(
						"QuizConfig created - TeacherId: {TeacherId}, SchoolId: {SchoolId}",
						teacherId, schoolId);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage = "Config saved successfully",
						Status = "successful"
					};
				}
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Error saving quiz config");
				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "An error occurred while saving config",
					Status = "failed"
				};
			}
		}
	}

	public async Task<BaseResponse> GetQuizConfig(AuthenticatedUserClaims claims)
	{
		using (LogContext.PushProperty("RequestedBy", claims.UserId))
		{
			try
			{
				if (!Guid.TryParse(claims.UserId, out var teacherId))
					return Unauthorized();
				if (!Guid.TryParse(claims.SchoolId, out var schoolId))
					return Unauthorized();

				var config = await _configQuery.Get($@"
                    SELECT TOP 1
                        Id, TeacherId, AllowRetakes, MaxAttempts, PassMarkPercent,
                        TimeLimitMinutes, AutoSubmitOnTimeout, ShuffleQuestions,
                        ShowResultImmediately, ShowCorrectAnswers, AllowBoardAnswer,
                        AllowAIAssistance, MaxAIAssistancePerQuestion, StarMarkConfig
                    FROM QuizConfig
                    WHERE TeacherId = '{teacherId}'
                    AND   SchoolId  = '{schoolId}'
                    AND   IsActive  = 1");

				if (config != null)
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage = "Config retrieved",
						Status = "successful",
						Data = new QuizConfigDto
						{
							Id = config.Id,
							TeacherId = config.TeacherId,
							AllowRetakes = config.AllowRetakes,
							MaxAttempts = config.MaxAttempts,
							PassMarkPercent = config.PassMarkPercent,
							TimeLimitMinutes = config.TimeLimitMinutes,
							AutoSubmitOnTimeout = config.AutoSubmitOnTimeout,
							ShuffleQuestions = config.ShuffleQuestions,
							ShowResultImmediately = config.ShowResultImmediately,
							ShowCorrectAnswers = config.ShowCorrectAnswers,
							AllowBoardAnswer = config.AllowBoardAnswer,
							AllowAIAssistance = config.AllowAIAssistance,
							MaxAIAssistancePerQuestion = config.MaxAIAssistancePerQuestion,
							StarMarkConfig = config.StarMarkConfig
						}
					};
				}

				// System defaults
				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "System default config",
					Status = "successful",
					Data = new QuizConfigDto
					{
						Id = Guid.Empty,
						TeacherId = teacherId,
						AllowRetakes = false,
						MaxAttempts = 1,
						PassMarkPercent = 50,
						TimeLimitMinutes = null,
						AutoSubmitOnTimeout = true,
						ShuffleQuestions = false,
						ShowResultImmediately = true,
						ShowCorrectAnswers = false,
						AllowBoardAnswer = true,
						AllowAIAssistance = false,
						MaxAIAssistancePerQuestion = 1000,
						StarMarkConfig = "{\"1\":1,\"2\":2,\"3\":3,\"4\":4,\"5\":5}"
					}
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Error retrieving quiz config");
				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "An error occurred while retrieving config",
					Status = "failed"
				};
			}
		}
	}

	// ═════════════════════════════════════════════════════════════════════════
	// ATTEMPT (student)
	// ═════════════════════════════════════════════════════════════════════════

	public async Task<BaseResponse> StartQuizAttempt(StartQuizViewModel model, AuthenticatedUserClaims claims)
	{
		using (LogContext.PushProperty("RequestedBy", claims.UserId))
		{
			try
			{
				if (!Guid.TryParse(claims.UserId, out var studentId))
					return Unauthorized();
				if (!Guid.TryParse(claims.SchoolId, out var schoolId))
					return Unauthorized();

				// ── Verify lesson exists, published, has quiz ─────────────────────
				var lesson = await _lessonQuery.Get($@"
                    SELECT TOP 1 Id, QuizCode, ClassroomId FROM LessonContent
                    WHERE  Id       = '{model.LessonId}'
                    AND    SchoolId = '{schoolId}'
                    AND    Status   = '{LessonStatus.Published}'");

				if (lesson is null)
					return new BaseResponse
					{
						ResponseCode = ResponseCode.NotFound,
						ResponseMessage = "Lesson not found or not published",
						Status = "failed"
					};

				if (string.IsNullOrWhiteSpace(lesson.QuizCode))
					return new BaseResponse
					{
						ResponseCode = ResponseCode.NotFound,
						ResponseMessage = "This lesson has no quiz attached",
						Status = "failed"
					};

				// ── Verify student enrollment ──────────────────────────────────────
				var enrolled = await _quizQuery.QueryAsync<int>($@"
                    SELECT TOP 1 1 FROM StudentClassroom
                    WHERE StudentId  = '{studentId}'
                    AND   ClassroomId = '{lesson.ClassroomId}'
                    AND   SchoolId   = '{schoolId}'
                    AND   IsActive   = 1", new Dictionary<string, object>());

				if (!enrolled.Any())
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Forbidden,
						ResponseMessage = "You are not enrolled in this class",
						Status = "failed"
					};

				// ── Check for existing InProgress attempt ──────────────────────────
				var existingAttempt = await _attemptQuery.Get($@"
                    SELECT TOP 1 Id, Status, AttemptNumber FROM QuizAttempt
                    WHERE  StudentId = '{studentId}'
                    AND    LessonId  = '{model.LessonId}'
                    AND    SchoolId  = '{schoolId}'
                    AND    Status    = '{QuizAttemptStatus.InProgress}'
                    ORDER  BY CreationDate DESC");

				if (existingAttempt != null)
				{
					_logger.Information(
						"Resuming existing attempt - AttemptId: {AttemptId}, StudentId: {StudentId}",
						existingAttempt.Id, studentId);

					var resumeQuestions = await FetchQuestionsForAttempt(lesson.QuizCode, schoolId);
					return new BaseResponse
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage = "Resuming existing attempt",
						Status = "successful",
						Data = new StartQuizResponseDto
						{
							AttemptId = existingAttempt.Id,
							QuizCode = lesson.QuizCode,
							AttemptNumber = existingAttempt.AttemptNumber,
							TotalQuestions = resumeQuestions.Count,
							Questions = resumeQuestions
						}
					};
				}

				// ── Get teacher config and count previous attempts ─────────────────
				var quiz = await _quizQuery.Get($@"
                    SELECT TOP 1 CreatedBy FROM Quiz
                    WHERE  Code     = '{lesson.QuizCode}'
                    AND    SchoolId = '{schoolId}'
                    AND    IsActive = 1");

				if (quiz is null)
					return new BaseResponse
					{
						ResponseCode = ResponseCode.NotFound,
						ResponseMessage = "Quiz not found",
						Status = "failed"
					};

				var teacherConfig = await _configQuery.Get($@"
                    SELECT TOP 1 AllowRetakes, MaxAttempts, ShuffleQuestions, TimeLimitMinutes
                    FROM QuizConfig
                    WHERE TeacherId = '{quiz.CreatedBy}'
                    AND   SchoolId  = '{schoolId}'
                    AND   IsActive  = 1");

				bool allowRetakes = teacherConfig?.AllowRetakes ?? false;
				int maxAttempts = teacherConfig?.MaxAttempts ?? 1;
				bool shuffle = teacherConfig?.ShuffleQuestions ?? false;
				int? timeLimit = teacherConfig?.TimeLimitMinutes;

				var completedCountResult = await _quizQuery.QueryAsync<int>($@"
                    SELECT COUNT(*) FROM QuizAttempt
                    WHERE  StudentId = '{studentId}'
                    AND    LessonId  = '{model.LessonId}'
                    AND    SchoolId  = '{schoolId}'
                    AND    Status    IN ('{QuizAttemptStatus.Submitted}','{QuizAttemptStatus.PartiallyGraded}','{QuizAttemptStatus.FullyGraded}')",
                    new Dictionary<string, object>());

				int completedAttempts = completedCountResult.FirstOrDefault();

				if (!allowRetakes && completedAttempts >= 1)
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Forbidden,
						ResponseMessage = "Retakes are not allowed for this quiz",
						Status = "failed"
					};

				if (completedAttempts >= maxAttempts)
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Forbidden,
						ResponseMessage = $"Maximum attempts ({maxAttempts}) reached",
						Status = "failed"
					};

				// ── Create new attempt ────────────────────────────────────────────
				var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
				var attemptId = Guid.NewGuid();
				int attemptNumber = completedAttempts + 1;

				var attemptDict = new Dictionary<string, object>
				{
					{ "Id", attemptId },
					{ "QuizCode", lesson.QuizCode },
					{ "LessonId", model.LessonId },
					{ "StudentId", studentId },
					{ "SchoolId", schoolId },
					{ "AttemptNumber", attemptNumber },
					{ "StartedAt", now },
					{ "TotalQuestions", 0 },
					{ "TotalAutoGraded", 0 },
					{ "TotalManualGraded", 0 },
					{ "TotalCorrect", 0 },
					{ "TotalWrong", 0 },
					{ "TotalSkipped", 0 },
					{ "AutoMarksObtained", 0m },
					{ "ManualMarksObtained", 0m },
					{ "TotalMarks", 0m },
					{ "Status", QuizAttemptStatus.InProgress },
					{ "CreationDate", now },
					{ "ModifiedDate", now }
				};

				using var scope = _scopeFactory.Create("DbConnectionString");
				try
				{
					await _attemptCommand.Create(scope.Transaction, scope.Connection, attemptDict);
					await scope.CommitAsync();
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "Rolling back attempt creation");
					try { await scope.RollbackAsync(); } catch { }
					throw;
				}

				var questionList = await FetchQuestionsForAttempt(lesson.QuizCode, schoolId);
				if (shuffle)
					questionList = ShuffleList(questionList);

				_logger.Information(
					"Quiz attempt started - AttemptId: {AttemptId}, StudentId: {StudentId}, LessonId: {LessonId}",
					attemptId, studentId, model.LessonId);

				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "Quiz started",
					Status = "successful",
					Data = new StartQuizResponseDto
					{
						AttemptId = attemptId,
						QuizCode = lesson.QuizCode,
						AttemptNumber = attemptNumber,
						TimeLimitMinutes = timeLimit,
						TotalQuestions = questionList.Count,
						Questions = questionList
					}
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Error starting quiz attempt");
				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "An error occurred while starting quiz",
					Status = "failed"
				};
			}
		}
	}

	public async Task<BaseResponse> SubmitQuizAttempt(Guid attemptId, SubmitQuizViewModel model, AuthenticatedUserClaims claims)
	{
		using (LogContext.PushProperty("RequestedBy", claims.UserId))
		{
			try
			{
				if (!Guid.TryParse(claims.UserId, out var studentId))
					return Unauthorized();
				if (!Guid.TryParse(claims.SchoolId, out var schoolId))
					return Unauthorized();

				// ── Verify attempt exists and is InProgress ──────────────────────
				var attempt = await _attemptQuery.Get($@"
                    SELECT TOP 1 Id, QuizCode, LessonId, StudentId, AttemptNumber, Status
                    FROM QuizAttempt
                    WHERE  Id       = '{attemptId}'
                    AND    StudentId = '{studentId}'
                    AND    SchoolId = '{schoolId}'");

				if (attempt is null)
					return new BaseResponse
					{
						ResponseCode = ResponseCode.NotFound,
						ResponseMessage = "Attempt not found",
						Status = "failed"
					};

				if (attempt.Status != QuizAttemptStatus.InProgress)
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Conflict,
						ResponseMessage = "This attempt has already been submitted",
						Status = "failed"
					};

				// ── Fetch quiz config for grading rules ──────────────────────────
				var quiz = await _quizQuery.Get($@"
                    SELECT TOP 1 CreatedBy FROM Quiz
                    WHERE  Code     = '{attempt.QuizCode}'
                    AND    SchoolId = '{schoolId}'
                    AND    IsActive = 1");

				var teacherConfig = await _configQuery.Get($@"
                    SELECT TOP 1 ShowResultImmediately, PassMarkPercent, StarMarkConfig
                    FROM QuizConfig
                    WHERE TeacherId = '{quiz.CreatedBy}'
                    AND   SchoolId  = '{schoolId}'
                    AND   IsActive  = 1");

				bool showResultImmediately = teacherConfig?.ShowResultImmediately ?? true;
				int passMarkPercent = teacherConfig?.PassMarkPercent ?? 50;
				string starMarkConfigJson = teacherConfig?.StarMarkConfig ?? "{\"1\":1,\"2\":2,\"3\":3,\"4\":4,\"5\":5}";

				// ── Fetch all questions for this quiz ────────────────────────────
				var questionRows = await _quizQuery.QueryAsync<QuestionGradeInfoDto>($@"
                    SELECT
                        q.Id              AS Id,
                        q.QuestionType    AS QuestionType,
                        q.MarksAllocation AS MarksAllocation
                    FROM   Quiz          qz
                    JOIN   QuizQuestion  qq ON qq.QuizId = qz.Id
                    JOIN   Questions     q  ON q.Id      = qq.QuestionId
                    WHERE  qz.Code      = '{attempt.QuizCode}'
                    AND    qz.SchoolId  = '{schoolId}'
                    AND    qq.IsActive  = 1
                    AND    q.IsActive   = 1", new Dictionary<string, object>());

				var questions = questionRows.ToList();
				var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

				// ── Build answers and auto-grade ─────────────────────────────────
				var answerDicts = new List<Dictionary<string, object>>();
				decimal totalMarks = 0m;
				decimal autoMarksObtained = 0m;
				int totalAutoGraded = 0;
				int totalManualGraded = 0;
				int totalCorrect = 0;
				int totalWrong = 0;
				int totalSkipped = 0;

				foreach (var q in questions)
				{
					var submitted = model.Answers.FirstOrDefault(a => a.QuestionId == q.Id);
					bool isSkipped = submitted?.IsSkipped ?? true;
					decimal maxMarks = ResolveStarMark(starMarkConfigJson, q.MarksAllocation);
					totalMarks += maxMarks;

					var answerDict = new Dictionary<string, object>
					{
						{ "Id", Guid.NewGuid() },
						{ "AttemptId", attemptId },
						{ "QuestionId", q.Id },
						{ "SchoolId", schoolId },
						{ "QuestionType", q.QuestionType },
						{ "SelectedOptionId", (object?)submitted?.SelectedOptionId ?? DBNull.Value },
						{ "IsCorrect", DBNull.Value },
						{ "AutoMarksObtained", DBNull.Value },
						{ "TypedAnswer", (object?)submitted?.TypedAnswer ?? DBNull.Value },
						{ "BoardSessionId", (object?)submitted?.BoardSessionId ?? DBNull.Value },
						{ "AudioUrl", (object?)submitted?.AudioUrl ?? DBNull.Value },
						{ "ManualMarksObtained", DBNull.Value },
						{ "TeacherFeedback", DBNull.Value },
						{ "GradedBy", DBNull.Value },
						{ "GradedAt", DBNull.Value },
						{ "MaxMarks", maxMarks },
						{ "TimeTakenMs", (object?)submitted?.TimeTakenMs ?? DBNull.Value },
						{ "IsSkipped", isSkipped },
						{ "CreationDate", now },
						{ "ModifiedDate", now }
					};

					if (isSkipped)
					{
						totalSkipped++;
						answerDict["IsCorrect"] = false;
						answerDict["AutoMarksObtained"] = 0m;
					}
					else if (q.QuestionType == 1) // MultipleChoice
					{
						totalAutoGraded++;
						bool correct = false;
						if (submitted?.SelectedOptionId.HasValue == true)
						{
						var optRows = await _quizQuery.QueryAsync<OptionCorrectDto>($@"
                                SELECT TOP 1 IsCorrect FROM QuestionOptions
                                WHERE  Id          = '{submitted.SelectedOptionId}'
                                AND    QuestionId  = '{q.Id}'
                                AND    IsActive    = 1
                                AND    IsDeleted   = 0", new Dictionary<string, object>());
						correct = optRows.FirstOrDefault()?.IsCorrect ?? false;
						}

						answerDict["IsCorrect"] = correct;
						answerDict["AutoMarksObtained"] = correct ? maxMarks : 0m;
						if (correct) { autoMarksObtained += maxMarks; totalCorrect++; }
						else totalWrong++;
					}
					else if (q.QuestionType == 4) // TrueOrFalse
					{
						totalAutoGraded++;
						bool correct = false;
						if (!string.IsNullOrWhiteSpace(submitted?.TypedAnswer))
						{
						var ansRows = await _quizQuery.QueryAsync<QuestionCorrectAnswerDto>($@"
                                SELECT TOP 1 CorrectAnswer FROM Questions
                                WHERE  Id       = '{q.Id}'
                                AND    SchoolId = '{schoolId}'
                                AND    IsActive = 1", new Dictionary<string, object>());
						correct = string.Equals(ansRows.FirstOrDefault()?.CorrectAnswer, submitted.TypedAnswer, StringComparison.OrdinalIgnoreCase);
						}

						answerDict["IsCorrect"] = correct;
						answerDict["AutoMarksObtained"] = correct ? maxMarks : 0m;
						if (correct) { autoMarksObtained += maxMarks; totalCorrect++; }
						else totalWrong++;
					}
					else
					{
						// Manual graded
						totalManualGraded++;
					}

					answerDicts.Add(answerDict);
				}

				// ── Determine status ─────────────────────────────────────────────
				string finalStatus = totalManualGraded > 0
					? QuizAttemptStatus.PartiallyGraded
					: QuizAttemptStatus.Submitted;

				decimal finalScorePercent = 0m;
				bool? isPassed = null;
				if (totalMarks > 0 && totalManualGraded == 0)
				{
					finalScorePercent = Math.Round((autoMarksObtained / totalMarks) * 100, 2);
					isPassed = finalScorePercent >= passMarkPercent;
				}

				// ── Persist answers and update attempt ───────────────────────────
				var updateDict = new Dictionary<string, object>
				{
					{ "SubmittedAt", now },
					{ "TimeTakenSeconds", (object?)model.TimeTakenSeconds ?? DBNull.Value },
					{ "TotalQuestions", questions.Count },
					{ "TotalAutoGraded", totalAutoGraded },
					{ "TotalManualGraded", totalManualGraded },
					{ "TotalCorrect", totalCorrect },
					{ "TotalWrong", totalWrong },
					{ "TotalSkipped", totalSkipped },
					{ "AutoMarksObtained", autoMarksObtained },
					{ "ManualMarksObtained", 0m },
					{ "TotalMarks", totalMarks },
					{ "FinalScorePercent", (object?)finalScorePercent ?? DBNull.Value },
					{ "IsPassed", (object?)isPassed ?? DBNull.Value },
					{ "Status", finalStatus },
					{ "ModifiedDate", now }
				};

				using var scope = _scopeFactory.Create("DbConnectionString");
				try
				{
					if (answerDicts.Any())
						await _answerCommand.CreateBatchAsync(scope.Transaction, scope.Connection, answerDicts);

					await _attemptCommand.UpdateTableColumnById(
						updateDict,
						new KeyValuePair<string, object>("Id", attemptId));

					await scope.CommitAsync();
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "Rolling back quiz submission");
					try { await scope.RollbackAsync(); } catch { }
					throw;
				}

				_logger.Information(
					"Quiz submitted - AttemptId: {AttemptId}, StudentId: {StudentId}, Status: {Status}",
					attemptId, studentId, finalStatus);

				if (showResultImmediately && totalManualGraded == 0)
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage = "Quiz submitted successfully",
						Status = "successful",
						Data = new QuizResultDto
						{
							AttemptId = attemptId,
							QuizCode = attempt.QuizCode,
							AttemptNumber = attempt.AttemptNumber,
							TotalMarks = totalMarks,
							AutoMarksObtained = autoMarksObtained,
							ManualMarksObtained = 0m,
							FinalScorePercent = finalScorePercent,
							IsPassed = isPassed,
							Status = finalStatus,
							SubmittedAt = now
						}
					};
				}

				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = totalManualGraded > 0
						? "Quiz submitted. Some answers require manual grading."
						: "Quiz submitted successfully",
					Status = "successful"
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Error submitting quiz attempt");
				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "An error occurred while submitting quiz",
					Status = "failed"
				};
			}
		}
	}

	public async Task<BaseResponse> GetQuizResult(Guid attemptId, AuthenticatedUserClaims claims)
	{
		using (LogContext.PushProperty("RequestedBy", claims.UserId))
		{
			try
			{
				if (!Guid.TryParse(claims.UserId, out var userId))
					return Unauthorized();
				if (!Guid.TryParse(claims.SchoolId, out var schoolId))
					return Unauthorized();

				var attempt = await _attemptQuery.Get($@"
                    SELECT TOP 1
                        Id, QuizCode, LessonId, StudentId, AttemptNumber,
                        SubmittedAt, TotalMarks, AutoMarksObtained, ManualMarksObtained,
                        FinalScorePercent, IsPassed, Status, TimeTakenSeconds
                    FROM QuizAttempt
                    WHERE  Id       = '{attemptId}'
                    AND    SchoolId = '{schoolId}'");

				if (attempt is null)
					return new BaseResponse
					{
						ResponseCode = ResponseCode.NotFound,
						ResponseMessage = "Attempt not found",
						Status = "failed"
					};

				// Students can only view their own results
				if (attempt.StudentId != userId)
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Forbidden,
						ResponseMessage = "You are not authorized to view this result",
						Status = "failed"
					};

				// ── Fetch config to respect ShowResultImmediately ────────────────
				var quiz = await _quizQuery.Get($@"
                    SELECT TOP 1 CreatedBy FROM Quiz
                    WHERE  Code     = '{attempt.QuizCode}'
                    AND    SchoolId = '{schoolId}'
                    AND    IsActive = 1");

				var teacherConfig = await _configQuery.Get($@"
                    SELECT TOP 1 ShowResultImmediately FROM QuizConfig
                    WHERE TeacherId = '{quiz.CreatedBy}'
                    AND   SchoolId  = '{schoolId}'
                    AND   IsActive  = 1");

				bool showResultImmediately = teacherConfig?.ShowResultImmediately ?? true;

				if (!showResultImmediately && attempt.Status != QuizAttemptStatus.FullyGraded)
					return new BaseResponse
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage = "Result will be available after manual grading is complete",
						Status = "successful"
					};

				var answerRows = await _answerQuery.QueryAsync<QuizAnswerResultDto>($@"
                    SELECT
                        QuestionId,
                        QuestionType,
                        MaxMarks,
                        ISNULL(AutoMarksObtained, 0) + ISNULL(ManualMarksObtained, 0) AS MarksObtained,
                        IsCorrect,
                        TypedAnswer,
                        TeacherFeedback,
                        IsSkipped
                    FROM QuizAttemptAnswer
                    WHERE AttemptId = '{attemptId}'
                    AND   SchoolId  = '{schoolId}'
                    ORDER BY CreationDate ASC", new Dictionary<string, object>());

				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "Result retrieved",
					Status = "successful",
					Data = new QuizResultDto
					{
						AttemptId = attempt.Id,
						QuizCode = attempt.QuizCode,
						AttemptNumber = attempt.AttemptNumber,
						TotalMarks = attempt.TotalMarks,
						AutoMarksObtained = attempt.AutoMarksObtained,
						ManualMarksObtained = attempt.ManualMarksObtained,
						FinalScorePercent = attempt.FinalScorePercent ?? 0m,
						IsPassed = attempt.IsPassed,
						Status = attempt.Status,
						SubmittedAt = attempt.SubmittedAt,
						Answers = answerRows.ToList()
					}
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Error retrieving quiz result");
				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "An error occurred while retrieving result",
					Status = "failed"
				};
			}
		}
	}

	// ═════════════════════════════════════════════════════════════════════════
	// GRADING (teacher)
	// ═════════════════════════════════════════════════════════════════════════

	public async Task<BaseResponse> GetPendingGrades(AuthenticatedUserClaims claims)
	{
		using (LogContext.PushProperty("RequestedBy", claims.UserId))
		{
			try
			{
				if (!Guid.TryParse(claims.UserId, out var teacherId))
					return Unauthorized();
				if (!Guid.TryParse(claims.SchoolId, out var schoolId))
					return Unauthorized();

				var sql = $@"
                    SELECT
                        qa.Id            AS AnswerId,
                        qa.AttemptId     AS AttemptId,
                        qa.QuestionId    AS QuestionId,
                        att.StudentId    AS StudentId,
                        u.FirstName + ' ' + u.LastName AS StudentName,
                        qz.Code          AS QuizCode,
                        lc.Aim           AS LessonTitle,
                        qa.QuestionType  AS QuestionType,
                        qa.TypedAnswer   AS TypedAnswer,
                        qa.BoardSessionId AS BoardSessionId,
                        qa.AudioUrl      AS AudioUrl,
                        qa.MaxMarks      AS MaxMarks,
                        att.SubmittedAt  AS SubmittedAt
                    FROM QuizAttemptAnswer qa
                    JOIN QuizAttempt att ON att.Id = qa.AttemptId
                    JOIN Quiz qz ON qz.Code = att.QuizCode
                    JOIN LessonContent lc ON lc.Id = att.LessonId
                    JOIN Users u ON u.Id = att.StudentId
                    WHERE qa.SchoolId = '{schoolId}'
                    AND   qz.SchoolId = '{schoolId}'
                    AND   qz.CreatedBy = '{teacherId}'
                    AND   qa.QuestionType IN (2, 3, 5, 6, 7, 8)
                    AND   qa.ManualMarksObtained IS NULL
                    AND   qa.IsSkipped = 0
                    AND   att.Status IN ('{QuizAttemptStatus.Submitted}','{QuizAttemptStatus.PartiallyGraded}')
                    ORDER BY att.SubmittedAt DESC";

				var rows = await _answerQuery.QueryAsync<PendingGradeDto>(sql, new Dictionary<string, object>());
				var list = rows.ToList();

				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = $"{list.Count} pending answer(s) found",
					Status = "successful",
					Data = list
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Error fetching pending grades");
				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "An error occurred while fetching pending grades",
					Status = "failed"
				};
			}
		}
	}

	public async Task<BaseResponse> GradeAnswer(Guid answerId, GradeAnswerViewModel model, AuthenticatedUserClaims claims)
	{
		using (LogContext.PushProperty("RequestedBy", claims.UserId))
		{
			try
			{
				if (!Guid.TryParse(claims.UserId, out var teacherId))
					return Unauthorized();
				if (!Guid.TryParse(claims.SchoolId, out var schoolId))
					return Unauthorized();

				if (model.ManualMarksObtained < 0)
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Marks cannot be negative",
						Status = "failed"
					};

				var answer = await _answerQuery.Get($@"
                    SELECT TOP 1 Id, AttemptId, MaxMarks FROM QuizAttemptAnswer
                    WHERE  Id       = '{answerId}'
                    AND    SchoolId = '{schoolId}'");

				if (answer is null)
					return new BaseResponse
					{
						ResponseCode = ResponseCode.NotFound,
						ResponseMessage = "Answer not found",
						Status = "failed"
					};

				if (model.ManualMarksObtained > answer.MaxMarks)
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = $"Marks cannot exceed maximum ({answer.MaxMarks})",
						Status = "failed"
					};

				var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
				var updateDict = new Dictionary<string, object>
				{
					{ "ManualMarksObtained", model.ManualMarksObtained },
					{ "TeacherFeedback", (object?)model.TeacherFeedback ?? DBNull.Value },
					{ "GradedBy", teacherId },
					{ "GradedAt", now },
					{ "ModifiedDate", now }
				};

				await _answerCommand.UpdateTableColumnById(
					updateDict,
					new KeyValuePair<string, object>("Id", answerId));

				// ── Check if all manual answers for this attempt are now graded ───
				var remainingResult = await _quizQuery.QueryAsync<int>($@"
                    SELECT COUNT(*) FROM QuizAttemptAnswer
                    WHERE AttemptId = '{answer.AttemptId}'
                    AND   SchoolId  = '{schoolId}'
                    AND   QuestionType IN (2, 3, 5, 6, 7, 8)
                    AND   IsSkipped = 0
                    AND   ManualMarksObtained IS NULL", new Dictionary<string, object>());

				int remaining = remainingResult.FirstOrDefault();

				if (remaining == 0)
				{
					// ── Finalize attempt ────────────────────────────────────────
					var attempt = await _attemptQuery.Get($@"
                        SELECT TOP 1
                            AutoMarksObtained, TotalMarks, LessonId
                        FROM QuizAttempt
                        WHERE Id = '{answer.AttemptId}'
                        AND   SchoolId = '{schoolId}'");

					var manualSumResult = await _quizQuery.QueryAsync<decimal>($@"
                        SELECT ISNULL(SUM(ManualMarksObtained), 0) FROM QuizAttemptAnswer
                        WHERE AttemptId = '{answer.AttemptId}'
                        AND   SchoolId  = '{schoolId}'", new Dictionary<string, object>());

					decimal manualMarks = manualSumResult.FirstOrDefault();
					decimal totalMarks = attempt.TotalMarks;
					decimal autoMarks = attempt.AutoMarksObtained;

					decimal finalScore = totalMarks > 0
						? Math.Round(((autoMarks + manualMarks) / totalMarks) * 100, 2)
						: 0m;

					// Get pass mark from config
					var lesson = await _lessonQuery.Get($@"
                        SELECT TOP 1 QuizCode FROM LessonContent
                        WHERE Id = '{attempt.LessonId}' AND SchoolId = '{schoolId}'");

					var quiz = await _quizQuery.Get($@"
                        SELECT TOP 1 CreatedBy FROM Quiz
                        WHERE Code = '{lesson.QuizCode}' AND SchoolId = '{schoolId}' AND IsActive = 1");

					var config = await _configQuery.Get($@"
                        SELECT TOP 1 PassMarkPercent FROM QuizConfig
                        WHERE TeacherId = '{quiz.CreatedBy}' AND SchoolId = '{schoolId}' AND IsActive = 1");

					int passMark = config?.PassMarkPercent ?? 50;
					bool isPassed = finalScore >= passMark;

					var attemptUpdate = new Dictionary<string, object>
					{
						{ "ManualMarksObtained", manualMarks },
						{ "FinalScorePercent", finalScore },
						{ "IsPassed", isPassed },
						{ "Status", QuizAttemptStatus.FullyGraded },
						{ "ModifiedDate", now }
					};

					await _attemptCommand.UpdateTableColumnById(
						attemptUpdate,
						new KeyValuePair<string, object>("Id", answer.AttemptId));

					_logger.Information(
						"Attempt fully graded - AttemptId: {AttemptId}, FinalScore: {FinalScore}",
						answer.AttemptId, finalScore);
				}

				_logger.Information(
					"Answer graded - AnswerId: {AnswerId}, Marks: {Marks}, TeacherId: {TeacherId}",
					answerId, model.ManualMarksObtained, teacherId);

				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "Answer graded successfully",
					Status = "successful"
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Error grading answer");
				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "An error occurred while grading answer",
					Status = "failed"
				};
			}
		}
	}

	// ═════════════════════════════════════════════════════════════════════════
	// ANALYTICS
	// ═════════════════════════════════════════════════════════════════════════

	public async Task<BaseResponse> GetLessonQuizResults(Guid lessonId, AuthenticatedUserClaims claims)
	{
		using (LogContext.PushProperty("RequestedBy", claims.UserId))
		{
			try
			{
				if (!Guid.TryParse(claims.SchoolId, out var schoolId))
					return Unauthorized();

				var lesson = await _lessonQuery.Get($@"
                    SELECT TOP 1 Id, QuizCode FROM LessonContent
                    WHERE  Id       = '{lessonId}'
                    AND    SchoolId = '{schoolId}'");

				if (lesson is null)
					return new BaseResponse
					{
						ResponseCode = ResponseCode.NotFound,
						ResponseMessage = "Lesson not found",
						Status = "failed"
					};

				if (string.IsNullOrWhiteSpace(lesson.QuizCode))
					return new BaseResponse
					{
						ResponseCode = ResponseCode.NotFound,
						ResponseMessage = "This lesson has no quiz attached",
						Status = "failed"
					};

				var sql = $@"
                    SELECT
                        att.Id               AS AttemptId,
                        att.StudentId        AS StudentId,
                        u.FirstName + ' ' + u.LastName AS StudentName,
                        att.AttemptNumber    AS AttemptNumber,
                        att.FinalScorePercent AS FinalScorePercent,
                        att.IsPassed         AS IsPassed,
                        att.Status           AS Status,
                        att.SubmittedAt      AS SubmittedAt
                    FROM QuizAttempt att
                    JOIN Users u ON u.Id = att.StudentId
                    WHERE att.LessonId  = '{lessonId}'
                    AND   att.SchoolId  = '{schoolId}'
                    AND   att.Status    IN ('{QuizAttemptStatus.Submitted}','{QuizAttemptStatus.PartiallyGraded}','{QuizAttemptStatus.FullyGraded}')
                    ORDER BY att.SubmittedAt DESC";

				var rows = await _attemptQuery.QueryAsync<LessonQuizResultDto>(sql, new Dictionary<string, object>());

				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = $"{rows.Count()} result(s) found",
					Status = "successful",
					Data = rows.ToList()
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Error fetching lesson quiz results");
				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "An error occurred while fetching results",
					Status = "failed"
				};
			}
		}
	}

	public async Task<BaseResponse> GetLessonQuizAnalytics(Guid lessonId, AuthenticatedUserClaims claims)
	{
		using (LogContext.PushProperty("RequestedBy", claims.UserId))
		{
			try
			{
				if (!Guid.TryParse(claims.SchoolId, out var schoolId))
					return Unauthorized();

				var lesson = await _lessonQuery.Get($@"
                    SELECT TOP 1 Id, QuizCode FROM LessonContent
                    WHERE  Id       = '{lessonId}'
                    AND    SchoolId = '{schoolId}'");

				if (lesson is null)
					return new BaseResponse
					{
						ResponseCode = ResponseCode.NotFound,
						ResponseMessage = "Lesson not found",
						Status = "failed"
					};

				if (string.IsNullOrWhiteSpace(lesson.QuizCode))
					return new BaseResponse
					{
						ResponseCode = ResponseCode.NotFound,
						ResponseMessage = "This lesson has no quiz attached",
						Status = "failed"
					};

				// Aggregate attempt stats
				var aggResult = await _quizQuery.QueryAsync<AggregateAttemptDto>($@"
                    SELECT
                        COUNT(*) AS TotalAttempts,
                        COUNT(DISTINCT StudentId) AS TotalStudents,
                        AVG(CAST(ISNULL(FinalScorePercent, 0) AS DECIMAL(10,2))) AS AverageScore,
                        SUM(CASE WHEN IsPassed = 1 THEN 1 ELSE 0 END) * 100.0 / NULLIF(COUNT(*), 0) AS PassRate
                    FROM QuizAttempt
                    WHERE LessonId = '{lessonId}'
                    AND   SchoolId = '{schoolId}'
                    AND   Status   IN ('{QuizAttemptStatus.Submitted}','{QuizAttemptStatus.PartiallyGraded}','{QuizAttemptStatus.FullyGraded}')",
                    new Dictionary<string, object>());

				var agg = aggResult.FirstOrDefault();

				// Per-question stats
				var questionStats = await _quizQuery.QueryAsync<QuestionAnalyticsDto>($@"
                    SELECT
                        qa.QuestionId             AS QuestionId,
                        q.Title                   AS QuestionTitle,
                        qa.QuestionType           AS QuestionType,
                        qa.MaxMarks               AS MaxMarks,
                        AVG(CAST(ISNULL(qa.AutoMarksObtained, 0) + ISNULL(qa.ManualMarksObtained, 0) AS DECIMAL(10,2))) AS AverageMarksObtained,
                        COUNT(*)                  AS TotalAttempts,
                        SUM(CASE WHEN ISNULL(qa.AutoMarksObtained, 0) + ISNULL(qa.ManualMarksObtained, 0) = qa.MaxMarks THEN 1 ELSE 0 END) * 100.0 / NULLIF(COUNT(*), 0) AS SuccessRate
                    FROM QuizAttemptAnswer qa
                    JOIN QuizAttempt att ON att.Id = qa.AttemptId
                    JOIN Questions q ON q.Id = qa.QuestionId
                    WHERE att.LessonId = '{lessonId}'
                    AND   qa.SchoolId  = '{schoolId}'
                    AND   att.Status   IN ('{QuizAttemptStatus.Submitted}','{QuizAttemptStatus.PartiallyGraded}','{QuizAttemptStatus.FullyGraded}')
                    GROUP BY qa.QuestionId, q.Title, qa.QuestionType, qa.MaxMarks", new Dictionary<string, object>());

				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "Analytics retrieved",
					Status = "successful",
					Data = new QuizAnalyticsDto
					{
						LessonId = lessonId,
						QuizCode = lesson.QuizCode,
						TotalStudents = agg?.TotalStudents ?? 0,
						TotalAttempts = agg?.TotalAttempts ?? 0,
						AverageScore = Math.Round(agg?.AverageScore ?? 0m, 2),
						PassRate = Math.Round(agg?.PassRate ?? 0m, 2),
						PerQuestionStats = questionStats.ToList()
					}
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Error fetching lesson quiz analytics");
				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "An error occurred while fetching analytics",
					Status = "failed"
				};
			}
		}
	}

	public async Task<BaseResponse> GetStudentQuizHistory(Guid studentId, AuthenticatedUserClaims claims)
	{
		using (LogContext.PushProperty("RequestedBy", claims.UserId))
		{
			try
			{
				if (!Guid.TryParse(claims.SchoolId, out var schoolId))
					return Unauthorized();

				// Students can only view their own history
				if (!Guid.TryParse(claims.UserId, out var requesterId))
					return Unauthorized();

				if (studentId != requesterId)
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Forbidden,
						ResponseMessage = "You can only view your own quiz history",
						Status = "failed"
					};

				var sql = $@"
                    SELECT
                        att.Id               AS AttemptId,
                        att.QuizCode         AS QuizCode,
                        att.LessonId         AS LessonId,
                        lc.Aim               AS LessonTitle,
                        att.AttemptNumber    AS AttemptNumber,
                        att.FinalScorePercent AS FinalScorePercent,
                        att.IsPassed         AS IsPassed,
                        att.Status           AS Status,
                        att.SubmittedAt      AS SubmittedAt
                    FROM QuizAttempt att
                    JOIN LessonContent lc ON lc.Id = att.LessonId
                    WHERE att.StudentId = '{studentId}'
                    AND   att.SchoolId  = '{schoolId}'
                    AND   att.Status    IN ('{QuizAttemptStatus.Submitted}','{QuizAttemptStatus.PartiallyGraded}','{QuizAttemptStatus.FullyGraded}')
                    ORDER BY att.SubmittedAt DESC";

				var rows = await _attemptQuery.QueryAsync<StudentQuizHistoryDto>(sql, new Dictionary<string, object>());

				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = $"{rows.Count()} attempt(s) found",
					Status = "successful",
					Data = rows.ToList()
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Error fetching student quiz history");
				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "An error occurred while fetching history",
					Status = "failed"
				};
			}
		}
	}

	// ═════════════════════════════════════════════════════════════════════════
	// PRIVATE HELPERS
	// ═════════════════════════════════════════════════════════════════════════

	private BaseResponse Unauthorized() => new BaseResponse
	{
		ResponseCode = ResponseCode.Unauthorized,
		ResponseMessage = "Invalid authentication",
		Status = "failed"
	};

	private async Task<List<QuizQuestionDetailDto>> FetchQuestionsForAttempt(string quizCode, Guid schoolId)
	{
		var sql = $@"
            SELECT
                q.Id              AS QuestionId,
                q.Title,
                q.TextContent,
                q.QuestionType,
                q.DifficultyLevel,
                q.MarksAllocation,
                s.Subject         AS SubjectName,
                t.Name            AS TopicName,
                qq.DisplayOrder
            FROM   Quiz          qz
            JOIN   QuizQuestion  qq ON qq.QuizId = qz.Id
            JOIN   Questions     q  ON q.Id      = qq.QuestionId
            LEFT JOIN Subjects   s  ON s.Id      = q.SubjectId
            LEFT JOIN Topic      t  ON t.Id      = q.TopicId
            WHERE  qz.Code      = '{quizCode}'
            AND    qz.SchoolId  = '{schoolId}'
            AND    qq.IsActive  = 1
            AND    q.IsActive   = 1
            ORDER  BY qq.DisplayOrder ASC";

		var questions = (await _quizQuery.QueryAsync<QuizQuestionDetailDto>(sql, new Dictionary<string, object>())).ToList();

		foreach (var q in questions)
		{
			if (q.QuestionType == 1) // MultipleChoice
			{
				var optSql = $@"
                    SELECT
                        Id          AS OptionId,
                        OptionLabel,
                        OptionText
                    FROM QuestionOptions
                    WHERE QuestionId = '{q.QuestionId}'
                    AND   IsActive   = 1
                    AND   IsDeleted  = 0
                    ORDER BY OrderIndex ASC";

				var opts = await _quizQuery.QueryAsync<QuizOptionDto>(optSql, new Dictionary<string, object>());
				q.Options = opts.ToList();
			}
		}

		return questions;
	}

	private static List<QuizQuestionDetailDto> ShuffleList(List<QuizQuestionDetailDto> list)
	{
		var rng = new Random();
		var shuffled = list.OrderBy(_ => rng.Next()).ToList();
		return shuffled;
	}

	private static decimal ResolveStarMark(string starMarkConfigJson, int marksAllocation)
	{
		try
		{
			using var doc = JsonDocument.Parse(starMarkConfigJson);
			if (doc.RootElement.TryGetProperty(marksAllocation.ToString(), out var val))
				return val.GetDecimal();
		}
		catch { }
		return marksAllocation;
	}

	// ── Internal DTOs for Dapper projections ─────────────────────────────────
	private class QuestionGradeInfoDto
	{
		public Guid Id { get; set; }
		public int QuestionType { get; set; }
		public int MarksAllocation { get; set; }
	}

	private class AggregateAttemptDto
	{
		public int TotalAttempts { get; set; }
		public int TotalStudents { get; set; }
		public decimal AverageScore { get; set; }
		public decimal PassRate { get; set; }
	}

	private class OptionCorrectDto
	{
		public bool IsCorrect { get; set; }
	}

	private class QuestionCorrectAnswerDto
	{
		public string CorrectAnswer { get; set; } = string.Empty;
	}

	public static class QuizCodeGenerator
	{
		private static readonly char[] Chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray();

		public static string Generate(string prefix = "QZ")
		{
			var random = new Random();
			var code = new char[6];
			for (int i = 0; i < 6; i++)
				code[i] = Chars[random.Next(Chars.Length)];
			return $"{prefix}{new string(code)}";
		}
	}
}
