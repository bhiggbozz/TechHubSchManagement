using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.ResponseModel;

public class TokenResponse
{
	public string AccessToken { get; set; }
	public string RefreshToken { get; set; }
	public DateTime AccessTokenExpiry { get; set; }
	public DateTime RefreshTokenExpiry { get; set; }
}

