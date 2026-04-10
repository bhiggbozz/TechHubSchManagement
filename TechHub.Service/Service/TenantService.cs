using Microsoft.AspNetCore.Http;
using Serilog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using TechHub.Core.Constant;
using TechHub.Core.Model;
using TechHub.Core.Models;
using TechHub.Core.Utilities;
using TechHub.Core.ViewModel;
using TechHub.Service.Interface;
using TechhubMS;

namespace TechHub.Service.Service;
/// <summary>
/// Tenant service implementation with caching for performance
/// </summary>
public class TenantService : ITenantService
{
	private readonly IQueryRepository<TenantInfo> _queryRepositoryTenant;
	//private readonly IDistributedCache _cache;
	private readonly ILogger _logger;
	private readonly IDbConnection _dbConnection;
	private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(30);
	private readonly IHttpContextAccessor _httpContextAccessor;


	public TenantService(
		IQueryRepository<TenantInfo> queryRepositoryTenant,
		//IDistributedCache cache,
		ILogger logger,
		IHttpContextAccessor httpContextAccessor)
	{
		_queryRepositoryTenant = queryRepositoryTenant;
		//_cache = cache;
		_logger = logger;
		_httpContextAccessor = httpContextAccessor;
	}

	public async Task<TenantInfo?> GetTenantByIdentifierAsync(string identifier)
	{
		if (string.IsNullOrWhiteSpace(identifier))
		{
			return null;
		}

		var cacheKey = $"tenant:identifier:{identifier.ToLowerInvariant()}";

		// Try cache first
		//var cachedTenant = await _cache.GetStringAsync(cacheKey);
		//if (!string.IsNullOrEmpty(cachedTenant))
		//{
		//	_logger.("Tenant {Identifier} found in cache", identifier);
		//	return JsonSerializer.Deserialize<TenantInfo>(cachedTenant);
		//}

		// Cache miss - query database
		_logger.Warning("Tenant {Identifier} not in cache, querying database", identifier);

		try
		{
			// Query your tenant/school table
			var sql = @"
				SELECT 
					Id,
				    SchoolId,
					Identifier,
					IsActive,
					ConnectionString,
					CreatedDate,
					ModifiedDate
				FROM TenantInfo 
				WHERE Identifier = LOWER(@Identifier)
				AND IsActive = 1";
			var columnInput = new Dictionary<string, object> { { "Identifier", identifier.ToLower() } };

			//var tenant = await _dbConnection.QueryFirstOrDefaultAsync<TenantInfo>(
			//	sql,
			//	new { Identifier = identifier });

			var tenant = await _queryRepositoryTenant.SelectByColumns(sql, columnInput);


			if (tenant != null)
			{
				// Cache the result
				//var cacheOptions = new DistributedCacheEntryOptions
				//{
				//	AbsoluteExpirationRelativeToNow = _cacheDuration
				//};

				//await _cache.SetStringAsync(
				//	cacheKey,
				//	JsonSerializer.Serialize(tenant),
				//	cacheOptions);

				_logger.Warning(
					"Tenant {Identifier} loaded from database and cached (SchoolId: {SchoolId})",
					identifier,
					tenant.SchoolId);
				return tenant;
			}
			else
			{
				_logger.Warning("Tenant {Identifier} not found in database", identifier);
			}

			return tenant;
		}
		catch (Exception ex)
		{
			_logger.Error(ex, "Error retrieving tenant by identifier: {Identifier}", identifier);
			throw;
		}
	}

