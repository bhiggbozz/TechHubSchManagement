namespace TechHub.Core.Model
{
	// Mirrors the OperationType constants pattern used for ApprovalRequests —
	// a plain string so it round-trips through the Notification table without
	// needing an enum-to-int mapping.
	public static class NotificationTypes
	{
		public const string LessonPublished = "LessonPublished";
		public const string AssessmentAssigned = "AssessmentAssigned";
		public const string GroupContentSubmitted = "GroupContentSubmitted";
		public const string GroupContentApproved = "GroupContentApproved";
	}
}
