using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.ResponseModel
{
	public class AllSubjectResponseModel : BaseResponse
	{
		public List<SubjectResponseModel> AllSubjects{ get; set; }	= new List<SubjectResponseModel>();
	}
	public class SubjectResponseModel
	{
		public string Subject { get; set; }
		public Guid SchoolId { get; set; }
		public string Category { get; set; }
	}
}
