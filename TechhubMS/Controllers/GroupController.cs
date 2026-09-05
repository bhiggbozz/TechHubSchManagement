using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechHub.Core;
using TechHub.Core.Model;
using TechHub.Core.ViewModel.Groups;
using TechHub.Service.Extension;
using TechHub.Service.Interface;

namespace TechhubMS.Controllers
{
	[ApiController]
	[Route("api/groups")]
	[Authorize]
	public class GroupController : ControllerBase
	{
		private readonly IGroupService _groupService;

		public GroupController(IGroupService groupService)
		{
			_groupService = groupService;
		}

		[HttpPost("create")]
		public async Task<ActionResult<BaseResponse>> CreateGroup(CreateGroupViewModel model)
		{
			var claims = User.GetAuthenticatedUserClaims();
			var result = await _groupService.CreateGroup(model, claims);
			return result.ResponseCode == ResponseCode.successful ? Ok(result) : BadRequest(result);
		}

		[HttpPost("{groupId:guid}/members")]
		public async Task<ActionResult<BaseResponse>> AddMembers(Guid groupId, AddGroupMembersViewModel model)
		{
			var claims = User.GetAuthenticatedUserClaims();
			var result = await _groupService.AddMembers(groupId, model, claims);
			return result.ResponseCode == ResponseCode.successful ? Ok(result) : BadRequest(result);
		}

		/// <summary>Removes a student from the group. Only the group's creator can do this.</summary>
		[HttpDelete("{groupId:guid}/members/{studentId:guid}")]
		public async Task<ActionResult<BaseResponse>> RemoveMember(Guid groupId, Guid studentId)
		{
			var claims = User.GetAuthenticatedUserClaims();
			var result = await _groupService.RemoveMember(groupId, studentId, claims);
			return result.ResponseCode == ResponseCode.successful ? Ok(result) : BadRequest(result);
		}

		[HttpGet("my-groups")]
		public async Task<ActionResult<BaseResponse>> GetMyGroups()
		{
			var claims = User.GetAuthenticatedUserClaims();
			var result = await _groupService.GetMyGroups(claims);
			return result.ResponseCode == ResponseCode.successful ? Ok(result) : BadRequest(result);
		}

		[HttpGet("{groupId:guid}")]
		public async Task<ActionResult<BaseResponse>> GetGroupDetail(Guid groupId)
		{
			var claims = User.GetAuthenticatedUserClaims();
			var result = await _groupService.GetGroupDetail(groupId, claims);
			return result.ResponseCode == ResponseCode.successful ? Ok(result) : BadRequest(result);
		}

		[HttpPost("{groupId:guid}/content")]
		public async Task<ActionResult<BaseResponse>> SubmitContent(Guid groupId, SubmitGroupContentViewModel model)
		{
			var claims = User.GetAuthenticatedUserClaims();
			var result = await _groupService.SubmitContent(groupId, model, claims);
			return result.ResponseCode == ResponseCode.successful ? Ok(result) : BadRequest(result);
		}
	}
}
