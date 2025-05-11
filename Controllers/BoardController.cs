using Microsoft.AspNetCore.Mvc;

namespace TechhubMS.Controllers
{
	//[ApiController]
	//[Route("api/[controller")]
	//public class BoardController : Controller
	//{
	//	public IActionResult Index()
	//	{
	//		return View();
	//	}
	//	//HttpPost("classrooms")]
	//	//public async Task<IActionResult> CreateClassroom()
	//	//{
	//		//var roomId = Guid.NewGuid().ToString();

	//		// 1. Create media resources
	//		//var (teacherTransport, studentTransport) = await _mediaService
	//		//	.CreateClassroomTransports(roomId);

	//					//await _classroomStore.CreateAsync(new Classroom
	//					//{
	//					//	Id = roomId,
	//					//	WebSocketEndpoint = wsEndpoint,
	//					//	MediaConfig = mediaConfig,

	//					//	Status = ClassroomStatus.Created
	//					//});


	//		// 2. Generate dedicated endpoints
	//		//return Ok(new
	//		//{
	//		//	teacherWs = $"wss://{Host}/ws-teacher/{roomId}?token={GenerateToken(roomId, "teacher")}",
	//		//	studentWsTemplate = $"wss://{Host}/ws-student/{roomId}?token=",

	//		//	// Teacher-specific config
	//		//	teacherRtcConfig = new
	//		//	{
	//		//		iceParameters = teacherTransport.IceParameters,
	//		//		dtlsParameters = teacherTransport.DtlsParameters
	//		//	}
	//		//});
	//	//}
	//}
}
