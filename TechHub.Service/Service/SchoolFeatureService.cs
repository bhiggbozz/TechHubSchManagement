using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Serilog;
using TechHub.Core;
using TechHub.Core.Entities;
using TechHub.Core.Model;
using TechHub.Core.ViewModel;
using TechHub.Service.Interface;

namespace TechHub.Service.Service;

/// <summary>
/// Backed by the SchoolFeature table. Every privileged feature is gated here
/// before the corresponding capability is exercised by a school.
/// </summary>
public class SchoolFeatureService : ISchoolFeatureService
{
	private readonly IQueryRepository<SchoolFeature> _featureQuery;
	private readonly ICommandRespository<SchoolFeature> _featureCommand;
	private readonly ILogger _logger;

	public SchoolFeatureService(
		IQueryRepository<SchoolFeature> featureQuery,
		ICommandRespository<SchoolFeature> featureCommand,
		ILogger logger)
	{
		_featureQuery = featureQuery;
		_featureCommand = featureCommand;
		_logger = logger;
	}

	public async Task<BaseResponse> SetFeatureAsync(SchoolFeatureViewModel model, AuthenticatedUserClaims claims)
	{
		try
		{
			if (model is null)
				return BadRequest("Feature data is required");

			if (!Guid.TryParse(claims.UserId, out var userId))
				return Unauthorized();

			Guid.TryParse(claims.SchoolId, out var callerSchoolId);

			if (string.IsNullOrWhiteSpace(model.FeatureKey))
				return BadRequest("FeatureKey is required");

			// ── Authorization ────────────────────────────────────────────
			var isPlatformAdmin = claims.Role is "PlatformSuperAdmin" or "PlatformAdmin";
			var isSchoolAdmin = claims.Role is "Administrator" or "SuperAdministrator";

			if (!isPlatformAdmin && !isSchoolAdmin)
				return Forbidden("You do not have permission to configure school features");

			// School admins may only configure their own school (default to it).
			var targetSchoolId = model.SchoolId == Guid.Empty ? callerSchoolId : model.SchoolId;

			if (targetSchoolId == Guid.Empty)
				return BadRequest("SchoolId is required");

			if (isSchoolAdmin && !isPlatformAdmin && targetSchoolId != callerSchoolId)
				return Forbidden("You can only configure features for your own school");

			// ── Upsert (SchoolId + FeatureKey) ───────────────────────────
			var existing = (await _featureQuery.QueryAsync<SchoolFeature>(
				"SELECT * FROM SchoolFeature WHERE SchoolId = @SchoolId AND FeatureKey = @FeatureKey AND IsActive = 1",
				new Dictionary<string, object>
				{
					{ "SchoolId", targetSchoolId },
					{ "FeatureKey", model.FeatureKey.Trim() }
				})).FirstOrDefault();

			if (existing is null)
			{
				var entity = new SchoolFeature
				{
					Id = Guid.NewGuid(),
					SchoolId = targetSchoolId,
					FeatureKey = model.FeatureKey.Trim(),
					IsEnabled = model.IsEnabled,
					ConfigurationJson = model.ConfigurationJson,
					CreatedAt = DateTime.UtcNow,
					CreatedBy = userId,
					IsActive = true
				};

				await _featureCommand.Create(entity);

				_logger.Information(
					"School feature created - SchoolId: {SchoolId}, Feature: {Feature}, Enabled: {Enabled}, By: {UserId}",
					targetSchoolId, entity.FeatureKey, model.IsEnabled, userId);
			}
			else
			{
				await _featureCommand.UpdateTableColumnById(
					new Dictionary<string, object>
					{
						{ "IsEnabled", model.IsEnabled },
						{ "ConfigurationJson", model.ConfigurationJson },
						{ "UpdatedAt", DateTime.UtcNow }
					},
					new KeyValuePair<string, object>("Id", existing.Id));

				_logger.Information(
					"School feature updated - SchoolId: {SchoolId}, Feature: {Feature}, Enabled: {Enabled}, By: {UserId}",
					model.SchoolId, existing.FeatureKey, model.IsEnabled, userId);
			}

			return new BaseResponse
			{
				ResponseCode = ResponseCode.successful,
				ResponseMessage = model.IsEnabled
					? "Feature enabled for school"
					: "Feature disabled for school",
				Status = "successful"
			};
		}
		catch (Exception ex)
		{
			_logger.Error(ex, "Error setting school feature - SchoolId: {SchoolId}", model?.SchoolId);
			return ServerError();
		}
	}

