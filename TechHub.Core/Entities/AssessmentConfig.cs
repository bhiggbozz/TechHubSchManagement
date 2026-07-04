using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.Entities;

public class AssessmentConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AssessmentId { get; set; }
    public int TimeLimitMinutes { get; set; }
    public bool ShuffleQuestions { get; set; } = false;
    public int PassMarkPercent { get; set; } = 50;
    public bool ShowResultImmediately { get; set; } = true;
    public bool ShowCorrectAnswers { get; set; } = false;
    public DateTime? ExpiresAt { get; set; }
    public int EasyMarks { get; set; } = 1;
    public int MediumMarks { get; set; } = 2;
    public int HardMarks { get; set; } = 3;
    public int ExamLevelMarks { get; set; } = 5;
    public Guid CreatedBy { get; set; }
    public string CreationDate { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
    public string ModifiedDate { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
    public bool IsActive { get; set; } = true;
}
