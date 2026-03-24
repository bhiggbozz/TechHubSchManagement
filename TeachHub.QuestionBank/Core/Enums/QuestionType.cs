using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.QuestionBank.Core.Enums
{
	public enum QuestionType
	{
		MultipleChoice = 1,
		ShortAnswer = 2,
		Essay = 3,
		TrueOrFalse = 4,
		FillInTheBlank = 5,
		ImageBased = 6,   // Full image question
		BoardBased = 7,   // Board session attached
		Mixed = 8    // Text + board/image
	}

	public enum DifficultyLevel
	{
		Easy = 1,
		Medium = 2,
		Hard = 3,
		ExamLevel = 4
	}

	public enum QuestionStatus
	{
		PendingReview = -1,
		Draft = 0,
		Published = 1,
		Archived = 2,
		FlaggedForReview = 3
	}

	public enum SyncStatus
	{
		PendingSync = 0,
		Syncing = 1,
		Synced = 2,
		Dirty = 3,   // Edited after sync
		Conflict = 4,   // Needs resolution
		Failed = 5    // Sync failed, retry
	}

	public enum ScanSessionStatus
	{
		Processing = 0,

		PendingReview = 1,

		InReview = 2,

		ReviewComplete = 3,

		Abandoned = 4,

		Failed = 5
	}
}
