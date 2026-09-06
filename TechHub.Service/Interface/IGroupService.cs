using System;
using System.Threading.Tasks;
using TechHub.Core;
using TechHub.Core.Model;
using TechHub.Core.ViewModel.Groups;

namespace TechHub.Service.Interface
{
	public interface IGroupService
	{
		Task<BaseResponse> CreateGroup(CreateGroupViewModel model, AuthenticatedUserClaims claims);
		Task<BaseResponse> AddMembers(Guid groupId, AddGroupMembersViewModel model, AuthenticatedUserClaims claims);
		Task<BaseResponse> RemoveMember(Guid groupId, Guid studentId, AuthenticatedUserClaims claims);
		Task<BaseResponse> GetMyGroups(AuthenticatedUserClaims claims);
		Task<BaseResponse> GetGroupDetail(Guid groupId, AuthenticatedUserClaims claims);
		Task<BaseResponse> SubmitContent(Guid groupId, SubmitGroupContentViewModel model, AuthenticatedUserClaims claims);
		Task<BaseResponse> GetContentDetail(Guid groupId, Guid contentId, AuthenticatedUserClaims claims);
	}
}
