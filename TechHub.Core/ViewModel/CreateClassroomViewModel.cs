using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.ViewModel
{
	public class CreateClassroomViewModel :CreateBaseViewModel
	{
		public List<Guid> SubjectId { get; set; }
		public Guid ClassroomId { get; set; }
	}
}
