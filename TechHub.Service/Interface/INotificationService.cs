using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TechHub.Core;
using TechHub.Core.Model;

namespace TechHub.Service.Interface
{
	public interface INotificationService
	{
		// ── Recipient-facing (exposed via NotificationController) ────────────
		Task<BaseResponse> GetMyNotificationsAsync(AuthenticatedUserClaims claims, int page, int pageSize);
		Task<BaseResponse> MarkDeliveredAsync(List<Guid> notificationIds, AuthenticatedUserClaims claims);
		Task<BaseResponse> MarkReadAsync(Guid notificationId, AuthenticatedUserClaims claims);
		Task<BaseResponse> MarkAllReadAsync(AuthenticatedUserClaims claims);

		// ── Fan-out triggers, called internally by other services. Each one
		// resolves its own recipient list and never throws — a notification
		// failure must never fail the operation that triggered it. ──────────
		Task NotifyLessonPublishedAsync(Guid lessonId, Guid schoolId);
		Task NotifyAssessmentAssignedAsync(Guid assessmentId, string targetType, List<Guid> targetIds, Guid schoolId);
		Task NotifyGroupContentSubmittedAsync(Guid contentId, Guid approverId, Guid schoolId, string aim, string groupName);
		Task NotifyGroupContentApprovedAsync(Guid contentId, Guid schoolId);
	}
}
