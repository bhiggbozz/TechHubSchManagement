//using System.Security.Claims;
//using TechhubMS.Middleware.Interface;
//using TechhubMS.Middleware.model;

//namespace TechhubMS.Middleware.Services
//{
//    public class UserService : IUserService
//    {
//        private readonly IHttpContextAccessor _httpContextAccessor;
//        private readonly ITenantService _tenantService;

//        public UserService(
//            IHttpContextAccessor httpContextAccessor,
//            ITenantService tenantService)
//        {
//            _httpContextAccessor = httpContextAccessor;
//            _tenantService = tenantService;
//        }

//        public string GetCurrentUserId()
//        {
//            var user = _httpContextAccessor.HttpContext?.User;

//            if (user?.Identity?.IsAuthenticated != true)
//                throw new UnauthorizedAccessException("User is not authenticated");

//            var userId = user.FindFirst(CustomClaimTypes.UserId)?.Value
//                         ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

//            if (string.IsNullOrEmpty(userId))
//                throw new InvalidOperationException("User ID not found in token");

//            return userId;
//        }

//        //public string GetCurrentSchoolId()
//        //{
//        //    //return _tenantService.GetCurrentSchoolId();
//        //}

//        public async Task<ClaimsPrincipal> GetCurrentUserAsync()
//        {
//            var user = _httpContextAccessor.HttpContext?.User;

//            if (user?.Identity?.IsAuthenticated != true)
//                throw new UnauthorizedAccessException("User is not authenticated");

//            return await Task.FromResult(user);
//        }

//        public bool IsAuthenticated()
//        {
//            return _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;
//        }

//		public string GetCurrentSchoolId()
//		{
//			throw new NotImplementedException();
//		}
//	}
//}
