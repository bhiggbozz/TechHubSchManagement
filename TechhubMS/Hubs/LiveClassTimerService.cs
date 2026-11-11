using TechHub.Service.Interface;

namespace TechhubMS.Hubs
{
	/// <summary>
	/// Enforces the time limit on the server: every few seconds it sends the 5- and 2-minute warnings and
	/// ends any class that has run out of time, whatever the browsers do.
	/// </summary>
	public class LiveClassTimerService : BackgroundService
	{
		private static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);

		private readonly IServiceScopeFactory _scopes;
		private readonly Serilog.ILogger _logger;

		public LiveClassTimerService(IServiceScopeFactory scopes, Serilog.ILogger logger)
		{
			_scopes = scopes;
			_logger = logger;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			_logger.Information("LiveClassTimerService started - Interval: {Seconds}s", Interval.TotalSeconds);

			try { await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); }
			catch (OperationCanceledException) { return; }

			using var timer = new PeriodicTimer(Interval);
			do
			{
				try
				{
					using var scope = _scopes.CreateScope();
					await scope.ServiceProvider.GetRequiredService<ILiveClassService>().TickAsync();
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "LiveClassTimerService tick failed");
				}
			}
			while (await WaitAsync(timer, stoppingToken));
		}

		private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken token)
		{
			try { return await timer.WaitForNextTickAsync(token); }
			catch (OperationCanceledException) { return false; }
		}
	}
}