	public async Task<TenantInfo?> GetTenantBySchoolIdAsync(Guid schoolId)
	{
		var cacheKey = $"tenant:schoolid:{schoolId}";

		// Try cache first
		//var cachedTenant = await _cache.GetStringAsync(cacheKey);
		//if (!string.IsNullOrEmpty(cachedTenant))
		//{
		//	return JsonSerializer.Deserialize<TenantInfo>(cachedTenant);
		//}

		try
		{
			var sql = @"
                SELECT 
                    SchoolId as TenantId,
                    Identifier,
                    SchoolId,
					ConnectionString,
                    IsActive,
                    CreatedDate as CreatedAt
                FROM TenantInfo 
                WHERE SchoolId = @SchoolId 
                AND IsActive = 1";

			//var tenant = await _dbConnection.QueryFirstOrDefaultAsync<TenantInfo>(
			//	sql,
			//	new { SchoolId = schoolId });

			var columnInput = new Dictionary<string, object> { { "SchoolId", schoolId } };

			//var tenant = await _dbConnection.QueryFirstOrDefaultAsync<TenantInfo>(
			//	sql,
			//	new { Identifier = identifier });

			var tenant = await _queryRepositoryTenant.SelectByColumns(sql, columnInput);

			//if (tenant != null)
			//{
			//	//	var cacheOptions = new DistributedCacheEntryOptions
			//	//	{
			//	//		AbsoluteExpirationRelativeToNow = _cacheDuration
			//	//	};

			//	//	await _cache.SetStringAsync(
			//	//		cacheKey,
			//	//		JsonSerializer.Serialize(tenant),
			//	//		cacheOptions);
			//	return tenant;

			//}

			return tenant;
		}
		catch (Exception ex)
		{
			_logger.Error(ex, "Error retrieving tenant by SchoolId: {SchoolId}", schoolId);
			throw;
		}
	}

	public async Task<bool> IsTenantActiveAsync(string identifier)
	{
		var tenant = await GetTenantByIdentifierAsync(identifier);
		return tenant?.IsActive ?? false;
	}

	public async Task<string?> GetTenantConnectionStringAsync(string identifier)
	{
		var tenant = await GetTenantByIdentifierAsync(identifier);
		return tenant?.ConnectionString;
	}
	public async Task<TenantInfo?> GetTenantByDomainAsync(string domain)
	{
		return await GetTenantByIdentifierAsync(domain);
	}

	//public async Task InvokeAsync(HttpContext context, ITenantService tenantService)
	//{
	//	var host = context.Request.Host.Host;
	//	var tenantIdentifier = ExtractTenantFromHost(host);

	//	// Uses: GetTenantByIdentifierAsync
	//	var tenant = await tenantService.GetTenantByIdentifierAsync(tenantIdentifier);

	//	if (tenant != null)
	//	{
	//		context.Items["SchoolId"] = tenant.SchoolId;
	//		context.Items["TenantIdentifier"] = tenant.Identifier;
	//	}
	//}

	//public async Task<string> GenerateTokenAsync(string userId, string domain, string role)
	//{
	//	// Uses: GetTenantByDomainAsync (which calls GetTenantByIdentifierAsync)
	//	var tenant = await _tenantService.GetTenantByDomainAsync(domain);

	//	var claims = new List<Claim>
	//	{
	//		new Claim(CustomClaimTypes.UserId, userId),
	//		new Claim(CustomClaimTypes.SchoolId, tenant.SchoolId.ToString()),
	//		// ...
	//	};
	//}

	public TenantInfo? GetCurrentTenant()
	{
		var httpContext = _httpContextAccessor.HttpContext;
		if (httpContext == null)
			return null;

		var schoolIdObj = httpContext.Items["SchoolId"];
		var tenantIdentifier = httpContext.Items["TenantIdentifier"] as string;
		var tenantName = httpContext.Items["TenantName"] as string;

		Guid? schoolId = null;
		if (schoolIdObj is Guid guidValue)
			schoolId = guidValue;
		else if (schoolIdObj is string str && Guid.TryParse(str, out var parsed))
			schoolId = parsed;

		if (!schoolId.HasValue || string.IsNullOrEmpty(tenantIdentifier))
			return null;

		return new TenantInfo
		{
			SchoolId = schoolId.Value,
			Identifier = tenantIdentifier,
			Name = tenantName ?? string.Empty,
			IsActive = true
		};
	}

	public Guid GetCurrentSchoolId()
	{
		var tenant = GetCurrentTenant();
		if (tenant == null)
		{
			throw new InvalidOperationException(
				"Tenant context not available. Ensure MultiTenantMiddleware is running.");
		}
		return tenant.SchoolId;
	}

	public string? GetCurrentTenantIdentifier()
	{
		return GetCurrentTenant()?.Identifier;
	}

	
}