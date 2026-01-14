using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core.Attribute;

namespace TechHub.Core.ViewModel.school
{
	public class updateSchoolSubject : CreateBaseViewModel
	{
		public List<subjectUpdate> subjectUpdates = new List<subjectUpdate>();
	}
	public class subjectUpdate
	{
		public bool isDeleted { get; set; }
		public string Subject { get; set; }
		[NotEmptyGuid(ErrorMessage = "The field must contain a valid Guid")]
		public Guid subjectId { get; set; }
	}
}
