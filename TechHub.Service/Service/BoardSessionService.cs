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
	private readonly IQueryRepository<LessonContent> _lessonQuery;
	private readonly IQueryRepository<StudentClassroom> _studentClassroomQuery;


	private readonly IDbTransactionScopeFactory _scopeFactory;


	private readonly ILogger _logger;

    public BoardSessionService( IBoardPublisherService publisherService, IBoardSessionRepository repository, 
        ICommandRespository<LessonContent> lessonCommand, IQueryRepository<LessonContent> lessonQuery, IQueryRepository<StudentClassroom> studentClassroomQuery,
		IDbTransactionScopeFactory scopeFactory, ILogger logger)
    {
        _publisherService = publisherService;
        _repository = repository;
        _lessonCommand = lessonCommand;
		_lessonQuery = lessonQuery;
		_studentClassroomQuery = studentClassroomQuery;
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


	public async Task<BaseResponse> GetManifest(
		string sessionId, AuthenticatedUserClaims claims)
	{
		try
		{
			if (!Guid.TryParse(claims.UserId, out var studentId))
				return Unauthorized();
			if (!Guid.TryParse(claims.SchoolId, out var schoolId))
				return Unauthorized();

			// Fetch session from MongoDB — strokes excluded
			var session = await _repository.GetManifestAsync(sessionId, claims.SchoolId);
			if (session is null)
				return NotFound("Session not found or not yet completed");

			// Verify lesson is published
			var lessonQuery = $@"
                SELECT lc.Id, lc.ClassroomId
                FROM   LessonContent lc
                WHERE  lc.Id       = '{session.LessonId}'
                AND    lc.SchoolId = '{schoolId}'
                AND    lc.Status   = '{LessonStatus.Published}'";

			var lesson = await _lessonQuery.Get(lessonQuery);
			if (lesson is null)
				return NotFound("Lesson not found or not published");

			// Verify student is enrolled in the lesson's classroom
			var membershipQuery = $@"
                SELECT TOP 1 Id FROM StudentClassroom
                WHERE  StudentId   = '{studentId}'
                AND    ClassroomId = '{lesson.ClassroomId}'
                AND    SchoolId    = '{schoolId}'
                AND    IsActive    = 1";

			var membership = await _studentClassroomQuery.Get(membershipQuery);
			if (membership is null)
				return Forbidden("You are not enrolled in this classroom");

			_logger.Information(
				"Manifest retrieved - SessionId: {SessionId}, StudentId: {StudentId}",
				sessionId, studentId);

			return new BaseResponse
			{
				ResponseCode = ResponseCode.successful,
				ResponseMessage = "Manifest retrieved successfully",
				Status = "successful",
				Data = new
				{
					session.Version,
					session.Teacher,
					session.Lesson,
					session.Stats,
					session.Chunks,
					session.MediaAssets,
					session.Boards,
					session.Chapters,
					StrokeBatches = session.Batches
						.Select(b => new
						{
							b.BatchIndex,
							b.IndexKey,
							b.StartMs,
							b.EndMs,
							b.StrokeCount
						})
						.OrderBy(b => b.BatchIndex)
						.ToList()
				}
			};
		}
		catch (Exception ex)
		{
			_logger.Error(ex,
				"Error fetching manifest - SessionId: {SessionId}, StudentId: {StudentId}",
				sessionId, claims?.UserId);
			return ServerError();
		}
	}

	// ── Get single batch by indexKey ─────────────────────────────────────────
	public async Task<BaseResponse> GetBatch(string sessionId, string indexKey, AuthenticatedUserClaims claims)
	{
		try
		{
			if (!Guid.TryParse(claims.UserId, out var studentId))
				return Unauthorized();
			if (!Guid.TryParse(claims.SchoolId, out var schoolId))
				return Unauthorized();

			if (string.IsNullOrWhiteSpace(indexKey))
				return BadRequest("IndexKey is required");

			var batch = await _repository.GetBatchByIndexKeyAsync(
				sessionId, schoolId.ToString(), indexKey);

			if (batch is null)
				return NotFound($"Batch not found - IndexKey: {indexKey}");

			_logger.Information(
				"Batch retrieved - SessionId: {SessionId}, IndexKey: {IndexKey}, StudentId: {StudentId}",
				sessionId, indexKey, studentId);

			return new BaseResponse
			{
				ResponseCode = ResponseCode.successful,
				ResponseMessage = "Batch retrieved successfully",
				Status = "successful",
				Data = batch
			};
		}
		catch (Exception ex)
		{
			_logger.Error(ex,
				"Error fetching batch - SessionId: {SessionId}, IndexKey: {IndexKey}",
				sessionId, indexKey);
			return ServerError();
		}
	}

	// ── Get full session — admin/teacher use ─────────────────────────────────
	private BaseResponse BadRequest(string message) => new BaseResponse
	{
		ResponseCode = ResponseCode.BadRequest,
		ResponseMessage = message,
		Status = "failed"
	};
	private BaseResponse Unauthorized() => new BaseResponse
	{
		ResponseCode = ResponseCode.Unauthorized,
		ResponseMessage = "Invalid authentication",
		Status = "failed"
	};
	private BaseResponse Forbidden(string message) => new BaseResponse
	{
		ResponseCode = ResponseCode.Forbidden,
		ResponseMessage = message,
		Status = "failed"
	};
	private BaseResponse NotFound(string message) => new BaseResponse
	{
		ResponseCode = ResponseCode.NotFound,
		ResponseMessage = message,
		Status = "failed"
	};
	private BaseResponse ServerError() => new BaseResponse
	{
		ResponseCode = ResponseCode.ErrorOccured,
		ResponseMessage = "An unexpected error occurred",
		Status = "failed"
	};


	public async Task<BoardSession?> GetSessionAsync(string sessionId, string schoolId)
    {
        return await _repository.GetSessionAsync(sessionId, schoolId);
    }
}
