using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.QuestionBank.Services.interfaces;

namespace TechHub.Background.Services;

/// <summary>
/// Hosted background service that processes pending QuestionJobs
///
/// BEHAVIOUR:
/// - Runs every 30 seconds
/// - Picks up ONE pending job per cycle
/// - Calls QuestionJobService.ProcessNextPendingJob()
/// - If multiple jobs pending — each cycle processes one
/// - This keeps memory controlled and prevents thundering herd
///   on Claude API during peak upload hours
///
/// SCALING NOTE:
/// - To process faster: reduce interval to 10 seconds
/// - To process in parallel: run multiple Render instances
///   Each instance picks a different job (Pending → Processing
///   transition is atomic — no double pickup)
/// </summary>
public class QuestionJobWorker : BackgroundService
{
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly ILogger _logger;

	// How often the worker checks for pending jobs
	// 30 seconds balances responsiveness vs API cost
	private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

	public QuestionJobWorker(IServiceScopeFactory scopeFactory,ILogger logger)
	{
		_scopeFactory = scopeFactory;
		_logger = logger;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		_logger.Information("QuestionJobWorker started - Interval: {Interval}s",Interval.TotalSeconds);

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await ProcessNextJob(stoppingToken);
			}
			catch (Exception ex)
			{
				// Log but never crash the worker
				// Next cycle will try again
				_logger.Error(ex, "QuestionJobWorker cycle error");
			}

			// Wait before next cycle
			await Task.Delay(Interval, stoppingToken);
		}

		_logger.Information("QuestionJobWorker stopped");
	}

	private async Task ProcessNextJob(CancellationToken stoppingToken)
	{
		// Use a fresh DI scope per cycle
		// Prevents stale DbContext or repository state
		// across long-running background service lifetime
		await using var scope = _scopeFactory.CreateAsyncScope();

		var jobService = scope.ServiceProvider.GetRequiredService<IQuestionJobService>();

		_logger.Debug("QuestionJobWorker - checking for pending jobs");

		await jobService.ProcessNextPendingJob();
	}
}
