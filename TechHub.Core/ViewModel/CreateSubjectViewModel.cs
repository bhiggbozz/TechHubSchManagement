using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.ViewModel
{
	public class CreateSubjectViewModel : CreateBaseViewModel
	{
		[Required]
		public List<SubjectsDetails> Subjects { get; set; }
		//public Guid ClassroomId { get; set; }
		
	}
	public class SubjectsDetails
	{
		[Required]
		public string Subject { get; set; }
		public bool status { get; set; } = true;
		[Required]
		public string Category { get; set; }	
	}
	
}
