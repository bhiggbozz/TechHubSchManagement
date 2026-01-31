using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using TechhubMS.Middleware.Interface;
using TechhubMS.Middleware.model;

namespace TechhubMS.Middleware.Services
{
    public class AuthService
    {
        private readonly IConfiguration _configuration;
        private readonly ITenantService _tenantService;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            IConfiguration configuration,
            ITenantService tenantService,
            ILogger<AuthService> logger)
        {
            _configuration = configuration;
            _tenantService = tenantService;
            _logger = logger;
        }

    //    public async Task<string> GenerateTokenAsync(string userId, string domain, string role = "Student")
    //    {
    //        var tenant = await _tenantService.GetTenantByDomainAsync(domain);

    //        var claims = new List<Claim>
    //    {
    //        new Claim(CustomClaimTypes.UserId, userId),
    //        new Claim(CustomClaimTypes.SchoolId, tenant.SchoolId),
    //        new Claim(CustomClaimTypes.SchoolName, tenant.SchoolName),
    //        new Claim(CustomClaimTypes.Domain, tenant.Domain),
    //        new Claim(CustomClaimTypes.Role, role),
    //        new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
    //    };

    //        var key = new SymmetricSecurityKey(
    //            Encoding.UTF8.GetBytes(_configuration["Jwt:SecretKey"]));

    //        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

    //        var token = new JwtSecurityToken(
    //            issuer: _configuration["Jwt:Issuer"],
    //            audience: _configuration["Jwt:Audience"],
    //            claims: claims,
    //            expires: DateTime.UtcNow.AddHours(8),
    //            signingCredentials: credentials
    //        );

    //        _logger.LogInformation(
    //            "Token generated for user {UserId} in school {SchoolId}",
    //            userId,
    //            tenant.SchoolId);

    //        return new JwtSecurityTokenHandler().WriteToken(token);
    //    }
    }
}
