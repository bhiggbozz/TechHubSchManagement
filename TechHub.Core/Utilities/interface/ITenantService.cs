//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using TechHub.Core.Models;

//namespace TechHub.Core.Utilities;
//	/// <summary>
//	/// Service for resolving tenant information from subdomain/identifier
//	/// </summary>
//public interface ITenantService
//{
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


