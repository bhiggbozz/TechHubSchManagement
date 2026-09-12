using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using TechHub.Core;
using TechHub.Core.Model;
using TechHub.Service.Extension;
using TechHub.Service.Interface;

namespace TechhubMS.Controllers
{
	[ApiController]
	[Route("api/Notification")]
	[Authorize]
	public class NotificationController : ControllerBase
	{
		private readonly INotificationService _notificationService;

		public NotificationController(INotificationService notificationService)
		{
			_notificationService = notificationService;
		}

		[HttpGet("my")]
		public async Task<ActionResult<BaseResponse>> GetMyNotifications(int page = 1, int pageSize = 20)
		{
			var claims = User.GetAuthenticatedUserClaims();
			var result = await _notificationService.GetMyNotificationsAsync(claims, page, pageSize);
			return result.ResponseCode == ResponseCode.successful ? Ok(result) : BadRequest(result);
		}

		[HttpPost("mark-delivered")]
		public async Task<ActionResult<BaseResponse>> MarkDelivered([FromBody] List<Guid> ids)
		{
			var claims = User.GetAuthenticatedUserClaims();
			var result = await _notificationService.MarkDeliveredAsync(ids, claims);
			return result.ResponseCode == ResponseCode.successful ? Ok(result) : BadRequest(result);
		}

		[HttpPost("{id:guid}/read")]
		public async Task<ActionResult<BaseResponse>> MarkRead(Guid id)
		{
			var claims = User.GetAuthenticatedUserClaims();
			var result = await _notificationService.MarkReadAsync(id, claims);
			return result.ResponseCode == ResponseCode.successful ? Ok(result) : BadRequest(result);
		}

		[HttpPost("read-all")]
		public async Task<ActionResult<BaseResponse>> MarkAllRead()
		{
			var claims = User.GetAuthenticatedUserClaims();
			var result = await _notificationService.MarkAllReadAsync(claims);
			return result.ResponseCode == ResponseCode.successful ? Ok(result) : BadRequest(result);
		}
	}
}
