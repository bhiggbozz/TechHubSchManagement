using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Service.util;

public static class EmailHelper
{
	// When creating user generate email token
	public static string GenerateEmailToken(Guid userId,Guid schoolId,string schoolCode,string email,EmailTokenPurpose purpose, IConfiguration configuration)
	{
		var claims = new[]
		{
			new Claim("userId", userId.ToString()),
			new Claim("schoolId", schoolId.ToString()),
			new Claim("schoolCode", schoolCode),
			new Claim("email", email),
			new Claim("purpose", purpose.ToString()),
			new Claim(JwtRegisteredClaimNames.Jti,Guid.NewGuid().ToString())
		};

		var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["EmailToken:SecretKey"]));

		var token = new JwtSecurityToken(
			claims: claims,
			expires: DateTime.UtcNow.AddHours(24),
			signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
			return new JwtSecurityTokenHandler().WriteToken(token);
	}

	public enum EmailTokenPurpose
	{
		WelcomeSetPassword,
		PasswordReset,
		EmailVerification
	}
}

