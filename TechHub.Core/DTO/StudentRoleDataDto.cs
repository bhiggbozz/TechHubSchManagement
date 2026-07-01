using System;
using System.Collections.Generic;

namespace TechHub.Core.DTO;

public class StudentRoleDataDto
{
    public ClassroomInfo? Classroom { get; set; }
    public List<SubjectInfo> MajorSubjects { get; set; } = new();
    public List<SubjectInfo> MinorSubjects { get; set; } = new();
}

public class ClassroomInfo
{
    public Guid ClassroomId { get; set; }
    public string ClassName { get; set; } = string.Empty;
}

public class SubjectInfo
{
    public Guid SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
}

public class StudentClassroomRow
{
    public Guid StudentId { get; set; }
    public Guid ClassroomId { get; set; }
    public string ClassName { get; set; } = string.Empty;
}

public class StudentMajorSubjectRow
{
    public Guid StudentId { get; set; }
    public Guid SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
}
