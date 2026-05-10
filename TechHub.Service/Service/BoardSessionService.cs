using Dapper;
using Serilog;
using TechHub.Core;
using TechHub.Core.Entities;
using TechHub.Core.Entities.Board;
using TechHub.Core.Messages;
using TechHub.Core.Model;
using TechHub.Core.ViewModels.Board;
using TechHub.Core.ViewModels.Board.Manifest;
using TechHub.Service.Interface;
using TechHub.Service.Service.DatabaseService;

namespace TechHub.Service.Service;

public class BoardSessionService : IBoardSessionService
{
    private readonly IBoardPublisherService _publisherService;
    private readonly IBoardSessionRepository _repository;
	private readonly ICommandRespository<LessonContent> _lessonCommand;
	private readonly IDbTransactionScopeFactory _scopeFactory;


	private readonly ILogger _logger;

    public BoardSessionService( IBoardPublisherService publisherService, IBoardSessionRepository repository, 
        ICommandRespository<LessonContent> lessonCommand, IDbTransactionScopeFactory scopeFactory, ILogger logger)
    {
        _publisherService = publisherService;
        _repository = repository;
        _lessonCommand = lessonCommand;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task PublishBatchAsync(string routeSessionId,BoardBatchViewModel model,AuthenticatedUserClaims claims)
    {
        if (routeSessionId != model.SessionId)
        {
            _logger.Warning( "Session ID mismatch - Route: {RouteSessionId}, Body: {BodySessionId}", routeSessionId, model.SessionId);
            throw new ArgumentException($"Session ID in route ({routeSessionId}) does not match body ({model.SessionId})");
        }

        var message = BoardBatchMessage.FromViewModel( model, claims.SchoolId ?? string.Empty, claims.UserId ?? string.Empty);

        await _publisherService.PublishBatchAsync(message);

        _logger.Information( "Published batch {BatchIndex} for session {SessionId}, Teacher: {TeacherId}", model.BatchIndex, model.SessionId, claims.UserId);


    }

	public async Task<BaseResponse> SaveManifestAsync(string routeSessionId,SessionManifestViewModel model,AuthenticatedUserClaims claims)
	{
		if (routeSessionId != model.Session.Id)
		{
			_logger.Warning(
				"Session ID mismatch - Route: {RouteSessionId}, Body: {BodySessionId}",
				routeSessionId, model.Session.Id);
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
				claims.SchoolId, model.Session.SchoolId);
			return new BaseResponse
			{
				ResponseCode = ResponseCode.Unauthorized,
				ResponseMessage = "School ID from JWT does not match the manifest school ID",
				Status = "failed"
			};
		}

		if (string.IsNullOrWhiteSpace(model.Session.LessonId))
		{
			_logger.Warning("LessonId missing from manifest - SessionId: {SessionId}", routeSessionId);
			return new BaseResponse
			{
				ResponseCode = ResponseCode.BadRequest,
				ResponseMessage = "LessonId is required in the manifest",
				Status = "failed"
			};
		}

		if (!Guid.TryParse(model.Session.LessonId, out var lessonId))
		{
			_logger.Warning("Invalid LessonId format - SessionId: {SessionId}, LessonId: {LessonId}", routeSessionId, model.Session.LessonId);
			return new BaseResponse
			{
				ResponseCode = ResponseCode.BadRequest,
				ResponseMessage = "LessonId format is invalid",
				Status = "failed"
			};
		}

		using var scope = _scopeFactory.Create("DbConnectionString");
		try
		{
			var updateQuery = $@"
				UPDATE LessonContent
				SET    Status     = '{LessonStatus.Published}',
					   ModifiedAt = '{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}'
				WHERE  Id       = '{lessonId}'
				AND    SchoolId = '{model.Session.SchoolId}'
				AND    Status   = '{LessonStatus.Approved}'";

			await scope.Connection.ExecuteAsync(updateQuery, transaction: scope.Transaction);

			_logger.Information(
				"Lesson status updated in transaction (pending commit) - LessonId: {LessonId}, SessionId: {SessionId}",
				lessonId, routeSessionId);

			await _repository.SaveManifestAsync(routeSessionId, model);

			_logger.Information(
				"Manifest saved to MongoDB - SessionId: {SessionId}, SchoolId: {SchoolId}, TeacherId: {TeacherId}",
				model.Session.Id, model.Session.SchoolId, claims.UserId);

			await scope.CommitAsync();

			_logger.Information(
				"Transaction committed - LessonId: {LessonId}, SessionId: {SessionId}",
				lessonId, routeSessionId);

			return new BaseResponse
			{
				ResponseCode = ResponseCode.successful,
				ResponseMessage = "Session manifest saved and lesson published successfully",
				Status = "success"
			};
		}
		catch (Exception ex)
		{
			_logger.Error(ex,
				"Failed - rolling back SQL transaction - SessionId: {SessionId}, LessonId: {LessonId}",
				routeSessionId, lessonId);

			try { await scope.RollbackAsync(); }
			catch (Exception rbEx)
			{
				_logger.Error(rbEx,
					"Rollback failed - SessionId: {SessionId}",
					routeSessionId);
			}

			return new BaseResponse
			{
				ResponseCode = ResponseCode.ErrorOccured,
				ResponseMessage = "Failed to save session manifest. No changes were applied.",
				Status = "failed"
			};
		}
	}
	public async Task<BoardSession?> GetSessionAsync(string sessionId, string schoolId)
    {
        return await _repository.GetSessionAsync(sessionId, schoolId);
    }
}
