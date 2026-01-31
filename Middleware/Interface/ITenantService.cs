using TechhubMS.Middleware.model;

namespace TechhubMS.Middleware.Interface
{
    public interface ITenantService
    {
        Task<TenantContext> GetTenantByDomainAsync(string domain);
        TenantContext GetCurrentTenant();
        string GetCurrentSchoolId();
    }
}
