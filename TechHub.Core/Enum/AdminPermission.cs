using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.Enum;


[Flags]
public enum AdminPermission
{
	None = 0,                    // 0000000 (binary)
	ApproveClasses = 1,          // 0000001
	CreateLessons = 2,           // 0000010
	ManageTeachers = 4,          // 0000100
	ManageStudents = 8,          // 0001000
	ViewReports = 16,            // 0010000
	ManageLessons = 32,          // 0100000
	ManageSubjects = 64,         // 1000000
	CreateUsers = 128,           // ✅ NEW: 10000000 - Can create users

	BasicAdmin = CreateLessons | ViewReports,  // = 18 (2 + 16)
	FullAdmin = ApproveClasses | CreateLessons | ManageTeachers | ManageStudents | ViewReports | ManageLessons | ManageSubjects  // = 127
}

