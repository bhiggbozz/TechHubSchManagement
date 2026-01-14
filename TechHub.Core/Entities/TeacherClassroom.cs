using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.Entities
{
    public class TeacherClassroom
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string CreationDate { get; set; } = DateTime.Now.ToString();
        public string ModifiedDate { get; set; } = DateTime.Now.ToString();
        public Guid TeacherId { get; set; }
        public Guid ClassroomId { get; set; }

        public Guid SchoolId { get; set; }
        public bool IsActive { get; set; }
        public Guid CreatedBy { get; set; }
    }
}
