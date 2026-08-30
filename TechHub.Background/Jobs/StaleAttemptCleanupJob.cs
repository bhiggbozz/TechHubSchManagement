using Hangfire;
using Serilog;
using TechHub.Core.Constant;
using TechHub.Core.Entities;
using TechHub.Service.Interface;

namespace TechHub.BackgroundJobs.Jobs
{
	/// <summary>
	/// Recurring job ("stale-attempt-cleanup") that finds QuizAttempt/AssessmentAttempt
	/// rows still Status='InProgress' well past their time limit (+ AttemptExpiryPolicy
	/// grace) and marks them Abandoned.
	///
	/// UserService.Login() already does this same check-and-close the moment the
	/// student themselves logs back in — this job exists for the student who never
	/// comes back to log in at all, so grading queues and dashboards don't keep
	/// counting a dead attempt as InProgress indefinitely.
	/// </summary>
	public class StaleAttemptCleanupJob
	{
		private readonly IQueryRepository<QuizAttempt> _quizAttemptQuery;
		private readonly ICommandRespository<QuizAttempt> _quizAttemptCommand;
		private readonly ILogger _logger;

		public StaleAttemptCleanupJob(
			IQueryRepository<QuizAttempt> quizAttemptQuery,
			ICommandRespository<QuizAttempt> quizAttemptCommand,
			ILogger logger)
		{
			_quizAttemptQuery = quizAttemptQuery;
			_quizAttemptCommand = quizAttemptCommand;
			_logger = logger;
		}

		[Queue("low")]
		[AutomaticRetry(Attempts = 2)]
		public async Task ExecuteAsync()
		{
			try
			{
				// Quiz time limits resolve through Quiz/AssessmentSet/teacher-default
				// config, not a direct QuizAttempt->QuizConfig join, so we use the
				// flat fallback window here rather than replicate that resolution —
				// same tradeoff UserService.Login() makes for the same reason.
				var staleQuizIds = (await _quizAttemptQuery.QueryAsync<Guid>(
					@"SELECT Id FROM QuizAttempt
					  WHERE Status = 'InProgress'
					  AND DATEADD(MINUTE, @FallbackMinutes + @GraceMinutes, StartedAt) < GETUTCDATE()",
					new Dictionary<string, object>
					{
						{ "FallbackMinutes", AttemptExpiryPolicy.UntimedQuizFallbackMinutes },
						{ "GraceMinutes", AttemptExpiryPolicy.GraceMinutes }
					})).ToList();

				if (staleQuizIds.Any())
				{
					await _quizAttemptCommand.UpdateAsync(
						"UPDATE QuizAttempt SET Status = 'Abandoned' WHERE Id IN @Ids AND Status = 'InProgress'",
						new Dictionary<string, object> { { "Ids", staleQuizIds } });
				}

				var staleAssessmentIds = (await _quizAttemptQuery.QueryAsync<Guid>(
					@"SELECT aa.Id
					  FROM AssessmentAttempt aa
					  LEFT JOIN AssessmentConfig ac ON ac.AssessmentId = aa.AssessmentId
					  WHERE aa.Status = 'InProgress'
					  AND DATEADD(MINUTE, ISNULL(ac.TimeLimitMinutes, @FallbackMinutes) + @GraceMinutes, aa.StartedAt) < GETUTCDATE()",
					new Dictionary<string, object>
					{
						{ "FallbackMinutes", AttemptExpiryPolicy.UntimedQuizFallbackMinutes },
						{ "GraceMinutes", AttemptExpiryPolicy.GraceMinutes }
					})).ToList();

				if (staleAssessmentIds.Any())
				{
					await _quizAttemptCommand.UpdateAsync(
						"UPDATE AssessmentAttempt SET Status = 'Abandoned' WHERE Id IN @Ids AND Status = 'InProgress'",
						new Dictionary<string, object> { { "Ids", staleAssessmentIds } });
				}

				_logger.Information(
					"StaleAttemptCleanup completed - QuizAttemptsAbandoned: {QuizCount}, AssessmentAttemptsAbandoned: {AssessmentCount}",
					staleQuizIds.Count, staleAssessmentIds.Count);
			}
			catch (Exception ex)
			{
				// Cleanup failure must never affect anything else.
				_logger.Warning(ex, "StaleAttemptCleanup failed (non-fatal)");
			}
		}
	}
}
