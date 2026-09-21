using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Serilog;
using TechHub.Core;
using TechHub.Core.Entities;
using TechHub.Core.Model;
using TechHub.Service.Interface;

namespace TechHub.Service.Service
{
	public class BackgroundJobRunTracker : IBackgroundJobRunTracker
	{
		private readonly ICommandRespository<BackgroundJobRun> _command;
		private readonly IQueryRepository<BackgroundJobRun> _query;
		private readonly ILogger _logger;

		public BackgroundJobRunTracker(
			ICommandRespository<BackgroundJobRun> command,
			IQueryRepository<BackgroundJobRun> query,
			ILogger logger)
		{
			_command = command;
			_query = query;
			_logger = logger;
		}

		public async Task TrackAsync(string jobName, Func<Task> work)
		{
			var runId = Guid.NewGuid();
			var startedAt = DateTime.UtcNow;
			var sw = Stopwatch.StartNew();

			try
			{
				await _command.Create(new Dictionary<string, object>
				{
					{ "Id", runId },
					{ "JobName", jobName },
					{ "StartedAt", startedAt },
					{ "CompletedAt", DBNull.Value },
					{ "DurationMs", DBNull.Value },
					{ "Status", "Running" },
					{ "ErrorMessage", DBNull.Value },
					{ "Details", DBNull.Value }
				});
			}
			catch (Exception ex)
			{
				// Never let tracking itself block the actual job from running.
				_logger.Error(ex, "BackgroundJobRunTracker failed to record start for {JobName}", jobName);
			}

			try
			{
				await work();
				sw.Stop();

				try
				{
					await _command.UpdateTableColumnById(
						new Dictionary<string, object>
						{
							{ "CompletedAt", DateTime.UtcNow },
							{ "DurationMs", sw.ElapsedMilliseconds },
							{ "Status", "Succeeded" }
						},
						new KeyValuePair<string, object>("Id", runId));
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "BackgroundJobRunTracker failed to record success for {JobName}", jobName);
				}
			}
			catch (Exception ex)
			{
				sw.Stop();

				try
				{
					await _command.UpdateTableColumnById(
						new Dictionary<string, object>
						{
							{ "CompletedAt", DateTime.UtcNow },
							{ "DurationMs", sw.ElapsedMilliseconds },
							{ "Status", "Failed" },
							{ "ErrorMessage", ex.Message.Length > 4000 ? ex.Message.Substring(0, 4000) : ex.Message }
						},
						new KeyValuePair<string, object>("Id", runId));
				}
				catch (Exception trackEx)
				{
					_logger.Error(trackEx, "BackgroundJobRunTracker failed to record failure for {JobName}", jobName);
				}

				throw;
			}
		}

		public async Task<BaseResponse> GetRecentRunsAsync(string? jobName, int limit)
		{
			try
			{
				limit = limit < 1 || limit > 200 ? 20 : limit;

				var sql = string.IsNullOrWhiteSpace(jobName)
					? $"SELECT TOP {limit} * FROM BackgroundJobRun ORDER BY StartedAt DESC"
					: $"SELECT TOP {limit} * FROM BackgroundJobRun WHERE JobName = @JobName ORDER BY StartedAt DESC";

				var runs = await _query.QueryAsync<BackgroundJobRun>(sql,
					string.IsNullOrWhiteSpace(jobName)
						? new Dictionary<string, object>()
						: new Dictionary<string, object> { { "JobName", jobName! } });

				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "Background job runs retrieved",
					Status = "successful",
					Data = runs.ToList()
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Error fetching background job runs");
				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "An error occurred while fetching background job runs",
					Status = "failed"
				};
			}
		}
	}
}
