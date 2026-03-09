using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.Enum;

/// <summary>
/// Type of class delivery
/// </summary>
public enum ClassType
{
	/// <summary>
	/// Live/in-person class
	/// </summary>
	LiveClass = 1,

	/// <summary>
	/// Pre-recorded class
	/// </summary>
	RecordedClass = 2,

	/// <summary>
	/// Interactive/hybrid class
	/// </summary>
	InteractiveClass = 3
}

