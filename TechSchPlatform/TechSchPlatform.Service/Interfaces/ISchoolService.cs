using TechSchPlatform.Core;
using TechSchPlatform.Core.Model;
using TechSchPlatform.Core.ViewModel.Platform;

namespace TechSchPlatform.Service.Interfaces;

public interface ISchoolService
{
    Task<BaseResponse> CreateSchoolAsync(CreateSchoolViewModel model, AuthenticatedUserClaims claims);
    Task<BaseResponse> ProvisionSchoolAsync(ProvisionSchoolViewModel model, AuthenticatedUserClaims claims);
    Task<BaseResponse> SubmitRegistrationRequestAsync(SchoolRegistrationRequestViewModel model);
    Task<BaseResponse> GetRegistrationRequestsAsync(string? statusFilter);
    Task<BaseResponse> ApproveRegistrationRequestAsync(Guid requestId, AuthenticatedUserClaims claims);
    Task<BaseResponse> RejectRegistrationRequestAsync(Guid requestId, string reason, AuthenticatedUserClaims claims);
    Task<BaseResponse> EditSchoolInfoAsync(Guid schoolId, SchoolEditViewModel model, AuthenticatedUserClaims claims);
    Task<BaseResponse> GetAllSchoolsWithStatusAsync();
    Task<BaseResponse> GetPendingSchoolIdsAsync();
    Task<BaseResponse> GetSchoolApprovalStatusAsync(Guid schoolId);
    Task<BaseResponse> GetStatesAsync(int countryId);
}