using System;

namespace TechHub.Core.Entities
{
	/// <summary>
	/// One term/semester inside an <see cref="AcademicSession"/>. The name is free text
	/// chosen by the school ("First Term", "Autumn Term", "Semester 1" …) — nothing in
	/// the code assumes a particular naming scheme or term count. <see cref="Sequence"/>
	/// is always derived from start-date order, never supplied by the client.
	/// </summary>
	public class AcademicTerm
	{
		public Guid Id { get; set; } = Guid.NewGuid();
		public Guid SessionId { get; set; }
		public Guid SchoolId { get; set; }
		public string Name { get; set; } = string.Empty;
		public int Sequence { get; set; }
		public DateTime StartDate { get; set; }
		public DateTime EndDate { get; set; }
		public Guid CreatedBy { get; set; }
		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
		public bool IsActive { get; set; } = true;
	}
}
