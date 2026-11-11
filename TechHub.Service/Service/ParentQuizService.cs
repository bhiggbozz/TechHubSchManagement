using Dapper;
using Microsoft.Data.SqlClient;
using Serilog;
using Serilog.Context;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TechHub.Core;
using TechHub.Core.DTO;
using TechHub.Core.Entities;
using TechHub.Core.Enum;
using TechHub.Core.Model;
using TechHub.Core.ViewModel.ParentQuizModels;
using TechHub.Service.Interface;
using TechHub.Service.Service.DatabaseService;

namespace TechHub.Service.Service
{
	/// <summary>
	/// Practice quizzes a parent sets for their own child. Questions come from the school's
	/// published objective bank (multiple choice / true-false, teacher-visible only — never the
	/// admin-only tier), matched to the child's class and subjects. Everything lives in its own
	/// ParentQuiz* tables, so none of it reaches teacher assessments, rankings or school
	/// performance snapshots.
	/// </summary>
	public class ParentQuizService : IParentQuizService
	{
		// Question types a parent quiz may contain (QuestionType enum): MultipleChoice, TrueOrFalse.
		private const string ObjectiveTypes = "1,4";

		// Mix target: 40% easy, 40% medium, the rest hard/exam-level.
		private const double MixEasyShare = 0.4;
		private const double MixMediumShare = 0.4;

		/// <summary>After the time limit, answers are still accepted for this long (network delay on submit).</summary>
		private static readonly TimeSpan SubmitGrace = TimeSpan.FromMinutes(1);

		private readonly IQueryRepository<ParentQuiz> _query;
		private readonly ICommandRespository<ParentQuiz> _command;
		private readonly IDbTransactionScopeFactory _scopeFactory;
		private readonly IAcademicPeriodResolver _periods;
		private readonly INotificationService _notifications;
		private readonly ILogger _logger;

		public ParentQuizService(
			IQueryRepository<ParentQuiz> query,
			ICommandRespository<ParentQuiz> command,
			IDbTransactionScopeFactory scopeFactory,
			IAcademicPeriodResolver periods,
			INotificationService notifications,
			ILogger logger)
		{
			_query = query;
			_command = command;
			_scopeFactory = scopeFactory;
			_periods = periods;
			_notifications = notifications;
			_logger = logger;
		}

		#region Parent — what can be chosen

		public async Task<BaseResponse> GetChildSubjectsAsync(Guid studentId, AuthenticatedUserClaims claims)
		{
			using (LogContext.PushProperty("RequestedBy", claims?.UserId))
			{
				try
				{
					var caller = RequireCaller(claims, UserRole.Parent, out var denied);
					if (caller is null) return denied!;
					var child = await CheckOwnChildAsync(caller.Value, studentId);
					if (child != null) return child;

					var subjects = await LoadChildSubjectsAsync(studentId, caller.Value.SchoolId);
					var classrooms = await LoadChildClassroomsAsync(studentId, caller.Value.SchoolId);
					if (subjects.Count == 0 || classrooms.Count == 0)
						return Ok("No subjects found for this child", new List<ParentQuizSubjectDto>());

					var counts = (await _query.QueryAsync<CountRow>(
						$@"SELECT q.SubjectId, q.TopicId, q.SubTopicId, {DifficultySql} AS Difficulty, COUNT(*) AS Total
						   FROM Questions q
						   WHERE {PoolFilter(classrooms, topicIds: null)} AND q.SubjectId IN ({GuidList(subjects.Select(s => s.Id))})
						   GROUP BY q.SubjectId, q.TopicId, q.SubTopicId, {DifficultySql}",
						new Dictionary<string, object> { { "SchoolId", caller.Value.SchoolId } })).ToList();

					var topics = counts.Any(c => c.TopicId.HasValue)
						? (await _query.QueryAsync<TopicRow>(
							$"SELECT Id, SubjectId, Name FROM Topic WHERE SchoolId = @SchoolId AND IsActive = 1 AND Id IN ({GuidList(counts.Where(c => c.TopicId.HasValue).Select(c => c.TopicId!.Value).Distinct())}) ORDER BY Name",
							new Dictionary<string, object> { { "SchoolId", caller.Value.SchoolId } })).ToList()
						: new List<TopicRow>();

					var subTopics = counts.Any(c => c.SubTopicId.HasValue)
						? (await _query.QueryAsync<SubTopicRow>(
							$"SELECT Id, TopicId, Name FROM SubTopic WHERE SchoolId = @SchoolId AND IsActive = 1 AND ISNULL(IsDeleted, 0) = 0 AND Id IN ({GuidList(counts.Where(c => c.SubTopicId.HasValue).Select(c => c.SubTopicId!.Value).Distinct())}) ORDER BY Name",
							new Dictionary<string, object> { { "SchoolId", caller.Value.SchoolId } })).ToList()
						: new List<SubTopicRow>();

					var result = subjects.Select(s => new ParentQuizSubjectDto
					{
						SubjectId = s.Id,
						Name = s.Name,
						Available = Tally(counts.Where(c => c.SubjectId == s.Id)),
						Topics = topics.Where(t => t.SubjectId == s.Id)
							.Select(t => new ParentQuizTopicDto
							{
								TopicId = t.Id,
								Name = t.Name,
								Available = Tally(counts.Where(c => c.TopicId == t.Id)),
								SubTopics = subTopics.Where(st => st.TopicId == t.Id)
									.Select(st => new ParentQuizSubTopicDto
									{
										SubTopicId = st.Id,
										Name = st.Name,
										Available = Tally(counts.Where(c => c.SubTopicId == st.Id))
									}).ToList()
							}).ToList()
					}).ToList();

					return Ok("Subjects retrieved", result);
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "ParentQuiz: error listing subjects for student {StudentId}", studentId);
					return Fail(ResponseCode.ErrorOccured, "An error occurred while loading the subjects");
				}
			}
		}

		#endregion

		#region Parent — settings

