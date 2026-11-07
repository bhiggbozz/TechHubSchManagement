using System;
using System.Threading.Tasks;

namespace TechHub.Service.Interface
{
	/// <summary>The academic session and term a moment falls in. Either may be null (no calendar set up, a break between terms, or outside every session).</summary>
	public readonly record struct AcademicPeriod(Guid? SessionId, Guid? TermId)
	{
		public static readonly AcademicPeriod None = new(null, null);

		/// <summary>Adds SessionId/TermId to an insert dictionary, skipping whichever is null (the columns are nullable and default to null).</summary>
		public void ApplyTo(System.Collections.Generic.IDictionary<string, object> values)
		{
			if (SessionId.HasValue) values["SessionId"] = SessionId.Value;
			if (TermId.HasValue) values["TermId"] = TermId.Value;
		}
	}

	/// <summary>
	/// Works out which academic session/term a quiz, assessment or attempt belongs to, so the
	/// ids can be stamped on the record when it is created and reports can filter on a plain
	/// column instead of joining to the calendar. Cached per school; never throws — a record
	/// must never fail to save because the calendar could not be read.
	/// </summary>
	public interface IAcademicPeriodResolver
	{
		/// <summary>The period containing the given UTC moment (matched by the school's local WAT date).</summary>
		Task<AcademicPeriod> ResolveAsync(Guid schoolId, DateTime utcWhen);

		/// <summary>The period containing right now.</summary>
		Task<AcademicPeriod> ResolveNowAsync(Guid schoolId);

		/// <summary>Drop the school's cached calendar; called after any session/term change.</summary>
		void Invalidate(Guid schoolId);
	}
}
