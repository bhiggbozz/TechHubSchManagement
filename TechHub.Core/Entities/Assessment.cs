using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.Entities;

public class Assessments
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; }
    public string Title { get; set; }
    public string? Description { get; set; }
    public Guid SchoolId { get; set; }
    public Guid CreatedBy { get; set; }
    public string CreationDate { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
    public string ModifiedDate { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
    public bool IsActive { get; set; } = true;
}
