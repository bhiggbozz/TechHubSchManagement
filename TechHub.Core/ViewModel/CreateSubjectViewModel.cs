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
		public string Subject { get; set; }
		
	}
	
}
