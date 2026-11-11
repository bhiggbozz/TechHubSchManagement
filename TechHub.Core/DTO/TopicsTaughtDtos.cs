using System;
using System.Collections.Generic;

namespace TechHub.Core.DTO
{
	/// <summary>What was taught in a term or session: subjects → topics → lessons.</summary>
	public class TopicsTaughtDto
	{
		/// <summary>"Term" or "Session" — what the request resolved to.</summary>
		public string Scope { get; set; } = "";
		public Guid SessionId { get; set; }
		public string SessionName { get; set; } = "";
		/// <summary>Set only when Scope is "Term".</summary>
		public Guid? TermId { get; set; }
		public string? TermName { get; set; }
		/// <summary>yyyy-MM-dd, inclusive.</summary>
		public string From { get; set; } = "";
		public string To { get; set; } = "";
		public Guid? StudentId { get; set; }
		public string? StudentName { get; set; }
		public List<TopicsTaughtClassroomDto> Classrooms { get; set; } = new();
		public int TotalLessons { get; set; }
		public int TotalTopics { get; set; }
		/// <summary>One entry per term (a single one for a term request); lessons that fall in a break go in a final entry with a null term.</summary>
		public List<TopicsTaughtTermDto> Terms { get; set; } = new();
	}

	public class TopicsTaughtClassroomDto
	{
		public Guid ClassroomId { get; set; }
		public string ClassroomName { get; set; } = "";
	}

	public class TopicsTaughtTermDto
	{
		public Guid? TermId { get; set; }
		/// <summary>Null for lessons taught between terms.</summary>
		public string? TermName { get; set; }
		public string? From { get; set; }
		public string? To { get; set; }
		public int LessonCount { get; set; }
		public int TopicCount { get; set; }
		public List<TopicsTaughtSubjectDto> Subjects { get; set; } = new();
	}

	public class TopicsTaughtSubjectDto
	{
		public Guid SubjectId { get; set; }
		public string SubjectName { get; set; } = "";
		public int LessonCount { get; set; }
		public List<TopicsTaughtTopicDto> Topics { get; set; } = new();
	}

	public class TopicsTaughtTopicDto
	{
		public Guid TopicId { get; set; }
		public string TopicName { get; set; } = "";
		public int LessonCount { get; set; }
		public string FirstTaughtOn { get; set; } = "";
		public string LastTaughtOn { get; set; } = "";
		public List<TopicsTaughtLessonDto> Lessons { get; set; } = new();
	}

	public class TopicsTaughtLessonDto
	{
		public Guid LessonId { get; set; }
		public string Aim { get; set; } = "";
		public Guid? SubTopicId { get; set; }
		public string? SubTopicName { get; set; }
		public string ClassroomName { get; set; } = "";
		public string TeacherName { get; set; } = "";
		/// <summary>The lesson's scheduled date, else the day it was approved. yyyy-MM-dd.</summary>
		public string TaughtOn { get; set; } = "";
		/// <summary>Only filled when a student is in scope.</summary>
		public bool? Watched { get; set; }
	}

	public class TopicsTaughtRow
	{
		public Guid LessonId { get; set; }
		public string Aim { get; set; } = "";
		public Guid ClassroomId { get; set; }
		public string ClassroomName { get; set; } = "";
		public Guid SubjectId { get; set; }
		public string SubjectName { get; set; } = "";
		public Guid TopicId { get; set; }
		public string TopicName { get; set; } = "";
		public Guid? SubTopicId { get; set; }
		public string? SubTopicName { get; set; }
		public string TeacherName { get; set; } = "";
		public DateTime TaughtOn { get; set; }
		public bool Watched { get; set; }
	}
}
