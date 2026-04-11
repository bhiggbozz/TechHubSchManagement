using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.ViewModel
{
	public class CreateClassroomViewModel 
	{
		public List<Guid> SubjectIds { get; set; }
		public Guid ClassroomId { get; set; }
	}
}
