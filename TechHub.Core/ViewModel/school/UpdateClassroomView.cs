using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core.Attribute;

namespace TechHub.Core.ViewModel.school
{
	public class UpdateClassroomView : CreateBaseViewModel
	{
		public List<ClassroomUpdateView> classroomUpdateViews { get; set; } = new List<ClassroomUpdateView>();
	}
	public class ClassroomUpdateView
	{
		public bool IsActive { get; set; } = true;
		public string Name { get; set; }
		[NotEmptyGuid(ErrorMessage = "The field must contain a valid Guid")]
		public Guid Id { get; set; }
	}
}
