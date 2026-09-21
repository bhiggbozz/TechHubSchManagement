using System;

namespace TechHub.Core.Entities;

// Records one execution of a periodic IHostedService job (the 24h aggregation
// workers, not Hangfire — Hangfire already tracks its own jobs in its own
// SQL schema). One row per run: when it started, how long it took, and
// whether it succeeded — the thing you can't currently tell without reading
// raw log files.
public class BackgroundJobRun
{
	public Guid Id { get; set; }
	public string JobName { get; set; } = string.Empty;
	public DateTime StartedAt { get; set; }
	public DateTime? CompletedAt { get; set; }
	public long? DurationMs { get; set; }
	// Running | Succeeded | Failed
	public string Status { get; set; } = "Running";
	public string? ErrorMessage { get; set; }
	public string? Details { get; set; }
}
