using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core.Enum;

namespace TechHub.Core.ViewModel
{
	public class CreateSubjectViewModel 
	{
		[Required(ErrorMessage = "At least one subject is required")]
		[MinLength(1, ErrorMessage = "Subject list cannot be empty")]
		public List<SubjectsDetails> Subjects { get; set; }
		//public Guid ClassroomId { get; set; }
		
	}
	public class SubjectsDetails
	{
		[Required]
		public string Subject { get; set; }
		public bool IsActive { get; set; } = true;
		[Required(ErrorMessage = "Subject category is required")]
		[EnumDataType(typeof(SubjectCategory), ErrorMessage = "Invalid subject category")]
		public SubjectCategory Category { get; set; }
		[Required(ErrorMessage = "Class category is required")]
		[EnumDataType(typeof(ClassCategory), ErrorMessage = "Invalid class category")] 
		public ClassCategory ClassCategory { get; set; }
	}
	
}
