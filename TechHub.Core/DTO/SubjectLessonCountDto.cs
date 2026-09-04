namespace TechHub.Core.DTO
{
	public class SubjectLessonCountDto
	{
		public Guid SubjectId { get; set; }
		public string SubjectName { get; set; } = string.Empty;
		public int LessonCount { get; set; }
	}
}
