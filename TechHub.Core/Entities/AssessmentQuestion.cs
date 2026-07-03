using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.Entities;

public class AssessmentQuestion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AssessmentId { get; set; }
    public Guid QuestionId { get; set; }
    public Guid SchoolId { get; set; }
    public int DisplayOrder { get; set; }
    public string CreationDate { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
    public bool IsActive { get; set; } = true;
}
