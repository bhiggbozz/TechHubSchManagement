using System;
using System.Collections.Generic;

namespace TechHub.Core.ViewModel.LiveClassModels
{
	public class CreateLiveClassViewModel
	{
		/// <summary>An Approved lesson the teacher owns (a head teacher may use any lesson in the school).</summary>
		public Guid LessonId { get; set; }

		/// <summary>1 to 3 students from the lesson's classroom. Only these can join.</summary>
		public List<Guid> StudentIds { get; set; } = new();

		/// <summary>Optional; defaults to the lesson's aim.</summary>
		public string? Title { get; set; }
	}

	public class SetSpeakViewModel
	{
		/// <summary>true = let this student speak, false = back to listen-only.</summary>
		public bool Allow { get; set; }
	}
}
