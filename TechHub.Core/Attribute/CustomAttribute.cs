using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.Attribute
{
	public class NotEmptyGuidAttribute : ValidationAttribute
	{
		public override bool IsValid(object value)
		{
			if (value == null) return false;
			return value is Guid guid && guid != Guid.Empty;
		}
	}

}
