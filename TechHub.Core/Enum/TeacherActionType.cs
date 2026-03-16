using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.Enum;

public enum TeacherActionType
{
	Add = 1,        // Create new assignment
	Remove = 2,     // Set existing assignment to IsActive = false
	Reactivate = 3  // Set existing assignment to IsActive = true
}
