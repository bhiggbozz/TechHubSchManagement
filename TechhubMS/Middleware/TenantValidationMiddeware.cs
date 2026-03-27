using Microsoft.AspNetCore.Http;
using Serilog;
using TechhubMS.Middleware.Interface;
using TechhubMS.Middleware.model;
using ILogger = Serilog.ILogger;

namespace TechhubMS.Middleware
{
    public class TenantValidationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger _logger;

        public TenantValidationMiddleware(
            RequestDelegate next,
            ILogger logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, ITenantService tenantService)
        {
            // Skip validation for authentication endpoints
            if (context.Request.Path.StartsWithSegments("/api/auth"))
            {
                await _next(context);
                return;
            }

            if (context.User?.Identity?.IsAuthenticated == true)
            {
                try
                {
                    var tokenSchoolId = context.User.FindFirst(CustomClaimTypes.SchoolId)?.Value;
                    var tokenDomain = context.User.FindFirst(CustomClaimTypes.Domain)?.Value;
                    var requestHost = context.Request.Host.Host;
                    var subdomain = ExtractSubdomain(requestHost);

                    // Validate that token's school matches the request subdomain
                    if (!string.IsNullOrEmpty(subdomain) &&
                        !string.IsNullOrEmpty(tokenDomain) &&
                        !subdomain.Equals(tokenDomain, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.Warning(
                            "School mismatch: Token domain {TokenDomain} doesn't match request subdomain {RequestDomain}",
                            tokenDomain,
                            subdomain);

                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        await context.Response.WriteAsJsonAsync(new
                        {
                            error = "Invalid tenant access",
                            message = "Token is not valid for this school"
                        });
                        return;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error validating tenant");
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    await context.Response.WriteAsJsonAsync(new { error = "Tenant validation failed" });
                    return;
                }
            }

            await _next(context);
        }

        private string ExtractSubdomain(string host)
        {
            // Extract subdomain from host (e.g., pearl.portal.com -> pearl)
            var parts = host.Split('.');
            return parts.Length > 2 ? parts[0] : string.Empty;
        }
    }
}
