using Microsoft.Extensions.Caching.Memory;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TechHub.Core;
using TechHub.Core.Entities;
using TechHub.Service.Interface;

namespace TechHub.Service.Service
{
	public class AcademicPeriodResolver : IAcademicPeriodResolver
	{
		private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(5);

		private readonly IQueryRepository<AcademicSession> _query;
		private readonly IMemoryCache _cache;
		private readonly ILogger _logger;

		public AcademicPeriodResolver(IQueryRepository<AcademicSession> query, IMemoryCache cache, ILogger logger)
		{
			_query = query;
			_cache = cache;
			_logger = logger;
		}

		public Task<AcademicPeriod> ResolveNowAsync(Guid schoolId) => ResolveAsync(schoolId, DateTime.UtcNow);

		public async Task<AcademicPeriod> ResolveAsync(Guid schoolId, DateTime utcWhen)
		{
			try
			{
				var calendar = await LoadAsync(schoolId);
				if (calendar.Count == 0) return AcademicPeriod.None;

				var day = SchoolDate(utcWhen);
				var session = calendar.FirstOrDefault(r => r.SessionStart <= day && day <= r.SessionEnd);
				if (session is null) return AcademicPeriod.None;

				var term = calendar.FirstOrDefault(r => r.SessionId == session.SessionId
					&& r.TermId.HasValue && r.TermStart <= day && day <= r.TermEnd);

				return new AcademicPeriod(session.SessionId, term?.TermId);
			}
			catch (Exception ex)
			{
				_logger.Warning(ex, "AcademicPeriodResolver: could not resolve the period for school {SchoolId}; record will be saved unstamped", schoolId);
				return AcademicPeriod.None;
			}
		}

		public void Invalidate(Guid schoolId) => _cache.Remove(CacheKey(schoolId));

		private async Task<List<CalendarRow>> LoadAsync(Guid schoolId)
		{
			if (_cache.TryGetValue(CacheKey(schoolId), out List<CalendarRow>? hit) && hit != null)
				return hit;

			// One row per (session, term); a session with no active term still yields one row.
			var rows = (await _query.QueryAsync<CalendarRow>(
				@"SELECT s.Id AS SessionId, s.StartDate AS SessionStart, s.EndDate AS SessionEnd,
				         t.Id AS TermId, t.StartDate AS TermStart, t.EndDate AS TermEnd
				  FROM AcademicSession s
				  LEFT JOIN AcademicTerm t ON t.SessionId = s.Id AND t.IsActive = 1
				  WHERE s.SchoolId = @SchoolId AND s.IsActive = 1",
				new Dictionary<string, object> { { "SchoolId", schoolId } })).ToList();

			_cache.Set(CacheKey(schoolId), rows, CacheFor);
			return rows;
		}

		private static string CacheKey(Guid schoolId) => "academic-calendar:" + schoolId;

		/// <summary>The school's local calendar date for a UTC moment (WAT, UTC+1, no DST).</summary>
		private static DateTime SchoolDate(DateTime utc)
		{
			utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
			foreach (var id in new[] { "Africa/Lagos", "W. Central Africa Standard Time" })
			{
				try { return TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.FindSystemTimeZoneById(id)).Date; }
				catch (TimeZoneNotFoundException) { }
				catch (InvalidTimeZoneException) { }
			}
			return utc.AddHours(1).Date;
		}

		public sealed class CalendarRow
		{
			public Guid SessionId { get; set; }
			public DateTime SessionStart { get; set; }
			public DateTime SessionEnd { get; set; }
			public Guid? TermId { get; set; }
			public DateTime? TermStart { get; set; }
			public DateTime? TermEnd { get; set; }
		}
	}
}
