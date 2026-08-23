using System;
using System.Collections.Generic;

namespace TechHub.Core.ViewModel
{
	public class RemoveClassroomSubjectViewModel
	{
		public List<Guid> SubjectIds { get; set; }
		public Guid ClassroomId { get; set; }
	}
}
