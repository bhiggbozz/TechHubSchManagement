using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using TechHub.Core.ViewModel;

namespace TechHub.Core.ViewModel.Groups
{
	public class CreateGroupViewModel
	{
		[Required]
		public string Name { get; set; }
	}

	public class AddGroupMembersViewModel
	{
		[Required]
		public List<Guid> StudentIds { get; set; }
	}

	public class SubmitGroupContentViewModel
	{
		[Required]
		public Guid SubjectId { get; set; }

		public Guid? TopicId { get; set; }

		[StringLength(200)]
		public string? SubTopic { get; set; }

		[Required]
		[StringLength(500)]
		public string Aim { get; set; }

		[Required]
		[StringLength(2000)]
		public string Description { get; set; }

		// Optional — a student may drop just one media file, several, or none
		// at all (e.g. board-only content). Same permissiveness as lessons.
		public List<LessonMediaViewModel> MediaFiles { get; set; } = new();
	}
}
