using TechHub.Core.Models;

namespace TechhubMS;

public interface ITenantService
{
	// Database queries
	Task<TenantInfo?> GetTenantByIdentifierAsync(string identifier);
	Task<TenantInfo?> GetTenantBySchoolIdAsync(Guid schoolId);
	Task<TenantInfo?> GetTenantByDomainAsync(string domain);
	Task<bool> IsTenantActiveAsync(string identifier);
	Task<string?> GetTenantConnectionStringAsync(string identifier);

	// HTTP context access
	TenantInfo? GetCurrentTenant();
	Guid GetCurrentSchoolId();
	string? GetCurrentTenantIdentifier();

	// Cache management
	//Task InvalidateTenantCacheAsync(string identifier);
	//Task InvalidateTenantCacheBySchoolIdAsync(Guid schoolId);
}
//	/// <summary>
//	/// Gets tenant information by identifier (subdomain)
//	/// Example: "pearl" -> Tenant with SchoolId
//	/// </summary>
//	Task<TenantInfo?> GetTenantByIdentifierAsync(string identifier);

//	/// <summary>
//	/// Gets tenant information by SchoolId
//	/// </summary>
//	Task<TenantInfo?> GetTenantBySchoolIdAsync(int schoolId);

//	/// <summary>
//	/// Validates if a tenant exists and is active
//	/// </summary>
//	Task<bool> IsTenantActiveAsync(string identifier);

//	/// <summary>
//	/// Gets connection string for a specific tenant (if using database-per-tenant)
//	/// </summary>
//	Task<string?> GetTenantConnectionStringAsync(string identifier);
//}

/// <summary>
/// Tenant information model
/// </summary>
//public class TenantInfo
//{
//	public int TenantId { get; set; }
//	public string Identifier { get; set; } = string.Empty; // pearl, xyz, abc
//	public int SchoolId { get; set; }
//	public string Name { get; set; } = string.Empty; // Pearl School
//	public bool IsActive { get; set; }
//	public string? ConnectionString { get; set; } // For database-per-tenant
//	public DateTime CreatedAt { get; set; }
//	public Dictionary<string, string>? Settings { get; set; } // Additional tenant settings
//}



