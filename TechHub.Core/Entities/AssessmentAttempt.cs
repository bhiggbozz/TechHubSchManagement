using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.Entities;

public class AssessmentAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AssessmentId { get; set; }
    public Guid StudentId { get; set; }
    public Guid SchoolId { get; set; }
    public int AttemptNumber { get; set; }
    public bool IsOfficial { get; set; } = false;
    public decimal AutoMarksObtained { get; set; }
    public decimal ManualMarksObtained { get; set; }
    public decimal TotalMarks { get; set; }
    public decimal? FinalScorePercent { get; set; }
    public bool? IsPassed { get; set; }
    public string Status { get; set; } = "InProgress";
    public string StartedAt { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
    public string? SubmittedAt { get; set; }
    public int? TimeTakenSeconds { get; set; }
    public string CreationDate { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
    public string ModifiedDate { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
}
