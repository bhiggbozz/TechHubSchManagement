using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core.Model;

namespace TechHub.Core.Entities
{
	public class ClassroomTeacher
	{
		
			public ClassroomTeacher()
			{
				Id = Guid.NewGuid();
				CreationDate = DateTime.UtcNow;
				ModifiedDate = DateTime.UtcNow;
				IsActive = true;
				IsPrimary = false;
			}

			/// <summary>
			/// Unique identifier for the classroom-teacher relationship
			/// </summary>
			public Guid Id { get; set; }

			/// <summary>
			/// Reference to the classroom
			/// </summary>
			public Guid ClassroomId { get; set; }

			/// <summary>
			/// Reference to the teacher (User)
			/// </summary>
			public Guid TeacherId { get; set; }

			/// <summary>
			/// Indicates if this is the primary/head teacher for the classroom
			/// </summary>
			public bool IsPrimary { get; set; }

			/// <summary>
			/// Date and time when the record was created (UTC)
			/// </summary>
			public DateTime CreationDate { get; set; }

			/// <summary>
			/// Date and time when the record was last modified (UTC)
			/// </summary>
			public DateTime ModifiedDate { get; set; }

			/// <summary>
			/// Reference to the user who created this record
			/// </summary>
			public Guid CreatedBy { get; set; }

			/// <summary>
			/// Reference to the school
			/// </summary>
			public Guid SchoolId { get; set; }

			/// <summary>
			/// Indicates if the record is active
			/// </summary>
			public bool IsActive { get; set; }
		}
	
}
