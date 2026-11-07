using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TechHub.Core.Entities;
using TechHub.Service.Extension;
using TechHub.Service.Interface;

namespace TechhubMS.Hubs
{
	/// <summary>
	/// The live boards of a class. The teacher's board is seen by everyone and only the teacher can draw
	/// on it. Each picked student also has a private board: the student draws on it, the teacher sees it
	/// live and can mark it up, and the other students see it only while the teacher is sharing it.
	/// Every connection is checked against the class's list on the server, so being signed in is not
	/// enough to listen in. Audio does not go through here — that is LiveKit.
	/// </summary>
	[Authorize]
	public class LiveBoardHub : Hub
	{
		private const int MaxPointsPerStroke = 5000;

		private static readonly string[] TeacherRoles = { "SubjectTeacher", "HeadTeacher", "ClassTeacher" };

		private readonly ILiveClassService _service;
		private readonly LiveBoardState _state;
		private readonly Serilog.ILogger _logger;

		public LiveBoardHub(ILiveClassService service, LiveBoardState state, Serilog.ILogger logger)
		{
			_service = service;
			_state = state;
			_logger = logger;
		}

		public static string GroupName(Guid liveClassId) => $"live:{liveClassId}";

		// ── Joining ───────────────────────────────────────────────────────────

		/// <summary>
		/// Enter the class. Returns the teacher's board as it is right now so a late joiner sees everything
		/// drawn so far. A student also gets their own private board back; the teacher gets a summary of
		/// every student board.
		/// </summary>
		public async Task<LiveBoardSnapshot> JoinClass(Guid liveClassId)
		{
			var claims = Context.User!.GetAuthenticatedUserClaims();
			if (!Guid.TryParse(claims.UserId, out var userId) || !Guid.TryParse(claims.SchoolId, out var schoolId))
				throw new HubException("Invalid user context");

			LiveClassAccess? access;
			try
			{
				access = await _service.GetAccessAsync(liveClassId, userId, schoolId);
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "LiveBoardHub: could not check access to {LiveClassId}", liveClassId);
				throw new HubException("Could not join the class. Please try again.");
			}

			if (access is null
				|| (access.IsTeacher && !TeacherRoles.Contains(claims.Role))
				|| (!access.IsTeacher && claims.Role != "Student"))
				throw new HubException("Live class not found");

			var endsAt = access.EndsAt.HasValue ? DateTime.SpecifyKind(access.EndsAt.Value, DateTimeKind.Utc) : DateTime.MinValue;
			if (access.Status != LiveClassStatus.Live || endsAt <= DateTime.UtcNow)
				throw new HubException("This class is not live");

			_state.AddConnection(Context.ConnectionId, new LiveBoardState.Member(liveClassId, userId, schoolId, access.IsTeacher));
			await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(liveClassId));
			await Clients.OthersInGroup(GroupName(liveClassId))
				.SendAsync("Presence", new { userId, role = access.IsTeacher ? "Teacher" : "Student", joined = true });

			var room = _state.GetOrCreate(liveClassId);
			var remaining = (int)Math.Max(0, (endsAt - DateTime.UtcNow).TotalSeconds);

