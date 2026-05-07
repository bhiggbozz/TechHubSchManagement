using Serilog;
using TechHub.Core;
using TechHub.Core.Entities.Board;
using TechHub.Core.Messages;
using TechHub.Core.Model;
using TechHub.Core.ViewModels.Board;
using TechHub.Core.ViewModels.Board.Manifest;
using TechHub.Service.Interface;

namespace TechHub.Service.Service;

public class BoardSessionService : IBoardSessionService
{
    private readonly IBoardPublisherService _publisherService;
    private readonly IBoardSessionRepository _repository;
    private readonly ILogger _logger;

    public BoardSessionService(
        IBoardPublisherService publisherService,
        IBoardSessionRepository repository,
        ILogger logger)
    {
        _publisherService = publisherService;
        _repository = repository;
        _logger = logger;
    }

    public async Task PublishBatchAsync(
        string routeSessionId,
        BoardBatchViewModel model,
        AuthenticatedUserClaims claims)
    {
        if (routeSessionId != model.SessionId)
        {
            _logger.Warning(
                "Session ID mismatch - Route: {RouteSessionId}, Body: {BodySessionId}",
                routeSessionId,
                model.SessionId);

            throw new ArgumentException(
                $"Session ID in route ({routeSessionId}) does not match body ({model.SessionId})");
        }

        var message = BoardBatchMessage.FromViewModel(
            model,
            claims.SchoolId ?? string.Empty,
            claims.UserId ?? string.Empty);

        await _publisherService.PublishBatchAsync(message);

        _logger.Information(
            "Published batch {BatchIndex} for session {SessionId}, Teacher: {TeacherId}",
            model.BatchIndex,
            model.SessionId,
            claims.UserId);
    }

    public async Task<BaseResponse> SaveManifestAsync(
        string routeSessionId,
        SessionManifestViewModel model,
        AuthenticatedUserClaims claims)
    {
        if (routeSessionId != model.Session.Id)
        {
            _logger.Warning(
                "Session ID mismatch - Route: {RouteSessionId}, Body: {BodySessionId}",
                routeSessionId,
                model.Session.Id);

            return new BaseResponse
            {
                ResponseCode = ResponseCode.BadRequest,
                ResponseMessage = $"Session ID in route ({routeSessionId}) does not match body ({model.Session.Id})",
                Status = "failed"
            };
        }

        if (claims.SchoolId != model.Session.SchoolId)
        {
            _logger.Warning(
                "School ID mismatch - JWT: {JwtSchoolId}, Body: {BodySchoolId}",
                claims.SchoolId,
                model.Session.SchoolId);

            return new BaseResponse
            {
                ResponseCode = ResponseCode.Unauthorized,
                ResponseMessage = "School ID from JWT does not match the manifest school ID",
                Status = "failed"
            };
        }

        try
        {
            await _repository.SaveManifestAsync(routeSessionId, model);

            _logger.Information(
                "Saved manifest for session {SessionId}, School: {SchoolId}, Teacher: {TeacherId}",
                model.Session.Id,
                model.Session.SchoolId,
                claims.UserId);

            return new BaseResponse
            {
                ResponseCode = ResponseCode.Created,
                ResponseMessage = "Session manifest saved successfully",
                Status = "success"
            };
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to save manifest for session {SessionId}", routeSessionId);

            return new BaseResponse
            {
                ResponseCode = ResponseCode.ServerError,
                ResponseMessage = "Failed to save session manifest",
                Status = "failed"
            };
        }
    }

    public async Task<BoardSession?> GetSessionAsync(string sessionId, string schoolId)
    {
        return await _repository.GetSessionAsync(sessionId, schoolId);
    }
}
