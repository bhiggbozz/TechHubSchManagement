namespace TechhubMS;

using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

/// <summary>
/// Removes the X-Tenant-ID security requirement from platform endpoints
/// (PlatformAuth / PlatformAdmin) — they are tenant-independent.
/// </summary>
public class RemoveTenantRequirementOperationFilter : IOperationFilter
{
	private static readonly string[] TenantFreePrefixes =
	{
		"/api/PlatformAuth",
		"/api/PlatformAdmin"
	};

	public void Apply(OpenApiOperation operation, OperationFilterContext context)
	{
		var path = context.ApiDescription.RelativePath;
		if (string.IsNullOrEmpty(path))
			return;

		var normalized = "/" + path.TrimEnd('/');

		foreach (var prefix in TenantFreePrefixes)
		{
			if (!normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
				continue;

			var security = operation.Security;

			if (security is not null)
			{
				var tenantRequirements = security
					.Where(r => r.Keys.Any(s => s.Reference != null && s.Reference.Id == "TenantId"))
					.ToList();

				foreach (var requirement in tenantRequirements)
					security.Remove(requirement);
			}

			return;
		}
	}
}
