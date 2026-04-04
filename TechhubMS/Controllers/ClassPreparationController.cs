using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TechHub.Core;
using TechHub.Core.Model;
using TechHub.Core.ResponseModel;
using TechHub.Core.ViewModel.classroom;
using TechHub.Service.Extension;
using TechHub.Service.Interface;

namespace TechhubMS.Controllers;

/// <summary>
/// Controller for class preparation operations
/// Handles entire workflow: Draft → Submit → Approve/Reject → Teach
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ClassPreparationController : ControllerBase
{
	private readonly IClassPreparationService _classPreparationService;

	public ClassPreparationController(IClassPreparationService classPreparationService)
	{
		_classPreparationService = classPreparationService;
	}

	#region Teacher Endpoints

	/// <summary>
	/// Save class preparation as draft (create new or update existing)
	/// </summary>
	/// <param name="model">Class preparation details</param>
	/// <returns>Saved class preparation with full details</returns>
	/// <remarks>
	/// Teachers can save multiple times before submitting for approval.
	/// 
	/// Sample request (Create new):
	///     POST /api/classpreparation/save
	///     {
	///       "id": null,
	///       "subjectId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
	///       "classroomId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
	///       "topic": "Molecules and Matter",
	///       "subTopic": "Organic vs Inorganic Matter",
	///       "aimAndObjectives": "To introduce students to the basic concepts...",
	///       "scheduledDate": "2025-03-20",
	///       "scheduledTime": "14:30",
	///       "durationMinutes": 60,
	///       "classType": 1,
	///       "mediaFileIds": [
	///         "media-guid-1",
	///         "media-guid-2"
	///       ]
	///     }
	/// 
	/// Sample request (Update existing):
	///     POST /api/classpreparation/save
	///     {
	///       "id": "existing-class-guid",
	///       "subjectId": "...",
	///       // ... rest of fields
	///     }
	/// 
	/// Business rules:
	/// - Can only edit classes in Draft or Rejected status
	/// - Teacher can only edit their own classes
	/// - Media files must be uploaded first (use /api/media/upload)
	/// </remarks>
	/// <response code="200">Class saved successfully</response>
	/// <response code="400">Invalid input data</response>
	/// <response code="401">User not authenticated</response>
	/// <response code="403">Not authorized (can't edit other teacher's classes)</response>
	/// <response code="404">Class not found (when updating)</response>
	/// <response code="500">Server error</response>
	[HttpPost("save")]
	[Authorize(Roles = "HeadTeacher,SubjectTeacher,Administrator,SuperAdministrator")]
	[ProducesResponseType(typeof(ClassPreparationResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ClassPreparationResponse), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ClassPreparationResponse), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ClassPreparationResponse), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ClassPreparationResponse), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ClassPreparationResponse), StatusCodes.Status500InternalServerError)]
	public async Task<IActionResult> SaveClassPreparation([FromBody] SaveClassPreparationViewModel model)
	{
		if (!ModelState.IsValid)
		{
			return BadRequest(new ClassPreparationResponse
			{
				ResponseCode = ResponseCode.BadRequest,
				ResponseMessage = "Invalid input data",
				Status = "failed"
			});
		}

		var userClaims = User.GetAuthenticatedUserClaims();

		if (userClaims == null || string.IsNullOrEmpty(userClaims.UserId))
		{
			return Unauthorized(new ClassPreparationResponse
			{
				ResponseCode = ResponseCode.Unauthorized,
				ResponseMessage = "User not authenticated",
				Status = "failed"
			});
		}

		var result = await _classPreparationService.SaveClassPreparation(model,userClaims);

		return result.ResponseCode switch
		{
			ResponseCode.successful => Ok(result),
			ResponseCode.NotFound => NotFound(result),
			ResponseCode.Forbidden => StatusCode(StatusCodes.Status403Forbidden, result),
			ResponseCode.BadRequest => BadRequest(result),
			_ => StatusCode(StatusCodes.Status500InternalServerError, result)
		};
	}

	/// <summary>
	/// Submit class preparation for admin approval
	/// </summary>
	/// <param name="model">Submit request containing class ID</param>
	/// <returns>Success or failure response</returns>
	/// <remarks>
	/// Changes status from Draft/Rejected → Pending
	/// 
	/// Sample request:
	///     POST /api/classpreparation/submit
	///     {
	///       "classPreparationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
	///     }
	/// 
	/// What happens:
	/// 1. Status changes to "Pending"
	/// 2. Submission date and user recorded
	/// 3. Teacher can no longer edit (until approved/rejected)
	/// 4. Admin can see in pending approvals list
	/// 
	/// Business rules:
	/// - Only Draft or Rejected classes can be submitted
	/// - Teacher can only submit their own classes
	/// - Once submitted, teacher must wait for admin review
	/// </remarks>
	/// <response code="200">Submitted successfully</response>
	/// <response code="400">Invalid status (can only submit Draft or Rejected)</response>
	/// <response code="401">User not authenticated</response>
	/// <response code="403">Not authorized (can't submit other teacher's classes)</response>
	/// <response code="404">Class not found</response>
	/// <response code="500">Server error</response>
	[HttpPost("submit")]
	[Authorize(Roles = "HeadTeacher,SubjectTeacher,Administrator,SuperAdministrator")]
	[ProducesResponseType(typeof(BaseResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(BaseResponse), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(BaseResponse), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(BaseResponse), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(BaseResponse), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(BaseResponse), StatusCodes.Status500InternalServerError)]
	public async Task<IActionResult> SubmitForApproval([FromBody] SubmitForApprovalViewModel model)
	{
		if (!ModelState.IsValid)
		{
			return BadRequest(new BaseResponse
			{
				ResponseCode = ResponseCode.BadRequest,
				ResponseMessage = "Invalid input data",
				Status = "failed"
			});
		}

		var userClaims = User.GetAuthenticatedUserClaims();

		if (userClaims == null)
		{
			return Unauthorized(new BaseResponse
			{
				ResponseCode = ResponseCode.Unauthorized,
				ResponseMessage = "User not authenticated",
				Status = "failed"
			});
		}

		var result = await _classPreparationService.SubmitForApproval(model,userClaims);

		return result.ResponseCode switch
		{
			ResponseCode.successful => Ok(result),
			ResponseCode.NotFound => NotFound(result),
			ResponseCode.Forbidden => StatusCode(StatusCodes.Status403Forbidden, result),
			ResponseCode.BadRequest => BadRequest(result),
			_ => StatusCode(StatusCodes.Status500InternalServerError, result)
		};
	}

	/// <summary>
	/// Get teacher's own class preparations
	/// </summary>
	/// <param name="query">Filter and pagination parameters</param>
	/// <returns>Paginated list of class preparations</returns>
	/// <remarks>
	/// Retrieves all classes created by the authenticated teacher.
	/// Supports filtering, searching, sorting, and pagination.
	/// 
	/// Sample request:
	///     GET /api/classpreparation/my-classes?status=0&amp;pageNumber=1&amp;pageSize=20
	/// 
	/// Query parameters:
	/// - status: Filter by status (0=Draft, 1=Pending, 2=Approved, 3=Rejected)
	/// - classroomId: Filter by classroom
	/// - subjectId: Filter by subject
	/// - fromDate: Filter from date (yyyy-MM-dd)
	/// - toDate: Filter to date (yyyy-MM-dd)
	/// - searchTerm: Search in topic/subtopic
	/// - pageNumber: Page number (default 1)
	/// - pageSize: Page size (default 20)
	/// - sortBy: Sort field (default CreationDate)
	/// - sortDirection: asc/desc (default desc)
	/// 
	/// Sample response:
	/// {
	///   "responseCode": 200,
	///   "classPreparations": [
	///     {
	///       "id": "class-guid",
	///       "topic": "Molecules and Matter",
	///       "status": 0,
	///       "statusName": "Draft",
	///       "statusColor": "gray",
	///       "mediaFilesCount": 2,
	///       "canEdit": true,
	///       "canSubmit": true
	///     }
	///   ],
	///   "totalCount": 45,
	///   "pageNumber": 1,
	///   "pageSize": 20,
	///   "totalPages": 3,
	///   "hasNextPage": true
	/// }
	/// </remarks>
	/// <response code="200">Class preparations retrieved successfully</response>
	/// <response code="401">User not authenticated</response>
	/// <response code="500">Server error</response>
	[HttpGet("my-classes")]
	[Authorize(Roles = "HeadTeacher,SubjectTeacher,Administrator,SuperAdministrator")]
	[ProducesResponseType(typeof(ClassPreparationsListResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ClassPreparationsListResponse), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ClassPreparationsListResponse), StatusCodes.Status500InternalServerError)]
	public async Task<IActionResult> GetMyClassPreparations([FromQuery] GetClassPreparationsQuery query)
	{
		var userClaims = User.GetAuthenticatedUserClaims();

		if (userClaims == null)
		{
			return Unauthorized(new ClassPreparationsListResponse
			{
				ResponseCode = ResponseCode.Unauthorized,
				ResponseMessage = "User not authenticated",
				Status = "failed"
			});
		}

		var result = await _classPreparationService.GetMyClassPreparations(query,userClaims);

		return result.ResponseCode switch
		{
			ResponseCode.successful => Ok(result),
			_ => StatusCode(StatusCodes.Status500InternalServerError, result)
		};
	}

	/// <summary>
	/// Get class preparation details by ID
	/// </summary>
	/// <param name="id">Class preparation ID</param>
	/// <returns>Class preparation with full details including media files</returns>
	/// <remarks>
	/// Sample request:
	///     GET /api/classpreparation/3fa85f64-5717-4562-b3fc-2c963f66afa6
	/// 
	/// Sample response:
	/// {
	///   "responseCode": 200,
	///   "classPreparation": {
	///     "id": "class-guid",
	///     "topic": "Molecules and Matter",
	///     "subTopic": "Organic vs Inorganic",
	///     "aimAndObjectives": "To introduce...",
	///     "status": 2,
	///     "statusName": "Approved",
	///     "statusColor": "green",
	///     "teacherName": "John Doe",
	///     "subjectName": "Physics",
	///     "classroomName": "SS2 Science A",
	///     "mediaFiles": [
	///       {
	///         "id": "media-guid-1",
	///         "displayName": "Lecture Video",
	///         "cdnUrl": "https://...",
	///         "thumbnailUrl": "https://..."
	///       }
	///     ],
	///     "canEdit": false,
	///     "canApprove": false
	///   }
	/// }
	/// </remarks>
	/// <response code="200">Class preparation retrieved successfully</response>
	/// <response code="401">User not authenticated</response>
	/// <response code="404">Class preparation not found</response>
	/// <response code="500">Server error</response>
	[HttpGet("{id}")]
	[ProducesResponseType(typeof(ClassPreparationResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ClassPreparationResponse), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ClassPreparationResponse), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ClassPreparationResponse), StatusCodes.Status500InternalServerError)]
	public async Task<IActionResult> GetClassPreparationById(Guid id)
	{
		var userClaims = User.GetAuthenticatedUserClaims();

		if (userClaims == null)
		{
			return Unauthorized(new ClassPreparationResponse
			{
				ResponseCode = ResponseCode.Unauthorized,
				ResponseMessage = "User not authenticated",
				Status = "failed"
			});
		}

		var result = await _classPreparationService.GetClassPreparationById(id,userClaims);

		return result.ResponseCode switch
		{
			ResponseCode.successful => Ok(result),
			ResponseCode.NotFound => NotFound(result),
			_ => StatusCode(StatusCodes.Status500InternalServerError, result)
		};
	}

	/// <summary>
	/// Delete draft class preparation
	/// </summary>
	/// <param name="id">Class preparation ID</param>
	/// <returns>Success or failure response</returns>
	/// <remarks>
	/// Only draft classes can be deleted.
	/// Also deletes associated media files.
	/// 
	/// Sample request:
	///     DELETE /api/classpreparation/3fa85f64-5717-4562-b3fc-2c963f66afa6
	/// 
	/// Business rules:
	/// - Only Draft classes can be deleted
	/// - Teacher can only delete their own classes
	/// - Deletes media files from Cloudinary
	/// - Soft delete in database
	/// </remarks>
	/// <response code="200">Class deleted successfully</response>
	/// <response code="400">Invalid status (only Draft can be deleted)</response>
	/// <response code="401">User not authenticated</response>
	/// <response code="403">Not authorized</response>
	/// <response code="404">Class not found</response>
	/// <response code="500">Server error</response>
	[HttpDelete("{id}")]
	[Authorize(Roles = "HeadTeacher,SubjectTeacher,Administrator,SuperAdministrator")]
	[ProducesResponseType(typeof(BaseResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(BaseResponse), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(BaseResponse), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(BaseResponse), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(BaseResponse), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(BaseResponse), StatusCodes.Status500InternalServerError)]
	public async Task<IActionResult> DeleteDraft(Guid id)
	{
		var userClaims = User.GetAuthenticatedUserClaims();

		if (userClaims == null)
		{
			return Unauthorized(new BaseResponse
			{
				ResponseCode = ResponseCode.Unauthorized,
				ResponseMessage = "User not authenticated",
				Status = "failed"
			});
		}

		var result = await _classPreparationService.DeleteDraft(id, userClaims);

		return result.ResponseCode switch
		{
			ResponseCode.successful => Ok(result),
			ResponseCode.NotFound => NotFound(result),
			ResponseCode.Forbidden => StatusCode(StatusCodes.Status403Forbidden, result),
			ResponseCode.BadRequest => BadRequest(result),
			_ => StatusCode(StatusCodes.Status500InternalServerError, result)
		};
	}

	#endregion

	#region Admin Endpoints

	/// <summary>
	/// Get pending approvals (Admin only)
	/// </summary>
	/// <param name="query">Filter and pagination parameters</param>
	/// <returns>Paginated list of pending class preparations</returns>
	/// <remarks>
	/// Returns all classes with Status = Pending that require admin approval.
	/// Requires ApproveClasses permission for regular admins.
	/// SuperAdmins bypass permission check.
	/// 
	/// Sample request:
	///     GET /api/classpreparation/pending-approvals?pageNumber=1&amp;pageSize=20
	/// 
	/// Query parameters (same as my-classes):
	/// - teacherId: Filter by specific teacher
	/// - classroomId: Filter by classroom
	/// - subjectId: Filter by subject
	/// - fromDate: Filter from date
	/// - toDate: Filter to date
	/// - searchTerm: Search in topic
	/// - pageNumber: Page number
	/// - pageSize: Page size
	/// 
	/// Sample response:
	/// {
	///   "responseCode": 200,
	///   "classPreparations": [
	///     {
	///       "id": "class-guid",
	///       "topic": "Photosynthesis",
	///       "teacherName": "Jane Smith",
	///       "submittedDate": "2025-03-15 08:30:00",
	///       "status": 1,
	///       "statusName": "Pending",
	///       "canApprove": true,
	///       "canReject": true
	///     }
	///   ],
	///   "totalCount": 12
	/// }
	/// </remarks>
	/// <response code="200">Pending approvals retrieved successfully</response>
	/// <response code="401">User not authenticated</response>
	/// <response code="403">User lacks ApproveClasses permission</response>
	/// <response code="500">Server error</response>
	[HttpGet("pending-approvals")]
	[Authorize(Roles = "Administrator,SuperAdministrator")]
	[ProducesResponseType(typeof(ClassPreparationsListResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ClassPreparationsListResponse), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ClassPreparationsListResponse), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ClassPreparationsListResponse), StatusCodes.Status500InternalServerError)]
	public async Task<IActionResult> GetPendingApprovals([FromQuery] GetClassPreparationsQuery query)
	{
		var userClaims = User.GetAuthenticatedUserClaims();

		if (userClaims == null)
		{
			return Unauthorized(new ClassPreparationsListResponse
			{
				ResponseCode = ResponseCode.Unauthorized,
				ResponseMessage = "User not authenticated",
				Status = "failed"
			});
		}

		var result = await _classPreparationService.GetPendingApprovals(query,userClaims);

		return result.ResponseCode switch
		{
			ResponseCode.successful => Ok(result),
			ResponseCode.Forbidden => StatusCode(StatusCodes.Status403Forbidden, result),
			_ => StatusCode(StatusCodes.Status500InternalServerError, result)
		};
	}

	// TechHub.Api/Controllers/ClassPreparationController.cs
	// Add these endpoints to existing ClassPreparationController

	/// <summary>
	/// Approve a pending class preparation
	/// 
	/// WORKFLOW:
	/// 1. Validates admin permissions
	/// 2. Checks all media uploaded
	/// 3. Moves media to permanent storage
	/// 4. Updates class status to Approved
	/// 5. Updates teacher trust score
	/// 
	/// AUTHORIZATION:
	/// - Administrators need ApproveClasses permission
	/// - SuperAdministrators bypass permission check
	/// - Cannot approve own classes
	/// </summary>
	[HttpPost("approve")]
	[Authorize(Roles = "Administrator,SuperAdministrator")]
	public async Task<IActionResult> ApproveClass([FromBody] ApproveClassViewModel model)
	{
		var userClaims = User.GetAuthenticatedUserClaims();
		var result = await _classPreparationService.ApproveClass(model, userClaims);

		return result.ResponseCode switch
		{
			ResponseCode.successful => Ok(result),
			ResponseCode.NotFound => NotFound(result),
			ResponseCode.Forbidden => StatusCode(StatusCodes.Status403Forbidden, result),
			ResponseCode.BadRequest => BadRequest(result),
			_ => StatusCode(StatusCodes.Status500InternalServerError, result)
		};
	}

	

	/// <summary>
	/// Reject a pending class preparation
	/// 
	/// WORKFLOW:
	/// 1. Validates admin permissions
	/// 2. Validates rejection reason (required, 10-500 chars)
	/// 3. Deletes all class media
	/// 4. Updates class status to Rejected
	/// 5. Updates teacher trust score (penalty)
	/// 
	/// AUTHORIZATION:
	/// - Same as approve
	/// - Rejection reason mandatory
	/// </summary>
	[HttpPost("reject")]
	[Authorize(Roles = "Administrator,SuperAdministrator")]
	public async Task<IActionResult> RejectClass([FromBody] RejectClassViewModel model)
	{
		var userClaims = User.GetAuthenticatedUserClaims();
		var result = await _classPreparationService.RejectClass(model, userClaims);

		return result.ResponseCode switch
		{
			ResponseCode.successful => Ok(result),
			ResponseCode.NotFound => NotFound(result),
			ResponseCode.Forbidden => StatusCode(StatusCodes.Status403Forbidden, result),
			ResponseCode.BadRequest => BadRequest(result),
			_ => StatusCode(StatusCodes.Status500InternalServerError, result)
		};
	}
	#endregion
}

