using System;
using System.Collections.Generic;

namespace TechHub.Core.ViewModel.classroom;

public class StudentSummaryDto
{
    public List<UnattemptedAssessmentDto> UnattemptedAssessments { get; set; } = new();
    public List<UnattemptedQuizDto> UnattemptedQuizzes { get; set; } = new();
    public List<UnwatchedLessonDto> UnwatchedLessons { get; set; } = new();
    public SummaryCounts Counts { get; set; } = new();
}

public class UnattemptedAssessmentDto
{
    public Guid AssessmentId { get; set; }
    public string Code { get; set; }
    public string Title { get; set; }
}

public class UnattemptedQuizDto
{
    public Guid LessonId { get; set; }
    public string LessonTitle { get; set; }
    public string QuizCode { get; set; }
    public string SubjectName { get; set; }
}

public class UnwatchedLessonDto
{
    public Guid LessonId { get; set; }
    public string LessonTitle { get; set; }
    public string SubjectName { get; set; }
}

public class SummaryCounts
{
    public int UnattemptedAssessments { get; set; }
    public int UnattemptedQuizzes { get; set; }
    public int UnwatchedLessons { get; set; }
}
