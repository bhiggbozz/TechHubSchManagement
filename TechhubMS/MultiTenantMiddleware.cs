namespace TechhubMS;

using Microsoft.AspNetCore.Http;
using Serilog;
using System.Data;


/// <summary>
/// Middleware to extract and set tenant context from subdomain
/// Runs early in the request pipeline
/// </summary>
public class MultiTenantMiddleware
{
	private readonly RequestDelegate _next;
	private readonly ILogger _logger;

	public MultiTenantMiddleware(
		RequestDelegate next,
		ILogger logger)
	{
		_next = next;
		_logger = logger;
	}

	public async Task InvokeAsync(HttpContext context, ITenantService tenantService)
	{
		if (context.Request.Path.StartsWithSegments("/swagger")
		    || string.Equals(context.Request.Path, "/api/School/createschool", StringComparison.OrdinalIgnoreCase)
		    || string.Equals(context.Request.Path, "/api/School/registration-requests", StringComparison.OrdinalIgnoreCase)
		    || string.Equals(context.Request.Path, "/api/School/register", StringComparison.OrdinalIgnoreCase)
		    || context.Request.Path.StartsWithSegments("/api/School/approve")
		    || context.Request.Path.StartsWithSegments("/api/School/reject")
		    || string.Equals(context.Request.Path, "/api/School/provision", StringComparison.OrdinalIgnoreCase)
		    || context.Request.Path.Value != null && context.Request.Path.Value.Contains("/approval-status", StringComparison.OrdinalIgnoreCase)
		    || string.Equals(context.Request.Path, "/api/PlatformAuth/login", StringComparison.OrdinalIgnoreCase))
		{
			await _next(context);
			return;
		}

		string? tenantIdentifier = null;

		//Priority 1: Check X-Tenant-ID header (from frontend)
		if (context.Request.Headers.TryGetValue("X-Tenant-ID", out var headerTenant))
		{
			tenantIdentifier = headerTenant.ToString();
			_logger.Warning(
				"Tenant from header: {Tenant}",
				tenantIdentifier);
		}
		// Priority 2: Extract from subdomain (direct API access/Swagger/Postman)
		else
		{
			var host = context.Request.Host.Host;
			tenantIdentifier = ExtractTenantFromHost(host);

			if (!string.IsNullOrEmpty(tenantIdentifier))
			{
				_logger.Warning(
					"Tenant from subdomain: {Host} → {Tenant}",
					host,
					tenantIdentifier);
			}
			else
			{
				_logger.Warning(
					"No tenant found in subdomain: {Host}",
					host);
			}
		}

		// Validate tenant identifier exists
		if (string.IsNullOrEmpty(tenantIdentifier))
		{
			_logger.Warning(
				"No tenant identifier found. Host: {Host}, HasHeader: {HasHeader}",
				context.Request.Host.Host,
				context.Request.Headers.ContainsKey("X-Tenant-ID"));

			context.Response.StatusCode = 400;
			context.Response.ContentType = "application/json";
			await context.Response.WriteAsJsonAsync(new
			{
				error = "TENANT_REQUIRED",
				message = "Tenant identifier required. Access your school via its subdomain (e.g., kingscollege.bluetsch.com) or include X-Tenant-ID header."
			});
			return;
		}

		// Query tenant from database
		var tenant = await tenantService.GetTenantByIdentifierAsync(tenantIdentifier);

		if (tenant == null)
		{
			_logger.Warning(
				"Tenant not found or inactive: {Identifier}",
				tenantIdentifier);

			context.Response.StatusCode = 404;
			context.Response.ContentType = "application/json";
			await context.Response.WriteAsJsonAsync(new
			{
				error = "TENANT_NOT_FOUND",
				message = $"Tenant '{tenantIdentifier}' not found or inactive."
			});
			return;
		}

		context.Items["TenantId"] = tenant.Id;
		context.Items["SchoolId"] = tenant.SchoolId;
		context.Items["TenantIdentifier"] = tenant.Identifier;
		context.Items["TenantName"] = tenant.Name;

		_logger.Warning(
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
	///   - kingscollege.bluetsch.com → "kingscollege"
	///   - pearl.bluethub.online → "pearl"
	///   - school-a.onrender.com → "school-a"
	///   - pearl.myapp.com → "pearl"
	///   - oxford.myapp.com → "oxford"
	///   - api.bluetsch.com / www.bluetsch.com → null (reserved)
	///   - bluetsch.com → null (no subdomain)
	///   - localhost → "dev-tenant" (for development)
	/// </summary>
	private string? ExtractTenantFromHost(string host)
	{
		if (string.IsNullOrWhiteSpace(host))
			return null;

		var hostWithoutPort = host.Split(':')[0].ToLowerInvariant();

		// Production: *.bluetsch.com → subdomain
		//   kingscollege.bluetsch.com → "kingscollege"
		if (hostWithoutPort.EndsWith(".bluetsch.com"))
		{
			var parts = hostWithoutPort.Split('.');
			if (parts.Length >= 3)
			{
				var subdomain = parts[0];

				if (subdomain == "www" || subdomain == "api" || subdomain == "admin")
					return null;

				return subdomain;
			}
		}

		// Production: *.bluethub.online → subdomain
		//   pearl.bluethub.online → "pearl"
		if (hostWithoutPort.EndsWith(".bluethub.online"))
		{
			var parts = hostWithoutPort.Split('.');
			if (parts.Length >= 3)
			{
				var subdomain = parts[0];

				if (subdomain == "www" || subdomain == "api" || subdomain == "admin")
					return null;

				return subdomain;
			}
		}

		// Staging: *.onrender.com → subdomain
		if (hostWithoutPort.EndsWith(".onrender.com"))
		{
			var parts = hostWithoutPort.Split('.');
			if (parts.Length >= 3)
			{
				var subdomain = parts[0];

				if (subdomain == "www" || subdomain == "api" || subdomain == "admin")
					return null;

				return subdomain;
			}
		}

		// Local development: *.localhost → subdomain
		if (hostWithoutPort.EndsWith(".localhost"))
		{
			var parts = hostWithoutPort.Split('.');
			if (parts.Length >= 2)
			{
				var subdomain = parts[0];

				// Plain "localhost" has no tenant
				if (subdomain == "localhost")
					return null;

				return subdomain;
			}
		}

		return null;
	}
}