	public async Task<BaseResponse> GetFeaturesAsync(Guid? schoolId, AuthenticatedUserClaims claims)
	{
		try
		{
			var isPlatformAdmin = claims.Role is "PlatformSuperAdmin" or "PlatformAdmin";

			Guid.TryParse(claims.SchoolId, out var callerSchoolId);
			if (!isPlatformAdmin && callerSchoolId == Guid.Empty)
				return Unauthorized();

			var targetSchool = schoolId ?? (callerSchoolId == Guid.Empty ? null : callerSchoolId);
			if (targetSchool is null)
				return BadRequest("SchoolId is required");

			if (!isPlatformAdmin && targetSchool != callerSchoolId)
				return Forbidden("You can only view features for your own school");

			var rows = await _featureQuery.QueryAsync<SchoolFeature>(
				"SELECT * FROM SchoolFeature WHERE SchoolId = @SchoolId AND IsActive = 1 ORDER BY FeatureKey",
				new Dictionary<string, object> { { "SchoolId", targetSchool } });

			return new BaseResponse
			{
				ResponseCode = ResponseCode.successful,
				ResponseMessage = "School features retrieved",
				Status = "successful",
				Data = rows.ToList()
			};
		}
		catch (Exception ex)
		{
			_logger.Error(ex, "Error retrieving school features");
			return ServerError();
		}
	}

	public async Task<bool> IsFeatureEnabledAsync(Guid schoolId, string featureKey)
	{
		try
		{
			if (string.IsNullOrWhiteSpace(featureKey))
				return false;

			var row = (await _featureQuery.QueryAsync<SchoolFeature>(
				"SELECT TOP 1 * FROM SchoolFeature WHERE SchoolId = @SchoolId AND FeatureKey = @FeatureKey AND IsActive = 1",
				new Dictionary<string, object>
				{
					{ "SchoolId", schoolId },
					{ "FeatureKey", featureKey.Trim() }
				})).FirstOrDefault();

			return row?.IsEnabled == true;
		}
		catch (Exception ex)
		{
			_logger.Error(ex,
				"Error checking school feature - SchoolId: {SchoolId}, Feature: {Feature}",
				schoolId, featureKey);
			return false;
		}
	}

	public async Task<BaseResponse> CheckFeatureAsync(Guid schoolId, string featureKey)
	{
		var enabled = await IsFeatureEnabledAsync(schoolId, featureKey);

		return new BaseResponse
		{
			ResponseCode = ResponseCode.successful,
			ResponseMessage = enabled
				? "Feature is enabled for this school"
				: "Feature is not enabled for this school",
			Status = "successful",
			Data = new
			{
				SchoolId = schoolId,
				FeatureKey = featureKey,
				IsEnabled = enabled
			}
		};
	}

	private static BaseResponse BadRequest(string message) => new()
	{
		ResponseCode = ResponseCode.BadRequest,
		ResponseMessage = message,
		Status = "failed"
	};

	private static BaseResponse Unauthorized() => new()
	{
		ResponseCode = ResponseCode.Unauthorized,
		ResponseMessage = "Invalid authentication",
		Status = "failed"
	};

	private static BaseResponse Forbidden(string message) => new()
	{
		ResponseCode = ResponseCode.Forbidden,
		ResponseMessage = message,
		Status = "failed"
	};

	private static BaseResponse ServerError() => new()
	{
		ResponseCode = ResponseCode.ErrorOccured,
		ResponseMessage = "An unexpected error occurred",
		Status = "failed"
	};
}
