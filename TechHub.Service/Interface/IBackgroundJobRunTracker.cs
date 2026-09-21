using System;
using System.Threading.Tasks;
using TechHub.Core;

namespace TechHub.Service.Interface
{
	public interface IBackgroundJobRunTracker
	{
		// Records a Running row, executes work, then updates it to Succeeded/Failed
		// with duration + error message. Rethrows on failure so the caller's own
		// try/catch (every worker already has one around its cycle) still sees
		// the exception and logs/continues exactly as it does today — this only
		// adds a persisted record alongside the existing behavior.
		Task TrackAsync(string jobName, Func<Task> work);

		Task<BaseResponse> GetRecentRunsAsync(string? jobName, int limit);
	}
}