		public async Task<BaseResponse> GetSettingsAsync(AuthenticatedUserClaims claims)
		{
			try
			{
				var caller = RequireCaller(claims, UserRole.Parent, out var denied);
				if (caller is null) return denied!;
				return Ok("Settings retrieved", ToDto(await LoadConfigAsync(caller.Value.UserId, caller.Value.SchoolId)));
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "ParentQuiz: error loading settings");
				return Fail(ResponseCode.ErrorOccured, "An error occurred while loading your settings");
			}
		}

		public async Task<BaseResponse> SaveSettingsAsync(ParentQuizSettingsViewModel model, AuthenticatedUserClaims claims)
		{
			using (LogContext.PushProperty("RequestedBy", claims?.UserId))
			{
				try
				{
					var caller = RequireCaller(claims, UserRole.Parent, out var denied);
					if (caller is null) return denied!;
					if (model is null) return Fail(ResponseCode.BadRequest, "Request body is required");

					var mode = NormaliseMode(model.Mode);
					var error = ValidateSettings(model.QuestionCount, mode, model.TimeLimitMinutes, model.PassMarkPercent);
					if (error != null) return Fail(ResponseCode.BadRequest, error);

					var time = model.TimeLimitMinutes is > 0 ? model.TimeLimitMinutes : null;
					var args = new Dictionary<string, object>
					{
						{ "ParentId", caller.Value.UserId }, { "SchoolId", caller.Value.SchoolId },
						{ "Count", model.QuestionCount }, { "Mode", mode! }, { "Time", time! },
						{ "Pass", model.PassMarkPercent }, { "Shuffle", model.ShuffleQuestions }
					};

					await _command.UpdateAsync(
						@"IF EXISTS (SELECT 1 FROM ParentQuizConfig WHERE ParentId = @ParentId AND SchoolId = @SchoolId)
						      UPDATE ParentQuizConfig SET QuestionCount = @Count, Mode = @Mode, TimeLimitMinutes = @Time,
						             PassMarkPercent = @Pass, ShuffleQuestions = @Shuffle, UpdatedAt = GETUTCDATE()
						      WHERE ParentId = @ParentId AND SchoolId = @SchoolId
						  ELSE
						      INSERT INTO ParentQuizConfig (Id, SchoolId, ParentId, QuestionCount, Mode, TimeLimitMinutes, PassMarkPercent, ShuffleQuestions)
						      VALUES (NEWID(), @SchoolId, @ParentId, @Count, @Mode, @Time, @Pass, @Shuffle)",
						args);

					return Ok("Settings saved", ToDto(await LoadConfigAsync(caller.Value.UserId, caller.Value.SchoolId)));
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "ParentQuiz: error saving settings");
					return Fail(ResponseCode.ErrorOccured, "An error occurred while saving your settings");
				}
			}
		}

		#endregion

		#region Parent — create / list / review / cancel

		public async Task<BaseResponse> CreateAsync(CreateParentQuizViewModel model, AuthenticatedUserClaims claims)
		{
			using (LogContext.PushProperty("RequestedBy", claims?.UserId))
			{
				try
				{
					var caller = RequireCaller(claims, UserRole.Parent, out var denied);
					if (caller is null) return denied!;
					var (parentId, schoolId) = (caller.Value.UserId, caller.Value.SchoolId);
					if (model is null) return Fail(ResponseCode.BadRequest, "Request body is required");

					var notChild = await CheckOwnChildAsync(caller.Value, model.StudentId);
					if (notChild != null) return notChild;

					// ── Settings: the request overrides the parent's saved defaults ─────────────────────
					var saved = await LoadConfigAsync(parentId, schoolId);
					var count = model.QuestionCount ?? saved?.QuestionCount ?? ParentQuizDefaults.QuestionCount;
					var mode = NormaliseMode(model.Mode ?? saved?.Mode ?? ParentQuizModes.Mix);
					var timeLimit = model.TimeLimitMinutes.HasValue ? model.TimeLimitMinutes : saved?.TimeLimitMinutes;
					var passMark = model.PassMarkPercent ?? saved?.PassMarkPercent ?? ParentQuizDefaults.PassMarkPercent;
					var shuffle = saved?.ShuffleQuestions ?? true;
					var settingsError = ValidateSettings(count, mode, timeLimit, passMark);
					if (settingsError != null) return Fail(ResponseCode.BadRequest, settingsError);
					if (timeLimit is <= 0) timeLimit = null;

					// ── Subject must be one of the child's; topics must belong to it ─────────────────────
					var subjects = await LoadChildSubjectsAsync(model.StudentId, schoolId);
					var subject = subjects.FirstOrDefault(s => s.Id == model.SubjectId);
					if (subject is null)
						return Fail(ResponseCode.BadRequest, "That subject isn't one of your child's subjects");

					var topicIds = (model.TopicIds ?? new List<Guid>()).Where(t => t != Guid.Empty).Distinct().ToList();
					var subTopicIds = (model.SubTopicIds ?? new List<Guid>()).Where(t => t != Guid.Empty).Distinct().ToList();
					var pickedNames = new List<string>();
					string? topicNames = null;
					if (topicIds.Count > 0)
					{
						var topics = (await _query.QueryAsync<TopicRow>(
							$"SELECT Id, SubjectId, Name FROM Topic WHERE SchoolId = @SchoolId AND SubjectId = @SubjectId AND IsActive = 1 AND Id IN ({GuidList(topicIds)}) ORDER BY Name",
							new Dictionary<string, object> { { "SchoolId", schoolId }, { "SubjectId", model.SubjectId } })).ToList();
						if (topics.Count != topicIds.Count)
							return Fail(ResponseCode.BadRequest, "One or more topics don't belong to that subject");
						pickedNames.AddRange(topics.Select(t => t.Name));
					}
					if (subTopicIds.Count > 0)
					{
						var subTopics = (await _query.QueryAsync<SubTopicRow>(
							$@"SELECT st.Id, st.TopicId, st.Name FROM SubTopic st JOIN Topic t ON t.Id = st.TopicId
							   WHERE st.SchoolId = @SchoolId AND st.IsActive = 1 AND ISNULL(st.IsDeleted, 0) = 0
							   AND t.SubjectId = @SubjectId AND st.Id IN ({GuidList(subTopicIds)}) ORDER BY st.Name",
							new Dictionary<string, object> { { "SchoolId", schoolId }, { "SubjectId", model.SubjectId } })).ToList();
						if (subTopics.Count != subTopicIds.Count)
							return Fail(ResponseCode.BadRequest, "One or more sub-topics don't belong to that subject");
						pickedNames.AddRange(subTopics.Select(t => t.Name));
					}
					if (pickedNames.Count > 0)
						topicNames = Truncate(string.Join(", ", pickedNames), 1000);

					// ── Limits ───────────────────────────────────────────────────────────────────────────
					var pending = (await _query.QueryAsync<int>(
						"SELECT COUNT(*) FROM ParentQuiz WHERE ParentId = @ParentId AND StudentId = @StudentId AND Status = 'Pending' AND IsActive = 1",
						new Dictionary<string, object> { { "ParentId", parentId }, { "StudentId", model.StudentId } })).FirstOrDefault();
					if (pending >= ParentQuizDefaults.MaxPendingPerChild)
						return Fail(ResponseCode.BadRequest, $"Your child already has {pending} quizzes waiting. Let them finish a few, or cancel some, before setting more.");

					var today = (await _query.QueryAsync<int>(
						"SELECT COUNT(*) FROM ParentQuiz WHERE ParentId = @ParentId AND CreatedAt > @Since",
						new Dictionary<string, object> { { "ParentId", parentId }, { "Since", DateTime.UtcNow.AddHours(-24) } })).FirstOrDefault();
					if (today >= ParentQuizDefaults.MaxCreatedPerParentPerDay)
						return Fail(ResponseCode.BadRequest, "You've reached today's limit for new quizzes. Please try again tomorrow.");

					// ── Pick the questions ───────────────────────────────────────────────────────────────
					var classrooms = await LoadChildClassroomsAsync(model.StudentId, schoolId);
					if (classrooms.Count == 0)
						return Fail(ResponseCode.BadRequest, "Your child isn't in a class yet, so a quiz can't be set.");

					var pool = (await _query.QueryAsync<PoolRow>(
						$@"SELECT q.Id AS QuestionId, {DifficultySql} AS Difficulty,
						          CASE WHEN EXISTS (SELECT 1 FROM ParentQuizQuestion pq JOIN ParentQuiz pz ON pz.Id = pq.QuizId
						                            WHERE pq.QuestionId = q.Id AND pz.StudentId = @StudentId AND pz.Status <> 'Cancelled') THEN 1 ELSE 0 END AS Seen
						   FROM Questions q
						   WHERE {PoolFilter(classrooms, topicIds, subTopicIds)} AND q.SubjectId = @SubjectId",
						new Dictionary<string, object> { { "SchoolId", schoolId }, { "SubjectId", model.SubjectId }, { "StudentId", model.StudentId } })).ToList();

					if (pool.Count < ParentQuizDefaults.MinQuestions)
						return Fail(ResponseCode.BadRequest,
							pool.Count == 0
								? "There are no questions available for that choice yet. Try another subject or topic."
								: $"Only {pool.Count} question(s) are available for that choice; at least {ParentQuizDefaults.MinQuestions} are needed. Try a wider topic or the whole subject.");

					var picked = PickQuestions(pool, Math.Min(count, pool.Count), mode!, shuffle);
					string? notice = null;
					if (picked.Count < count)
						notice = $"Only {picked.Count} questions were available, so your quiz has {picked.Count}.";
					else if (mode != ParentQuizModes.Mix && picked.Any(p => p.Difficulty != DifficultyOf(mode!)))
						notice = $"There weren't enough {mode} questions, so the rest come from the nearest difficulty.";

					// ── Save ─────────────────────────────────────────────────────────────────────────────
					var title = string.IsNullOrWhiteSpace(model.Title)
						? Truncate(topicNames != null ? $"{subject.Name}: {topicNames}" : $"{subject.Name} practice quiz", 200)
						: Truncate(model.Title.Trim(), 200);
					var quizId = Guid.NewGuid();
					var period = await _periods.ResolveNowAsync(schoolId);

					using (var scope = _scopeFactory.Create("DbConnectionString"))
					{
						try
						{
							await scope.Connection.ExecuteAsync(
								@"INSERT INTO ParentQuiz (Id, SchoolId, ParentId, StudentId, SubjectId, SubjectName, TopicNames, Title, Mode,
								      QuestionCount, TimeLimitMinutes, PassMarkPercent, Status, SessionId, TermId)
								  VALUES (@Id, @SchoolId, @ParentId, @StudentId, @SubjectId, @SubjectName, @TopicNames, @Title, @Mode,
								      @QuestionCount, @TimeLimitMinutes, @PassMarkPercent, 'Pending', @SessionId, @TermId)",
								new
								{
									Id = quizId, SchoolId = schoolId, ParentId = parentId, model.StudentId, model.SubjectId,
									SubjectName = subject.Name, TopicNames = topicNames, Title = title, Mode = mode,
									QuestionCount = picked.Count, TimeLimitMinutes = timeLimit, PassMarkPercent = passMark,
									period.SessionId, period.TermId
								}, scope.Transaction);

							await scope.Connection.ExecuteAsync(
								"INSERT INTO ParentQuizQuestion (Id, QuizId, QuestionId, DisplayOrder, DifficultyLevel) VALUES (NEWID(), @QuizId, @QuestionId, @DisplayOrder, @DifficultyLevel)",
								picked.Select((p, i) => new { QuizId = quizId, p.QuestionId, DisplayOrder = i + 1, DifficultyLevel = p.Difficulty }),
								scope.Transaction);

							await scope.CommitAsync();
						}
						catch
						{
							try { await scope.RollbackAsync(); } catch { }
							throw;
						}
					}

					_logger.Information("ParentQuiz: quiz {QuizId} ({Count} questions, {Mode}) set by parent {ParentId} for student {StudentId}",
						quizId, picked.Count, mode, parentId, model.StudentId);

					await _notifications.NotifyParentQuizAssignedAsync(quizId, model.StudentId, schoolId, title);

					var summary = (await LoadSummariesAsync("z.Id = @QuizId", new Dictionary<string, object> { { "QuizId", quizId } })).First();
					return Ok("Quiz created", new ParentQuizCreatedDto { Quiz = summary, Notice = notice });
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "ParentQuiz: error creating quiz");
					return Fail(ResponseCode.ErrorOccured, "An error occurred while creating the quiz");
				}
			}
		}

		public async Task<BaseResponse> ListForParentAsync(Guid? studentId, AuthenticatedUserClaims claims)
		{
			try
			{
				var caller = RequireCaller(claims, UserRole.Parent, out var denied);
				if (caller is null) return denied!;

				var args = new Dictionary<string, object> { { "ParentId", caller.Value.UserId }, { "SchoolId", caller.Value.SchoolId } };
				var where = "z.ParentId = @ParentId AND z.SchoolId = @SchoolId";
				if (studentId.HasValue)
				{
					var notChild = await CheckOwnChildAsync(caller.Value, studentId.Value);
					if (notChild != null) return notChild;
					where += " AND z.StudentId = @StudentId";
					args["StudentId"] = studentId.Value;
				}

				return Ok("Quizzes retrieved", await LoadSummariesAsync(where, args));
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "ParentQuiz: error listing parent quizzes");
				return Fail(ResponseCode.ErrorOccured, "An error occurred while loading the quizzes");
			}
		}

		public async Task<BaseResponse> GetResultForParentAsync(Guid quizId, AuthenticatedUserClaims claims)
		{
			try
			{
				var caller = RequireCaller(claims, UserRole.Parent, out var denied);
				if (caller is null) return denied!;

				var summary = (await LoadSummariesAsync("z.Id = @QuizId AND z.ParentId = @ParentId AND z.SchoolId = @SchoolId",
					new Dictionary<string, object> { { "QuizId", quizId }, { "ParentId", caller.Value.UserId }, { "SchoolId", caller.Value.SchoolId } }))
					.FirstOrDefault();
				if (summary is null) return Fail(ResponseCode.NotFound, "Quiz not found");
				if (summary.Status != ParentQuizStatus.Completed)
					return Fail(ResponseCode.BadRequest, "The review is available once your child has finished the quiz.");

				return Ok("Result retrieved", await BuildResultAsync(summary));
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "ParentQuiz: error loading result for quiz {QuizId}", quizId);
				return Fail(ResponseCode.ErrorOccured, "An error occurred while loading the result");
			}
		}

		public async Task<BaseResponse> CancelAsync(Guid quizId, AuthenticatedUserClaims claims)
		{
			using (LogContext.PushProperty("RequestedBy", claims?.UserId))
			{
				try
				{
					var caller = RequireCaller(claims, UserRole.Parent, out var denied);
					if (caller is null) return denied!;

					var changed = (await _query.QueryAsync<int>(
						@"UPDATE ParentQuiz SET Status = 'Cancelled'
						  WHERE Id = @QuizId AND ParentId = @ParentId AND SchoolId = @SchoolId AND Status = 'Pending' AND IsActive = 1;
						  SELECT @@ROWCOUNT;",
						new Dictionary<string, object> { { "QuizId", quizId }, { "ParentId", caller.Value.UserId }, { "SchoolId", caller.Value.SchoolId } })).FirstOrDefault();
					if (changed > 0) return Ok("Quiz cancelled", null);

					var exists = (await _query.QueryAsync<int>(
						"SELECT COUNT(*) FROM ParentQuiz WHERE Id = @QuizId AND ParentId = @ParentId AND SchoolId = @SchoolId AND IsActive = 1",
						new Dictionary<string, object> { { "QuizId", quizId }, { "ParentId", caller.Value.UserId }, { "SchoolId", caller.Value.SchoolId } })).FirstOrDefault();
					return exists == 0
						? Fail(ResponseCode.NotFound, "Quiz not found")
						: Fail(ResponseCode.BadRequest, "Only quizzes your child hasn't started can be cancelled.");
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "ParentQuiz: error cancelling quiz {QuizId}", quizId);
					return Fail(ResponseCode.ErrorOccured, "An error occurred while cancelling the quiz");
				}
			}
		}

		#endregion

		#region Student

		public async Task<BaseResponse> ListForStudentAsync(AuthenticatedUserClaims claims)
		{
			try
			{
				var caller = RequireCaller(claims, UserRole.Student, out var denied);
				if (caller is null) return denied!;
				return Ok("Quizzes retrieved", await LoadSummariesAsync("z.StudentId = @StudentId AND z.SchoolId = @SchoolId",
					new Dictionary<string, object> { { "StudentId", caller.Value.UserId }, { "SchoolId", caller.Value.SchoolId } }));
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "ParentQuiz: error listing quizzes for student");
				return Fail(ResponseCode.ErrorOccured, "An error occurred while loading your quizzes");
			}
		}

		public async Task<BaseResponse> StartAsync(Guid quizId, AuthenticatedUserClaims claims)
		{
			using (LogContext.PushProperty("RequestedBy", claims?.UserId))
			{
				try
				{
					var caller = RequireCaller(claims, UserRole.Student, out var denied);
					if (caller is null) return denied!;
					var (studentId, schoolId) = (caller.Value.UserId, caller.Value.SchoolId);

					var quiz = await LoadQuizAsync(quizId, studentId, schoolId);
					if (quiz is null) return Fail(ResponseCode.NotFound, "Quiz not found");
					if (quiz.Status == ParentQuizStatus.Cancelled)
						return Fail(ResponseCode.BadRequest, "Your parent cancelled this quiz.");
					if (quiz.Status == ParentQuizStatus.Completed)
						return Fail(ResponseCode.BadRequest, "You've already finished this quiz. Open it to see your result.");

					var attempt = await LoadAttemptAsync(quizId);
					if (attempt is null)
					{
						try
						{
							using var scope = _scopeFactory.Create("DbConnectionString");
							try
							{
								await scope.Connection.ExecuteAsync(
									@"INSERT INTO ParentQuizAttempt (Id, QuizId, StudentId, SchoolId, Status, StartedAt, TotalQuestions)
									  VALUES (NEWID(), @QuizId, @StudentId, @SchoolId, 'InProgress', GETUTCDATE(), @Total);
									  UPDATE ParentQuiz SET Status = 'InProgress' WHERE Id = @QuizId AND Status = 'Pending';",
									new { QuizId = quizId, StudentId = studentId, SchoolId = schoolId, Total = quiz.QuestionCount }, scope.Transaction);
								await scope.CommitAsync();
							}
							catch
							{
								try { await scope.RollbackAsync(); } catch { }
								throw;
							}
						}
						catch (SqlException ex) when (ex.Number is 2601 or 2627)
						{
							// A double-tap started it twice; the unique index kept it to one attempt — use that one.
						}
						attempt = await LoadAttemptAsync(quizId);
					}
					if (attempt is null) return Fail(ResponseCode.ErrorOccured, "Could not start the quiz");
					if (attempt.Status == ParentQuizAttemptStatus.Submitted)
						return Fail(ResponseCode.BadRequest, "You've already finished this quiz. Open it to see your result.");

					// Resuming after the time has run out: nothing was saved, so it ends with every question unanswered.
					if (quiz.TimeLimitMinutes.HasValue
						&& DateTime.UtcNow - attempt.StartedAt > TimeSpan.FromMinutes(quiz.TimeLimitMinutes.Value) + SubmitGrace)
					{
						await FinishAsync(quiz, attempt, new List<ParentQuizAnswerInput>());
						return Fail(ResponseCode.BadRequest, "The time for this quiz ran out. Open it to see your result.");
					}

					return Ok("Quiz started", new ParentQuizStartDto
					{
						QuizId = quiz.Id,
						AttemptId = attempt.Id,
						Title = quiz.Title,
						TimeLimitMinutes = quiz.TimeLimitMinutes,
						StartedAt = Utc(attempt.StartedAt),
						Questions = await LoadQuestionsForSittingAsync(quizId)
					});
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "ParentQuiz: error starting quiz {QuizId}", quizId);
					return Fail(ResponseCode.ErrorOccured, "An error occurred while starting the quiz");
				}
			}
		}

		public async Task<BaseResponse> SubmitAsync(Guid quizId, SubmitParentQuizViewModel model, AuthenticatedUserClaims claims)
		{
			using (LogContext.PushProperty("RequestedBy", claims?.UserId))
			{
				try
				{
					var caller = RequireCaller(claims, UserRole.Student, out var denied);
					if (caller is null) return denied!;

					var quiz = await LoadQuizAsync(quizId, caller.Value.UserId, caller.Value.SchoolId);
					if (quiz is null) return Fail(ResponseCode.NotFound, "Quiz not found");
					if (quiz.Status == ParentQuizStatus.Cancelled)
						return Fail(ResponseCode.BadRequest, "Your parent cancelled this quiz.");

					var attempt = await LoadAttemptAsync(quizId);
					if (attempt is null)
						return Fail(ResponseCode.BadRequest, "Start the quiz before submitting it.");
					if (attempt.Status == ParentQuizAttemptStatus.Submitted)
						return Fail(ResponseCode.Conflict, "This quiz has already been submitted.");

					var finished = await FinishAsync(quiz, attempt, model?.Answers ?? new List<ParentQuizAnswerInput>());
					if (!finished)
						return Fail(ResponseCode.Conflict, "This quiz has already been submitted.");

					var summary = (await LoadSummariesAsync("z.Id = @QuizId", new Dictionary<string, object> { { "QuizId", quizId } })).First();
					return Ok("Quiz submitted", await BuildResultAsync(summary));
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "ParentQuiz: error submitting quiz {QuizId}", quizId);
					return Fail(ResponseCode.ErrorOccured, "An error occurred while submitting the quiz");
				}
			}
		}

		public async Task<BaseResponse> GetResultForStudentAsync(Guid quizId, AuthenticatedUserClaims claims)
		{
			try
			{
				var caller = RequireCaller(claims, UserRole.Student, out var denied);
				if (caller is null) return denied!;

				var summary = (await LoadSummariesAsync("z.Id = @QuizId AND z.StudentId = @StudentId AND z.SchoolId = @SchoolId",
					new Dictionary<string, object> { { "QuizId", quizId }, { "StudentId", caller.Value.UserId }, { "SchoolId", caller.Value.SchoolId } }))
					.FirstOrDefault();
				if (summary is null) return Fail(ResponseCode.NotFound, "Quiz not found");
				if (summary.Status != ParentQuizStatus.Completed)
					return Fail(ResponseCode.BadRequest, "Finish the quiz to see your result.");

				return Ok("Result retrieved", await BuildResultAsync(summary));
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "ParentQuiz: error loading result for quiz {QuizId}", quizId);
				return Fail(ResponseCode.ErrorOccured, "An error occurred while loading the result");
			}
		}

		#endregion

		#region Question selection

		private const string DifficultySql = "CASE WHEN q.DifficultyLevel BETWEEN 1 AND 4 THEN q.DifficultyLevel ELSE 2 END";

		/// <summary>
		/// The questions a parent quiz may draw from: published, active, teacher-visible (never the
		/// admin-only tier), objective with at least two live options of which one is correct, in
		/// the child's class, optionally narrowed to topics. Caller adds the subject condition.
		/// Ids are inlined as formatted Guids (injection-proof) to avoid list-parameter expansion.
		/// </summary>
		private static string PoolFilter(IEnumerable<Guid> classrooms, IEnumerable<Guid>? topicIds, IEnumerable<Guid>? subTopicIds = null)
			=> $@"q.SchoolId = @SchoolId AND q.Status = 1 AND q.IsActive = 1 AND ISNULL(q.IsDeleted, 0) = 0
			      AND ISNULL(q.IsAdminOnly, 0) = 0 AND q.QuestionType IN ({ObjectiveTypes})
			      AND q.ClassroomId IN ({GuidList(classrooms)})
			      {TopicScope(topicIds, subTopicIds)}
			      AND EXISTS (SELECT 1 FROM QuestionOptions o WHERE o.QuestionId = q.Id AND o.IsCorrect = 1 AND ISNULL(o.IsActive, 1) = 1 AND ISNULL(o.IsDeleted, 0) = 0)
			      AND (SELECT COUNT(*) FROM QuestionOptions o2 WHERE o2.QuestionId = q.Id AND ISNULL(o2.IsActive, 1) = 1 AND ISNULL(o2.IsDeleted, 0) = 0) >= 2";

		/// <summary>Topics and sub-topics the parent picked: a question qualifies if it is in either set.</summary>
		private static string TopicScope(IEnumerable<Guid>? topicIds, IEnumerable<Guid>? subTopicIds)
		{
			var hasTopics = topicIds != null && topicIds.Any();
			var hasSubTopics = subTopicIds != null && subTopicIds.Any();
			if (hasTopics && hasSubTopics)
				return $"AND (q.TopicId IN ({GuidList(topicIds!)}) OR q.SubTopicId IN ({GuidList(subTopicIds!)}))";
			if (hasTopics) return $"AND q.TopicId IN ({GuidList(topicIds!)})";
			if (hasSubTopics) return $"AND q.SubTopicId IN ({GuidList(subTopicIds!)})";
			return string.Empty;
		}

		private static string GuidList(IEnumerable<Guid> ids)
		{
			var list = ids.Select(g => $"'{g:D}'").ToList();
			return list.Count == 0 ? $"'{Guid.Empty:D}'" : string.Join(",", list);
		}

		private static int DifficultyOf(string mode) => mode switch
		{
			ParentQuizModes.Easy => 1,
			ParentQuizModes.Medium => 2,
			ParentQuizModes.Hard => 3,
			ParentQuizModes.ExamLevel => 4,
			_ => 2
		};

		/// <summary>
		/// Picks <paramref name="count"/> questions. Questions the child has not yet had in a parent
		/// quiz are preferred; within that, selection is random. A single difficulty takes the
		/// closest levels when it runs short; Mix takes 40/40/20 easy/medium/hard-or-exam and tops
		/// up from whatever is left.
		/// </summary>
		private static List<PoolRow> PickQuestions(List<PoolRow> pool, int count, string mode, bool shuffle)
		{
			var rng = Random.Shared;
			List<PoolRow> picked;

			if (mode == ParentQuizModes.Mix)
			{
				var easy = (int)Math.Round(count * MixEasyShare);
				var medium = (int)Math.Round(count * MixMediumShare);
				var hard = Math.Max(0, count - easy - medium);

				picked = new List<PoolRow>();
				picked.AddRange(Take(pool.Where(p => p.Difficulty == 1), easy, rng));
				picked.AddRange(Take(pool.Where(p => p.Difficulty == 2), medium, rng));
				picked.AddRange(Take(pool.Where(p => p.Difficulty >= 3), hard, rng));

				if (picked.Count < count)
				{
					var taken = picked.Select(p => p.QuestionId).ToHashSet();
					picked.AddRange(Take(pool.Where(p => !taken.Contains(p.QuestionId)), count - picked.Count, rng));
				}
			}
			else
			{
				var wanted = DifficultyOf(mode);
				picked = pool
					.OrderBy(p => Math.Abs(p.Difficulty - wanted))
					.ThenBy(p => p.Seen)
					.ThenBy(_ => rng.Next())
					.Take(count)
					.ToList();
			}

			// Shuffle on: random order. Off: gentle ramp from easy to hard.
			return shuffle
				? picked.OrderBy(_ => rng.Next()).ToList()
				: picked.OrderBy(p => p.Difficulty).ToList();
		}

		private static IEnumerable<PoolRow> Take(IEnumerable<PoolRow> source, int n, Random rng)
			=> n <= 0 ? Enumerable.Empty<PoolRow>() : source.OrderBy(p => p.Seen).ThenBy(_ => rng.Next()).Take(n);

		#endregion

		#region Marking and results

		/// <summary>
		/// Grades the submitted answers and closes the attempt and quiz in one transaction.
		/// False when someone else (a double submit) closed it first.
		/// </summary>
		private async Task<bool> FinishAsync(ParentQuiz quiz, ParentQuizAttempt attempt, List<ParentQuizAnswerInput> submitted)
		{
			var questionIds = (await _query.QueryAsync<Guid>(
				"SELECT QuestionId FROM ParentQuizQuestion WHERE QuizId = @QuizId ORDER BY DisplayOrder",
				new Dictionary<string, object> { { "QuizId", quiz.Id } })).ToList();

			// Last answer per question wins; ignore questions that aren't part of this quiz.
			var chosen = submitted
				.Where(a => questionIds.Contains(a.QuestionId))
				.GroupBy(a => a.QuestionId)
				.ToDictionary(g => g.Key, g => g.Last().SelectedOptionId);

			// The option must belong to its question and still be live; the correct flag is read here, never trusted from the client.
			var optionRows = (await _query.QueryAsync<OptionCheckRow>(
				$@"SELECT o.Id AS OptionId, o.QuestionId, o.IsCorrect
				   FROM QuestionOptions o
				   WHERE o.QuestionId IN ({GuidList(questionIds)}) AND ISNULL(o.IsActive, 1) = 1 AND ISNULL(o.IsDeleted, 0) = 0",
				new Dictionary<string, object>())).ToList();
			var optionLookup = optionRows.ToDictionary(o => o.OptionId);

			var answers = new List<ParentQuizAnswer>();
			int correct = 0, wrong = 0, skipped = 0;
			foreach (var questionId in questionIds)
			{
				chosen.TryGetValue(questionId, out var selected);
				var valid = selected.HasValue && optionLookup.TryGetValue(selected.Value, out var opt) && opt.QuestionId == questionId;
				var isCorrect = valid && optionLookup[selected!.Value].IsCorrect;

				if (!valid) skipped++;
				else if (isCorrect) correct++;
				else wrong++;

				answers.Add(new ParentQuizAnswer
				{
					AttemptId = attempt.Id,
					QuestionId = questionId,
					SelectedOptionId = valid ? selected : null,
					IsCorrect = isCorrect,
					IsSkipped = !valid
				});
			}

			var total = questionIds.Count;
			var score = total == 0 ? 0m : Math.Round(correct * 100m / total, 2);
			var passed = score >= quiz.PassMarkPercent;
			var taken = (int)Math.Max(0, (DateTime.UtcNow - attempt.StartedAt).TotalSeconds);

			using (var scope = _scopeFactory.Create("DbConnectionString"))
			{
				try
				{
					var closed = await scope.Connection.ExecuteScalarAsync<int>(
						@"UPDATE ParentQuizAttempt
						  SET Status = 'Submitted', SubmittedAt = GETUTCDATE(), TimeTakenSeconds = @Taken, TotalQuestions = @Total,
						      TotalCorrect = @Correct, TotalWrong = @Wrong, TotalSkipped = @Skipped, ScorePercent = @Score, IsPassed = @Passed
						  WHERE Id = @AttemptId AND Status = 'InProgress';
						  SELECT @@ROWCOUNT;",
						new { AttemptId = attempt.Id, Taken = taken, Total = total, Correct = correct, Wrong = wrong, Skipped = skipped, Score = score, Passed = passed },
						scope.Transaction);

					if (closed == 0)
					{
						await scope.RollbackAsync();
						return false;
					}

					await scope.Connection.ExecuteAsync(
						"INSERT INTO ParentQuizAnswer (Id, AttemptId, QuestionId, SelectedOptionId, IsCorrect, IsSkipped) VALUES (NEWID(), @AttemptId, @QuestionId, @SelectedOptionId, @IsCorrect, @IsSkipped)",
						answers.Select(a => new { a.AttemptId, a.QuestionId, a.SelectedOptionId, a.IsCorrect, a.IsSkipped }),
						scope.Transaction);

					await scope.Connection.ExecuteAsync(
						"UPDATE ParentQuiz SET Status = 'Completed', CompletedAt = GETUTCDATE() WHERE Id = @QuizId",
						new { QuizId = quiz.Id }, scope.Transaction);

					await scope.CommitAsync();
				}
				catch
				{
					try { await scope.RollbackAsync(); } catch { }
					throw;
				}
			}

			_logger.Information("ParentQuiz: quiz {QuizId} submitted by student {StudentId}: {Correct}/{Total} ({Score}%)",
				quiz.Id, quiz.StudentId, correct, total, score);

			var studentName = (await _query.QueryAsync<string>(
				"SELECT LTRIM(RTRIM(ISNULL(FirstName, '') + ' ' + ISNULL(LastName, ''))) FROM Users WHERE Id = @Id",
				new Dictionary<string, object> { { "Id", quiz.StudentId } })).FirstOrDefault() ?? "Your child";
			await _notifications.NotifyParentQuizCompletedAsync(quiz.Id, quiz.ParentId, quiz.SchoolId, studentName, quiz.Title, score);
			return true;
		}

		private async Task<ParentQuizResultDto> BuildResultAsync(ParentQuizSummaryDto summary)
		{
			var attempt = await LoadAttemptAsync(summary.Id);

			var questions = (await _query.QueryAsync<QuestionRow>(
				@"SELECT pq.QuestionId, pq.DisplayOrder, q.Title, q.TextContent, q.QuestionHtml, q.ContentParts, q.HasLatex, q.ImageUrl,
				         ISNULL(q.QuestionType, 1) AS QuestionType, pq.DifficultyLevel
				  FROM ParentQuizQuestion pq JOIN Questions q ON q.Id = pq.QuestionId
				  WHERE pq.QuizId = @QuizId ORDER BY pq.DisplayOrder",
				new Dictionary<string, object> { { "QuizId", summary.Id } })).ToList();

			var options = await LoadOptionsAsync(questions.Select(q => q.QuestionId));

			var given = attempt == null
				? new Dictionary<Guid, AnswerRow>()
				: (await _query.QueryAsync<AnswerRow>(
					"SELECT QuestionId, SelectedOptionId, IsCorrect, IsSkipped FROM ParentQuizAnswer WHERE AttemptId = @AttemptId",
					new Dictionary<string, object> { { "AttemptId", attempt.Id } })).ToDictionary(a => a.QuestionId);

			return new ParentQuizResultDto
			{
				Quiz = summary,
				TotalQuestions = attempt?.TotalQuestions ?? summary.QuestionCount,
				TotalCorrect = attempt?.TotalCorrect ?? 0,
				TotalWrong = attempt?.TotalWrong ?? 0,
				TotalSkipped = attempt?.TotalSkipped ?? 0,
				TimeTakenSeconds = attempt?.TimeTakenSeconds,
				Questions = questions.Select(q =>
				{
					given.TryGetValue(q.QuestionId, out var answer);
					var opts = options.TryGetValue(q.QuestionId, out var list) ? list : new List<OptionRow>();
					return new ParentQuizReviewItemDto
					{
						QuestionId = q.QuestionId,
						DisplayOrder = q.DisplayOrder,
						Title = q.Title,
						TextContent = q.TextContent,
						QuestionHtml = q.QuestionHtml,
						ContentParts = q.ContentParts,
						HasLatex = q.HasLatex,
						ImageUrl = q.ImageUrl,
						DifficultyLevel = q.DifficultyLevel,
						IsCorrect = answer?.IsCorrect ?? false,
						IsSkipped = answer?.IsSkipped ?? true,
						SelectedOptionId = answer?.SelectedOptionId,
						CorrectOptionId = opts.FirstOrDefault(o => o.IsCorrect)?.OptionId,
						Options = opts.Select(o => new ParentQuizReviewOptionDto
						{
							OptionId = o.OptionId,
							OptionLabel = o.OptionLabel,
							OptionText = o.OptionText,
							OptionHtml = o.OptionHtml,
							ContentParts = o.ContentParts,
							HasLatex = o.HasLatex,
							IsCorrect = o.IsCorrect,
							WasSelected = answer?.SelectedOptionId == o.OptionId
						}).ToList()
					};
				}).ToList()
			};
		}

		private async Task<List<ParentQuizQuestionDto>> LoadQuestionsForSittingAsync(Guid quizId)
		{
			var questions = (await _query.QueryAsync<QuestionRow>(
				@"SELECT pq.QuestionId, pq.DisplayOrder, q.Title, q.TextContent, q.QuestionHtml, q.ContentParts, q.HasLatex, q.ImageUrl,
				         ISNULL(q.QuestionType, 1) AS QuestionType, pq.DifficultyLevel
				  FROM ParentQuizQuestion pq JOIN Questions q ON q.Id = pq.QuestionId
				  WHERE pq.QuizId = @QuizId ORDER BY pq.DisplayOrder",
				new Dictionary<string, object> { { "QuizId", quizId } })).ToList();

			var options = await LoadOptionsAsync(questions.Select(q => q.QuestionId));

			// The correct flag is never sent while the child is sitting the quiz.
			return questions.Select(q => new ParentQuizQuestionDto
			{
				QuestionId = q.QuestionId,
				DisplayOrder = q.DisplayOrder,
				Title = q.Title,
				TextContent = q.TextContent,
				QuestionHtml = q.QuestionHtml,
				ContentParts = q.ContentParts,
				HasLatex = q.HasLatex,
				ImageUrl = q.ImageUrl,
				QuestionType = q.QuestionType,
				DifficultyLevel = q.DifficultyLevel,
				Options = (options.TryGetValue(q.QuestionId, out var list) ? list : new List<OptionRow>())
					.Select(o => new ParentQuizOptionDto
					{
						OptionId = o.OptionId,
						OptionLabel = o.OptionLabel,
						OptionText = o.OptionText,
						OptionHtml = o.OptionHtml,
						ContentParts = o.ContentParts,
						HasLatex = o.HasLatex,
						OrderIndex = o.OrderIndex
					}).ToList()
			}).ToList();
		}

		private async Task<Dictionary<Guid, List<OptionRow>>> LoadOptionsAsync(IEnumerable<Guid> questionIds)
		{
			var ids = questionIds.ToList();
			if (ids.Count == 0) return new Dictionary<Guid, List<OptionRow>>();

			var rows = await _query.QueryAsync<OptionRow>(
				$@"SELECT QuestionId, Id AS OptionId, OptionLabel, OptionText, OptionHtml, ContentParts, HasLatex, OrderIndex, ISNULL(IsCorrect, 0) AS IsCorrect
				   FROM QuestionOptions
				   WHERE QuestionId IN ({GuidList(ids)}) AND ISNULL(IsActive, 1) = 1 AND ISNULL(IsDeleted, 0) = 0
				   ORDER BY QuestionId, OrderIndex, OptionLabel",
				new Dictionary<string, object>());
			return rows.GroupBy(r => r.QuestionId).ToDictionary(g => g.Key, g => g.ToList());
		}

		#endregion

		#region Data access

		private async Task<List<ParentQuizSummaryDto>> LoadSummariesAsync(string where, Dictionary<string, object> args)
		{
			var rows = (await _query.QueryAsync<ParentQuizSummaryDto>(
				$@"SELECT TOP 100 z.Id, z.StudentId,
				          LTRIM(RTRIM(ISNULL(su.FirstName, '') + ' ' + ISNULL(su.LastName, ''))) AS StudentName,
				          LTRIM(RTRIM(ISNULL(pu.FirstName, '') + ' ' + ISNULL(pu.LastName, ''))) AS ParentName,
				          z.Title, z.SubjectName, z.TopicNames, z.Mode, z.QuestionCount, z.TimeLimitMinutes, z.PassMarkPercent,
				          z.Status, z.CreatedAt, z.CompletedAt, a.ScorePercent, a.IsPassed, a.TotalCorrect
				   FROM ParentQuiz z
				   JOIN Users su ON su.Id = z.StudentId
				   LEFT JOIN Users pu ON pu.Id = z.ParentId
				   LEFT JOIN ParentQuizAttempt a ON a.QuizId = z.Id AND a.Status = 'Submitted'
				   WHERE {where} AND z.IsActive = 1 AND z.Status <> 'Cancelled'
				   ORDER BY z.CreatedAt DESC", args)).ToList();

			foreach (var r in rows)
			{
				r.CreatedAt = Utc(r.CreatedAt);
				if (r.CompletedAt.HasValue) r.CompletedAt = Utc(r.CompletedAt.Value);
			}
			return rows;
		}

		private async Task<ParentQuiz?> LoadQuizAsync(Guid quizId, Guid studentId, Guid schoolId)
			=> (await _query.QueryAsync<ParentQuiz>(
				"SELECT * FROM ParentQuiz WHERE Id = @QuizId AND StudentId = @StudentId AND SchoolId = @SchoolId AND IsActive = 1",
				new Dictionary<string, object> { { "QuizId", quizId }, { "StudentId", studentId }, { "SchoolId", schoolId } })).FirstOrDefault();

		private async Task<ParentQuizAttempt?> LoadAttemptAsync(Guid quizId)
			=> (await _query.QueryAsync<ParentQuizAttempt>(
				"SELECT * FROM ParentQuizAttempt WHERE QuizId = @QuizId",
				new Dictionary<string, object> { { "QuizId", quizId } })).FirstOrDefault();

		private async Task<ParentQuizConfig?> LoadConfigAsync(Guid parentId, Guid schoolId)
			=> (await _query.QueryAsync<ParentQuizConfig>(
				"SELECT * FROM ParentQuizConfig WHERE ParentId = @ParentId AND SchoolId = @SchoolId",
				new Dictionary<string, object> { { "ParentId", parentId }, { "SchoolId", schoolId } })).FirstOrDefault();

		private async Task<List<Guid>> LoadChildClassroomsAsync(Guid studentId, Guid schoolId)
			=> (await _query.QueryAsync<Guid>(
				"SELECT ClassroomId FROM StudentClassroom WHERE StudentId = @StudentId AND SchoolId = @SchoolId AND IsActive = 1",
				new Dictionary<string, object> { { "StudentId", studentId }, { "SchoolId", schoolId } })).Distinct().ToList();

		/// <summary>The subjects the child takes: those taught in their class plus any minor subjects they elected.</summary>
		private async Task<List<IdName>> LoadChildSubjectsAsync(Guid studentId, Guid schoolId)
			=> (await _query.QueryAsync<IdName>(
				@"SELECT s.Id, s.Subject AS Name
				  FROM Subjects s
				  WHERE s.SchoolId = @SchoolId AND s.IsActive = 1
				    AND s.Id IN (
				        SELECT cs.SubjectId FROM StudentClassroom sc
				        JOIN ClassroomSubject cs ON cs.ClassroomId = sc.ClassroomId AND cs.IsActive = 1 AND cs.SchoolId = @SchoolId
				        WHERE sc.StudentId = @StudentId AND sc.IsActive = 1
				        UNION
				        SELECT sms.SubjectId FROM StudentMinorSubject sms
				        WHERE sms.StudentId = @StudentId AND sms.IsActive = 1 AND sms.SchoolId = @SchoolId)
				  ORDER BY s.Subject",
				new Dictionary<string, object> { { "StudentId", studentId }, { "SchoolId", schoolId } })).ToList();

		/// <summary>Null when the parent really owns this (active) child in their school; otherwise the failure to return.</summary>
		private async Task<BaseResponse?> CheckOwnChildAsync(Caller caller, Guid studentId)
		{
			var owns = (await _query.QueryAsync<int>(
				@"SELECT COUNT(*) FROM StudentParent sp JOIN Users u ON u.Id = sp.StudentId AND u.SchoolId = sp.SchoolId AND u.IsActive = 1
				  WHERE sp.ParentId = @ParentId AND sp.StudentId = @StudentId AND sp.SchoolId = @SchoolId AND sp.IsActive = 1",
				new Dictionary<string, object> { { "ParentId", caller.UserId }, { "StudentId", studentId }, { "SchoolId", caller.SchoolId } })).FirstOrDefault();

			return owns > 0 ? null : Fail(ResponseCode.Forbidden, "You can only set quizzes for your own children");
		}

		#endregion

		#region Helpers

		private readonly record struct Caller(Guid UserId, Guid SchoolId);

		/// <summary>The signed-in user, provided they hold the expected role; otherwise null with the failure to return.</summary>
		private static Caller? RequireCaller(AuthenticatedUserClaims? claims, UserRole expected, out BaseResponse? denied)
		{
			denied = null;
			if (!Guid.TryParse(claims?.UserId, out var userId) || !Guid.TryParse(claims?.SchoolId, out var schoolId)
				|| userId == Guid.Empty || schoolId == Guid.Empty)
			{
				denied = Fail(ResponseCode.Unauthorized, "Invalid user context");
				return null;
			}
			if (!string.Equals(claims?.Role, expected.ToString(), StringComparison.OrdinalIgnoreCase))
			{
				denied = Fail(ResponseCode.Forbidden, expected == UserRole.Parent
					? "Only parents can set practice quizzes"
					: "Only students can take practice quizzes");
				return null;
			}
			return new Caller(userId, schoolId);
		}

		private static string? NormaliseMode(string? mode)
			=> ParentQuizModes.All.FirstOrDefault(m => string.Equals(m, mode?.Trim(), StringComparison.OrdinalIgnoreCase));

		private static string? ValidateSettings(int count, string? mode, int? timeLimit, int passMark)
		{
			if (count < ParentQuizDefaults.MinQuestions || count > ParentQuizDefaults.MaxQuestions)
				return $"A quiz needs between {ParentQuizDefaults.MinQuestions} and {ParentQuizDefaults.MaxQuestions} questions";
			if (mode is null)
				return "Difficulty must be Mix, Easy, Medium, Hard or ExamLevel";
			if (timeLimit is < 0 or > ParentQuizDefaults.MaxTimeLimitMinutes)
				return $"The time limit must be between 1 and {ParentQuizDefaults.MaxTimeLimitMinutes} minutes (or none)";
			if (passMark < 1 || passMark > 100)
				return "The pass mark must be between 1 and 100";
			return null;
		}

		private static ParentQuizSettingsDto ToDto(ParentQuizConfig? config) => new()
		{
			QuestionCount = config?.QuestionCount ?? ParentQuizDefaults.QuestionCount,
			Mode = config?.Mode ?? ParentQuizModes.Mix,
			TimeLimitMinutes = config?.TimeLimitMinutes,
			PassMarkPercent = config?.PassMarkPercent ?? ParentQuizDefaults.PassMarkPercent,
			ShuffleQuestions = config?.ShuffleQuestions ?? true,
			IsCustomised = config != null
		};

		private static ParentQuizAvailabilityDto Tally(IEnumerable<CountRow> rows)
		{
			var result = new ParentQuizAvailabilityDto();
			foreach (var r in rows)
			{
				switch (r.Difficulty)
				{
					case 1: result.Easy += r.Total; break;
					case 3: result.Hard += r.Total; break;
					case 4: result.ExamLevel += r.Total; break;
					default: result.Medium += r.Total; break;
				}
			}
			return result;
		}

		private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];

		/// <summary>SQL datetime2 comes back with no kind; mark it UTC so clients parse it correctly.</summary>
		private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

		private static BaseResponse Ok(string message, object? data)
			=> new BaseResponse { ResponseCode = ResponseCode.successful, ResponseMessage = message, Status = "successful", Data = data! };

		private static BaseResponse Fail(string code, string message)
			=> new BaseResponse { ResponseCode = code, ResponseMessage = message, Status = "failed" };

		#endregion

		#region Row types (public so Dapper can materialise them)

		public class IdName { public Guid Id { get; set; } public string Name { get; set; } = string.Empty; }
		public class TopicRow { public Guid Id { get; set; } public Guid SubjectId { get; set; } public string Name { get; set; } = string.Empty; }
		public class SubTopicRow { public Guid Id { get; set; } public Guid TopicId { get; set; } public string Name { get; set; } = string.Empty; }
		public class CountRow { public Guid SubjectId { get; set; } public Guid? TopicId { get; set; } public Guid? SubTopicId { get; set; } public int Difficulty { get; set; } public int Total { get; set; } }
		public class PoolRow { public Guid QuestionId { get; set; } public int Difficulty { get; set; } public int Seen { get; set; } }
		public class OptionCheckRow { public Guid OptionId { get; set; } public Guid QuestionId { get; set; } public bool IsCorrect { get; set; } }
		public class AnswerRow { public Guid QuestionId { get; set; } public Guid? SelectedOptionId { get; set; } public bool IsCorrect { get; set; } public bool IsSkipped { get; set; } }

		public class QuestionRow
		{
			public Guid QuestionId { get; set; }
			public int DisplayOrder { get; set; }
			public string? Title { get; set; }
			public string? TextContent { get; set; }
			public string? QuestionHtml { get; set; }
			public string? ContentParts { get; set; }
			public bool? HasLatex { get; set; }
			public string? ImageUrl { get; set; }
			public int QuestionType { get; set; }
			public int DifficultyLevel { get; set; }
		}

		public class OptionRow
		{
			public Guid QuestionId { get; set; }
			public Guid OptionId { get; set; }
			public string? OptionLabel { get; set; }
			public string? OptionText { get; set; }
			public string? OptionHtml { get; set; }
			public string? ContentParts { get; set; }
			public bool? HasLatex { get; set; }
			public int? OrderIndex { get; set; }
			public bool IsCorrect { get; set; }
		}

		#endregion
	}
}
