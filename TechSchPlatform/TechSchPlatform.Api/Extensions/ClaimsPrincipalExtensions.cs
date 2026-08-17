using System.Security.Claims;
using TechSchPlatform.Core.Model;

namespace TechSchPlatform.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static AuthenticatedUserClaims GetAuthenticatedUserClaims(this ClaimsPrincipal user) => new()
    {
        UserId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value,
        Email = user.FindFirst(ClaimTypes.Email)?.Value,
        Role = user.FindFirst(ClaimTypes.Role)?.Value,
        Username = user.FindFirst(ClaimTypes.Name)?.Value
    };
}