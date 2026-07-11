using System;

namespace TechHub.Core.Entities;

public class AssessmentAttemptAnswerBoard
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AnswerId { get; set; }
    public string BoardSessionId { get; set; } = string.Empty;
    public int? BoardIndex { get; set; }
    public string? BoardLabel { get; set; }
    public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
}
