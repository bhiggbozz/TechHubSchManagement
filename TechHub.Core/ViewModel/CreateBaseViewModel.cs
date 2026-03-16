using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.ViewModel
{
	public class CreateBaseViewModel
	{
		public Guid? CreatedBy { get; set; }
		public Guid? SchoolId { get; set; }
	}
}
