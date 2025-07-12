using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.ViewModel
{
	public class RegisterStudentClassViewModel : CreateBaseViewModel
	{
		[Required]
		public Guid ClassId { get; set; }
		[Required]
		public Guid StudentId { get; set; }
	}
}
