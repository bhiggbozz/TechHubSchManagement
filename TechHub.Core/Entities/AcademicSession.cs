using System;

namespace TechHub.Core.Entities
{
	/// <summary>
	/// A school's academic year (e.g. "2026/2027"). Terms live inside it. Dates are
	/// whole days, inclusive on both ends, stored as SQL DATE — no time component, so
	/// there are no timezone surprises when a session starts or ends.
	/// </summary>
	public class AcademicSession
	{
		public Guid Id { get; set; } = Guid.NewGuid();
		public Guid SchoolId { get; set; }
		public string Name { get; set; } = string.Empty;
		public DateTime StartDate { get; set; }
		public DateTime EndDate { get; set; }
		public Guid CreatedBy { get; set; }
		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
		public DateTime? ModifiedAt { get; set; }
		public bool IsActive { get; set; } = true;
	}
}
