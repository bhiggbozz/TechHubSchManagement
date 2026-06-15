using Serilog;
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

public class QuizService : IQuizService
{
	private readonly ICommandRespository<Quiz> _quizCommand;
	private readonly ICommandRespository<QuizQuestion> _quizQuestionCommand;
	private readonly IQueryRepository<Quiz> _quizQuery;
	private readonly IQueryRepository<QuizQuestion> _quizQuestionQuery;
	private readonly IQueryRepository<LessonContent> _lessonQuery;
	private readonly ICommandRespository<LessonContent> _lessonCommand;
	private readonly IDbTransactionScopeFactory _scopeFactory;
	private readonly ILogger _logger;

	public QuizService(
		ICommandRespository<Quiz> quizCommand,
		ICommandRespository<QuizQuestion> quizQuestionCommand,
		IQueryRepository<Quiz> quizQuery,
		IQueryRepository<QuizQuestion> quizQuestionQuery,
		IQueryRepository<LessonContent> lessonQuery,
		ICommandRespository<LessonContent> lessonCommand,
		IDbTransactionScopeFactory scopeFactory,
		ILogger logger)
	{
		_quizCommand = quizCommand;
		_quizQuestionCommand = quizQuestionCommand;
		_quizQuery = quizQuery;
		_quizQuestionQuery = quizQuestionQuery;
		_lessonQuery = lessonQuery;
		_lessonCommand = lessonCommand;
		_scopeFactory = scopeFactory;
		_logger = logger;
	}

	// ── Create quiz ───────────────────────────────────────────────────────────
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

			// ── Generate unique code ──────────────────────────────────
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

	// ── Attach quiz to lesson ─────────────────────────────────────────────────
	public async Task<BaseResponse> AttachQuizToLesson(Guid lessonId, AttachQuizViewModel model, AuthenticatedUserClaims claims)
	{
		try
		{
			if (!Guid.TryParse(claims.SchoolId, out var schoolId))
				return Unauthorized();

			// Verify quiz code exists
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

			// Verify lesson exists and belongs to school
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

			// Save quiz code on lesson
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

	// ── Get quiz questions for student ────────────────────────────────────────
	public async Task<BaseResponse> GetQuizByLesson(Guid lessonId, AuthenticatedUserClaims claims)
	{
		try
		{
			if (!Guid.TryParse(claims.SchoolId, out var schoolId))
				return Unauthorized();

			// Get quiz code from lesson
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

			// Fetch questions via code → quiz → questions
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

	private BaseResponse Unauthorized() => new BaseResponse
	{
		ResponseCode = ResponseCode.Unauthorized,
		ResponseMessage = "Invalid authentication",
		Status = "failed"
	};

	public static class QuizCodeGenerator
	{
		private static readonly char[] Chars ="ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray();

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
