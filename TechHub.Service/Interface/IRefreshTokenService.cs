using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core;

namespace TechHub.Service.Interface;

public interface IRefreshTokenService
{
	Task<BaseResponse> RefreshAsync(string refreshToken);
	Task<string> GenerateAndStoreRefreshToken(IDbTransaction transaction, IDbConnection connection,Guid userId, Guid schoolId);
}

