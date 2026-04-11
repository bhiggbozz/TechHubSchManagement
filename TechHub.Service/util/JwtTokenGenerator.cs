using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core.Enum;
using TechHub.Core.Model;
using TechHub.Core.ResponseModel;

namespace TechHub.Service.util;

public class JwtTokenGenerator
{
	private readonly IConfiguration _configuration;

	public JwtTokenGenerator(IConfiguration configuration)
	{
		_configuration = configuration;
	}

	public string Generate(Users user, string tenantId, SchoolResponseModel schoolInfo)
	{
		var claims = new List<Claim>
		{
			new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
			new Claim(ClaimTypes.Name,           user.UserName ?? string.Empty),
			new Claim(ClaimTypes.Email,          user.EmailAddress ?? string.Empty),
			new Claim(ClaimTypes.Role,           ((UserRole)user.RoleId).ToString()),
			new Claim("SchoolId",                user.SchoolId.ToString()),
			new Claim("TenantId",               tenantId ?? string.Empty),
			new Claim("SchoolName",              schoolInfo?.SchoolName ?? string.Empty),
			new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
			new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString())
		};

		if (!string.IsNullOrEmpty(user.FirstName))
			claims.Add(new Claim(ClaimTypes.GivenName, user.FirstName));

		if (!string.IsNullOrEmpty(user.LastName))
			claims.Add(new Claim(ClaimTypes.Surname, user.LastName));

		var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:SecretKey"]));
		var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
		var expires = DateTime.UtcNow.AddHours(1);

		var token = new JwtSecurityToken(
			issuer: _configuration["Jwt:Issuer"],
			audience: _configuration["Jwt:Audience"],
			claims: claims,
			expires: expires,
			signingCredentials: credentials
		);

		return new JwtSecurityTokenHandler().WriteToken(token);
	}
}

