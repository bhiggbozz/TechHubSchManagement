namespace TechhubMS;
using System.Data;


/// <summary>
/// Middleware to extract and set tenant context from subdomain
/// Runs early in the request pipeline
/// </summary>
public class MultiTenantMiddleware
{
	private readonly RequestDelegate _next;
	private readonly ILogger<MultiTenantMiddleware> _logger;

	public MultiTenantMiddleware(
		RequestDelegate next,
		ILogger<MultiTenantMiddleware> logger)
	{
		_next = next;
		_logger = logger;
	}

	public async Task InvokeAsync(HttpContext context, ITenantService tenantService)
	{
		var host = context.Request.Host.Host;

		// Extract tenant identifier from subdomain
		var tenantIdentifier = ExtractTenantFromHost(host);

		if (string.IsNullOrEmpty(tenantIdentifier))
		{
			_logger.LogWarning("No tenant identifier found in host: {Host}", host);
			context.Response.StatusCode = 400;
			await context.Response.WriteAsync("Invalid tenant. Please access via subdomain (e.g., pearl.myapp.com)");
			return;
		}

		_logger.LogInformation("Extracted tenant identifier: {Identifier} from host: {Host}", tenantIdentifier, host);

		// Query tenant from database
		var tenant = await tenantService.GetTenantByIdentifierAsync(tenantIdentifier);

		if (tenant == null)
		{
			_logger.LogWarning("Tenant not found: {Identifier}", tenantIdentifier);
			context.Response.StatusCode = 404;
			await context.Response.WriteAsync($"Tenant '{tenantIdentifier}' not found or inactive");
			return;
		}

		// Set tenant context in HttpContext.Items (accessible throughout request)
		context.Items["TenantId"] = tenant.Id;
		context.Items["SchoolId"] = tenant.SchoolId;
		context.Items["TenantIdentifier"] = tenant.Identifier;
		context.Items["TenantName"] = tenant.Name;

		_logger.LogInformation(
			"Tenant context set - Identifier: {Identifier}, SchoolId: {SchoolId}, Name: {Name}",
			tenant.Identifier,
			tenant.SchoolId,
			tenant.Name);

		// Continue to next middleware
		await _next(context);
	}

	/// <summary>
	/// Extracts tenant identifier from host
	/// Examples:
	///   - pearl.myapp.com → "pearl"
	///   - oxford.myapp.com → "oxford"
	///   - localhost → "dev-tenant" (for development)
	///   - myapp.com → null (no subdomain)
	/// </summary>
	private string? ExtractTenantFromHost(string host)
	{
		// Handle localhost for development
		if (host.Contains("localhost") || host.StartsWith("127.0.0.1"))
		{
			_logger.LogDebug("Localhost detected, using dev-tenant");
			return "dev-tenant";
		}

		// Remove port if present (e.g., pearl.myapp.com:5000)
		var hostWithoutPort = host.Split(':')[0];

		// Split by dots
		var parts = hostWithoutPort.Split('.');

		// Need at least 3 parts for subdomain (e.g., pearl.myapp.com)
		if (parts.Length < 3)
		{
			_logger.LogDebug("No subdomain found in host: {Host}", host);
			return null;
		}

		// First part is the tenant identifier
		var identifier = parts[0].ToLowerInvariant();

		// Validate identifier format (only lowercase letters, numbers, hyphens)
		if (!System.Text.RegularExpressions.Regex.IsMatch(identifier, @"^[a-z0-9-]+$"))
		{
			_logger.LogWarning("Invalid tenant identifier format: {Identifier}", identifier);
			return null;
		}

		return identifier;
	}
}