			lock (room.Lock)
			{
				var snapshot = LiveBoardState.SnapshotOf(room.Main, room.Seq, remaining);
				snapshot.SharedStudentId = room.SharedStudentId;

				if (access.IsTeacher)
				{
					snapshot.StudentBoards = room.Students.Select(s => new StudentBoardSummary
					{
						StudentId = s.Key,
						CurrentBoard = s.Value.CurrentBoard,
						StrokeCount = s.Value.Boards.Values.Sum(b => b.Count)
					}).ToList();
				}
				else
				{
					if (!room.Students.TryGetValue(userId, out var mine))
						room.Students[userId] = mine = new BoardSet();
					snapshot.MyBoard = LiveBoardState.SnapshotOf(mine, room.Seq);
				}
				return snapshot;
			}
		}

		// ── The teacher's board (teacher draws, everyone watches) ─────────────

		/// <summary>Teacher only. The stroke is stamped with a sequence number and sent to everyone else in the class.</summary>
		public async Task SendStroke(Guid liveClassId, LiveStrokeDto stroke)
		{
			var (room, member) = RequireTeacher(liveClassId);
			AddStroke(room, room.Main, stroke, member.UserId);
			await Clients.OthersInGroup(GroupName(liveClassId)).SendAsync("Stroke", stroke);
		}

		/// <summary>Teacher only. Removes a stroke (undo / erase) on the current board.</summary>
		public async Task RemoveStroke(Guid liveClassId, string strokeId)
		{
			var (room, _) = RequireTeacher(liveClassId);
			long seq;
			lock (room.Lock)
			{
				room.Main.Boards[room.Main.CurrentBoard].RemoveAll(s => s.Id == strokeId);
				seq = ++room.Seq;
			}
			await Clients.OthersInGroup(GroupName(liveClassId)).SendAsync("StrokeRemoved", new { strokeId, seq });
		}

		/// <summary>Teacher only. Wipes the current board.</summary>
		public async Task ClearBoard(Guid liveClassId)
		{
			var (room, _) = RequireTeacher(liveClassId);
			int board;
			long seq;
			lock (room.Lock)
			{
				board = room.Main.CurrentBoard;
				room.Main.Boards[board].Clear();
				seq = ++room.Seq;
			}
			await Clients.OthersInGroup(GroupName(liveClassId)).SendAsync("BoardCleared", new { board, seq });
		}

		/// <summary>Teacher only. Moves to another board (creating it if new).</summary>
		public async Task SwitchBoard(Guid liveClassId, int boardIndex)
		{
			var (room, _) = RequireTeacher(liveClassId);
			long seq = SwitchSet(room, room.Main, boardIndex);
			await Clients.OthersInGroup(GroupName(liveClassId)).SendAsync("BoardChanged", new { board = boardIndex, seq });
		}

		// ── A student's own private board (student draws, teacher watches) ────

		/// <summary>Student only. Draws on the student's own board. The teacher sees it live; other students do not unless it is shared.</summary>
		public async Task SendMyStroke(Guid liveClassId, LiveStrokeDto stroke)
		{
			var (room, member, set) = RequireStudentBoard(liveClassId);
			AddStroke(room, set, stroke, member.UserId);
			await SendToBoardAudience(liveClassId, room, member.UserId, "StudentStroke", new { studentId = member.UserId, stroke });
		}

		public async Task RemoveMyStroke(Guid liveClassId, string strokeId)
		{
			var (room, member, set) = RequireStudentBoard(liveClassId);
			var seq = RemoveFrom(room, set, strokeId);
			await SendToBoardAudience(liveClassId, room, member.UserId, "StudentStrokeRemoved", new { studentId = member.UserId, strokeId, seq });
		}

		public async Task ClearMyBoard(Guid liveClassId)
		{
			var (room, member, set) = RequireStudentBoard(liveClassId);
			var (board, seq) = ClearCurrent(room, set);
			await SendToBoardAudience(liveClassId, room, member.UserId, "StudentBoardCleared", new { studentId = member.UserId, board, seq });
		}

		public async Task SwitchMyBoard(Guid liveClassId, int boardIndex)
		{
			var (room, member, set) = RequireStudentBoard(liveClassId);
			var seq = SwitchSet(room, set, boardIndex);
			await SendToBoardAudience(liveClassId, room, member.UserId, "StudentBoardChanged", new { studentId = member.UserId, board = boardIndex, seq });
		}

		// ── The teacher looking at, marking up and sharing student boards ─────

		/// <summary>
		/// A student's board as it stands. The teacher can open any student's; a student can open only their
		/// own, or the one the teacher is currently sharing.
		/// </summary>
		public Task<LiveBoardSnapshot> GetStudentBoard(Guid liveClassId, Guid studentId)
		{
			var member = RequireMember(liveClassId);
			var room = RequireRoom(liveClassId);

			lock (room.Lock)
			{
				var allowed = member.IsTeacher || member.UserId == studentId || room.SharedStudentId == studentId;
				if (!allowed)
					throw new HubException("You can't view that board");
				if (!room.Students.TryGetValue(studentId, out var set))
					throw new HubException("That student has not joined yet");
				return Task.FromResult(LiveBoardState.SnapshotOf(set, room.Seq));
			}
		}

		/// <summary>Teacher only. Writes on a student's private board; the student (and the class, if shared) see it.</summary>
		public async Task SendStudentBoardStroke(Guid liveClassId, Guid studentId, LiveStrokeDto stroke)
		{
			var (room, member, set) = RequireTeacherAndStudentBoard(liveClassId, studentId);
			AddStroke(room, set, stroke, member.UserId);
			await SendToBoardAudience(liveClassId, room, studentId, "StudentStroke", new { studentId, stroke });
		}

		public async Task RemoveStudentBoardStroke(Guid liveClassId, Guid studentId, string strokeId)
		{
			var (room, _, set) = RequireTeacherAndStudentBoard(liveClassId, studentId);
			var seq = RemoveFrom(room, set, strokeId);
			await SendToBoardAudience(liveClassId, room, studentId, "StudentStrokeRemoved", new { studentId, strokeId, seq });
		}

		public async Task ClearStudentBoard(Guid liveClassId, Guid studentId)
		{
			var (room, _, set) = RequireTeacherAndStudentBoard(liveClassId, studentId);
			var (board, seq) = ClearCurrent(room, set);
			await SendToBoardAudience(liveClassId, room, studentId, "StudentBoardCleared", new { studentId, board, seq });
		}

		/// <summary>
		/// Teacher only. Shows one student's board to the whole class (view only), or stops sharing when
		/// <paramref name="studentId"/> is null. Only one board can be shared at a time.
		/// </summary>
		public async Task ShareStudentBoard(Guid liveClassId, Guid? studentId)
		{
			var (room, _) = RequireTeacher(liveClassId);

			LiveBoardSnapshot? snapshot = null;
			lock (room.Lock)
			{
				if (studentId.HasValue)
				{
					if (!room.Students.TryGetValue(studentId.Value, out var set))
						throw new HubException("That student has not joined yet");
					snapshot = LiveBoardState.SnapshotOf(set, room.Seq);
				}
				room.SharedStudentId = studentId;
			}

			await Clients.OthersInGroup(GroupName(liveClassId)).SendAsync("BoardShared", new { studentId, snapshot });
		}

		// ── Leaving ───────────────────────────────────────────────────────────

		public override async Task OnDisconnectedAsync(Exception? exception)
		{
			var member = _state.RemoveConnection(Context.ConnectionId);
			if (member != null)
			{
				// Tell the class first; the database write can take a moment.
				await Clients.OthersInGroup(GroupName(member.LiveClassId))
					.SendAsync("Presence", new { userId = member.UserId, role = member.IsTeacher ? "Teacher" : "Student", joined = false });

				if (!member.IsTeacher)
					await _service.RecordStudentLeftAsync(member.LiveClassId, member.UserId);
			}
			await base.OnDisconnectedAsync(exception);
		}

		// ── Helpers ───────────────────────────────────────────────────────────

		private LiveBoardState.Member RequireMember(Guid liveClassId)
		{
			if (!_state.TryGetMember(Context.ConnectionId, out var member) || member.LiveClassId != liveClassId)
				throw new HubException("Join the class first");
			return member;
		}

		private LiveBoardState.Room RequireRoom(Guid liveClassId)
		{
			if (!_state.TryGetRoom(liveClassId, out var room))
				throw new HubException("This class has ended");
			return room;
		}

		private (LiveBoardState.Room Room, LiveBoardState.Member Member) RequireTeacher(Guid liveClassId)
		{
			var member = RequireMember(liveClassId);
			if (!member.IsTeacher)
				throw new HubException("Only the teacher can change the board");
			return (RequireRoom(liveClassId), member);
		}

		private (LiveBoardState.Room Room, LiveBoardState.Member Member, BoardSet Set) RequireStudentBoard(Guid liveClassId)
		{
			var member = RequireMember(liveClassId);
			if (member.IsTeacher)
				throw new HubException("Only a student can draw on their own board");
			var room = RequireRoom(liveClassId);
			lock (room.Lock)
			{
				if (!room.Students.TryGetValue(member.UserId, out var set))
					room.Students[member.UserId] = set = new BoardSet();
				return (room, member, set);
			}
		}

		private (LiveBoardState.Room Room, LiveBoardState.Member Member, BoardSet Set) RequireTeacherAndStudentBoard(Guid liveClassId, Guid studentId)
		{
			var (room, member) = RequireTeacher(liveClassId);
			lock (room.Lock)
			{
				if (!room.Students.TryGetValue(studentId, out var set))
					throw new HubException("That student has not joined yet");
				return (room, member, set);
			}
		}

		private static void AddStroke(LiveBoardState.Room room, BoardSet set, LiveStrokeDto stroke, Guid author)
		{
			if (stroke is null || string.IsNullOrWhiteSpace(stroke.Id) || stroke.Id.Length > 64
				|| stroke.Pts is null || stroke.Pts.Count == 0 || stroke.Pts.Count > MaxPointsPerStroke
				|| stroke.Pts.Any(p => p is null || p.Count != 2)
				|| (stroke.C?.Length ?? 0) > 32 || stroke.W < 0 || stroke.W > 200)
				throw new HubException("Invalid stroke");

			lock (room.Lock)
			{
				var board = set.Boards[set.CurrentBoard];
				if (board.Count >= LiveBoardState.MaxStrokesPerBoard)
					throw new HubException("This board is full. Move to the next board.");

				stroke.CurrentBoard = set.CurrentBoard;
				stroke.Seq = ++room.Seq;
				stroke.Author = author;
				board.Add(stroke);
			}
		}

		private static long RemoveFrom(LiveBoardState.Room room, BoardSet set, string strokeId)
		{
			lock (room.Lock)
			{
				set.Boards[set.CurrentBoard].RemoveAll(s => s.Id == strokeId);
				return ++room.Seq;
			}
		}

		private static (int Board, long Seq) ClearCurrent(LiveBoardState.Room room, BoardSet set)
		{
			lock (room.Lock)
			{
				var board = set.CurrentBoard;
				set.Boards[board].Clear();
				return (board, ++room.Seq);
			}
		}

		private static long SwitchSet(LiveBoardState.Room room, BoardSet set, int boardIndex)
		{
			if (boardIndex < 1 || boardIndex > LiveBoardState.MaxBoards)
				throw new HubException("Invalid board");

			lock (room.Lock)
			{
				if (!set.Boards.ContainsKey(boardIndex))
					set.Boards[boardIndex] = new();
				set.CurrentBoard = boardIndex;
				return ++room.Seq;
			}
		}

		/// <summary>
		/// Sends a student-board event to the people allowed to see that board: the teacher, the student who
		/// owns it, and the whole class while it is shared — never back to the caller.
		/// </summary>
		private Task SendToBoardAudience(Guid liveClassId, LiveBoardState.Room room, Guid ownerStudentId, string eventName, object payload)
		{
			bool shared;
			lock (room.Lock) { shared = room.SharedStudentId == ownerStudentId; }

			var recipients = _state.ConnectionsOf(liveClassId)
				.Where(c => c.ConnectionId != Context.ConnectionId
					&& (c.Member.IsTeacher || c.Member.UserId == ownerStudentId || shared))
				.Select(c => c.ConnectionId)
				.ToList();

			return recipients.Count == 0
				? Task.CompletedTask
				: Clients.Clients(recipients).SendAsync(eventName, payload);
		}
	}
}
