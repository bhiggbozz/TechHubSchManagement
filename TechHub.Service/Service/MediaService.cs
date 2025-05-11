//using System;
//using System.Collections.Concurrent;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace TechHub.Service.Service
//{
//	public class MediaService : IMediaService, IDisposable
//	{
//		private readonly IWorker _worker;
//		private readonly ConcurrentDictionary<string, MediaRoom> _rooms = new();
//		private readonly ILogger<MediaService> _logger;

//		public MediaService(ILogger<MediaService> logger)
//		{
//			_logger = logger;
//			_worker = Mediasoup.CreateWorker(new WorkerSettings
//			{
//				RtcMinPort = 40000,
//				RtcMaxPort = 49999,
//				LogLevel = WorkerLogLevel.Warn
//			});
//		}

//		public async Task<TeacherRoomConfig> CreateTeacherRoomAsync(string roomId)
//		{
//			// 1. Create router for the room
//			var router = await _worker.CreateRouter(new RouterOptions
//			{
//				MediaCodecs = new List<MediaCodec>
//			{
//				new AudioCodec
//				{
//					Kind = MediaKind.Audio,
//					MimeType = "audio/opus",
//					ClockRate = 48000,
//					Channels = 2
//				}
//			}
//			});

//			// 2. Create teacher transport
//			var teacherTransport = await CreateTransport(router, "teacher");

//			// 3. Store room state
//			var room = new MediaRoom(router, teacherTransport);
//			_rooms.TryAdd(roomId, room);

//			return new TeacherRoomConfig
//			{
//				IceParameters = teacherTransport.IceParameters,
//				IceCandidates = teacherTransport.IceCandidates,
//				DtlsParameters = teacherTransport.DtlsParameters,
//				RouterRtpCapabilities = router.RtpCapabilities
//			};
//		}

//		public async Task<StudentConnectionConfig> AddStudentToRoomAsync(string roomId)
//		{
//			if (!_rooms.TryGetValue(roomId, out var room))
//				throw new RoomNotFoundException(roomId);

//			// 1. Create student-specific transport
//			var studentTransport = await CreateTransport(room.Router, "student");
//			room.StudentTransports.Add(studentTransport.Id, studentTransport);

//			// 2. Get teacher's audio producer ID
//			var audioProducerId = room.TeacherAudioProducer?.Id;

//			return new StudentConnectionConfig
//			{
//				IceParameters = studentTransport.IceParameters,
//				IceCandidates = studentTransport.IceCandidates,
//				DtlsParameters = studentTransport.DtlsParameters,
//				AudioProducerId = audioProducerId
//			};
//		}

//		private async Task<WebRtcTransport> CreateTransport(IRouter router, string role)
//		{
//			var transport = await router.CreateWebRtcTransport(new WebRtcTransportOptions
//			{
//				ListenIps = new List<TransportListenIp>
//			{
//				new TransportListenIp
//				{
//					Ip = "0.0.0.0",
//					AnnouncedIp = GetPublicIpForRole(role)
//				}
//			},
//				EnableUdp = true,
//				EnableTcp = true,
//				InitialAvailableOutgoingBitrate = 1_000_000,
//				AppData = new { Role = role }
//			});

//			_logger.LogInformation($"Created {role} transport {transport.Id}");
//			return transport;
//		}

//		public async Task<string> StartTeacherAudioAsync(string roomId, RtpParameters rtpParameters)
//		{
//			if (!_rooms.TryGetValue(roomId, out var room))
//				throw new RoomNotFoundException(roomId);

//			var producer = await room.TeacherTransport.Produce(new ProducerOptions
//			{
//				Kind = MediaKind.Audio,
//				RtpParameters = rtpParameters,
//				AppData = new { ProducerType = "teacher_audio" }
//			});

//			room.TeacherAudioProducer = producer;
//			return producer.Id;
//		}

//		public async Task<string> StartTeacherDataChannelAsync(string roomId)
//		{
//			if (!_rooms.TryGetValue(roomId, out var room))
//				throw new RoomNotFoundException(roomId);

//			var producer = await room.TeacherTransport.ProduceData(new DataProducerOptions
//			{
//				Label = "whiteboard",
//				Protocol = "json",
//				AppData = new { ProducerType = "whiteboard" }
//			});

//			room.BoardDataProducer = producer;
//			return producer.Id;
//		}

//		public async Task SendWhiteboardUpdateAsync(string roomId, string jsonData)
//		{
//			if (!_rooms.TryGetValue(roomId, out var room) ||
//				room.BoardDataProducer == null)
//				return;

//			await room.BoardDataProducer.Send(Encoding.UTF8.GetBytes(jsonData));
//		}

//		private string GetPublicIpForRole(string role)
//		{
//			return role == "teacher"
//				? _config["MediaServer:PublicIp"]
//				: _config["MediaServer:EdgeIp"];
//		}

//		public void Dispose()
//		{
//			_worker?.Dispose();
//		}
//	}

//	internal interface IWorker
//	{
//	}

//	// Supporting classes
//	public class MediaRoom
//	{
//		public IRouter Router { get; }
//		public WebRtcTransport TeacherTransport { get; }
//		public Dictionary<string, WebRtcTransport> StudentTransports { get; } = new();
//		public Producer TeacherAudioProducer { get; set; }
//		public DataProducer BoardDataProducer { get; set; }

//		public MediaRoom(IRouter router, WebRtcTransport teacherTransport)
//		{
//			Router = router;
//			TeacherTransport = teacherTransport;
//		}
//	}

//	public class TeacherRoomConfig
//	{
//		public IceParameters IceParameters { get; set; }
//		public List<IceCandidate> IceCandidates { get; set; }
//		public DtlsParameters DtlsParameters { get; set; }
//		public RtpCapabilities RouterRtpCapabilities { get; set; }
//	}

//	public class StudentConnectionConfig
//	{
//		public IceParameters IceParameters { get; set; }
//		public List<IceCandidate> IceCandidates { get; set; }
//		public DtlsParameters DtlsParameters { get; set; }
//		public string AudioProducerId { get; set; }
//	}
//}
