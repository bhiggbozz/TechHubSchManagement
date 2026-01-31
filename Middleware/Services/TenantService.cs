//using TechHub.Core.Models;
//using TechhubMS.Middleware.Interface;
//using TechhubMS.Middleware.model;

//namespace TechhubMS.Middleware.Services
//{
//    public class TenantService : ITenantService
//    {
//        private readonly IHttpContextAccessor _httpContextAccessor;
//        private readonly IConfiguration _configuration;
//        private readonly ILogger<TenantService> _logger;

//        public TenantService(
//            IHttpContextAccessor httpContextAccessor,
//            IConfiguration configuration,
//            ILogger<TenantService> logger)
//        {
//            _httpContextAccessor = httpContextAccessor;
//            _configuration = configuration;
//            _logger = logger;
//        }

//        public async Task<TenantContext> GetTenantByDomainAsync(string domain)
//        {
//            // This would typically query your database
//            // For demo purposes, using a simple lookup
//            var school = await GetSchoolFromDatabaseAsync(domain);

//            if (school == null)
//            {
//                _logger.LogWarning("School not found for domain: {Domain}", domain);
//                throw new UnauthorizedAccessException($"Invalid school domain: {domain}");
//            }

//            return new TenantContext
//            {
//                SchoolId = school.Id,
//                SchoolName = school.Name,
//                Domain = domain
//            };
//        }

//        public TenantContext GetCurrentTenant()
//        {
//            var user = _httpContextAccessor.HttpContext?.User;

//            if (user?.Identity?.IsAuthenticated != true)
//                throw new UnauthorizedAccessException("User is not authenticated");

//            var schoolId = user.FindFirst(CustomClaimTypes.SchoolId)?.Value;
//            var schoolName = user.FindFirst(CustomClaimTypes.SchoolName)?.Value;
//            var domain = user.FindFirst(CustomClaimTypes.Domain)?.Value;

//            if (string.IsNullOrEmpty(schoolId))
//                throw new InvalidOperationException("School information not found in token");

//            return new TenantContext
//            {
//                SchoolId = schoolId,
//                SchoolName = schoolName,
//                Domain = domain
//            };
//        }

//        public string GetCurrentSchoolId()
//        {
//            return GetCurrentTenant().SchoolId;
//        }

//        private async Task<dynamic> GetSchoolFromDatabaseAsync(string domain)
//        {
//            // Replace with actual database query
//            // Example: await _dbContext.Schools.FirstOrDefaultAsync(s => s.Domain == domain);
//            await Task.CompletedTask;

//            return new { Id = "school123", Name = domain, Domain = domain };
//        }

//		public Task<TenantInfo?> GetTenantByIdentifierAsync(string identifier)
//		{
//			throw new NotImplementedException();
//		}

//		public Task<TenantInfo?> GetTenantBySchoolIdAsync(int schoolId)
//		{
//			throw new NotImplementedException();
//		}

//		public Task<bool> IsTenantActiveAsync(string identifier)
//		{
//			throw new NotImplementedException();
//		}

//		public Task<string?> GetTenantConnectionStringAsync(string identifier)
//		{
//			throw new NotImplementedException();
//		}
//	}
//}
