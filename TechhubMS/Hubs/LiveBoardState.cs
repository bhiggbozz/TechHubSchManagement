using System.Collections.Concurrent;

namespace TechhubMS.Hubs
{
	/// <summary>One pen stroke, in the same shape the board recorder already uses (pts = [[x,y],...]).</summary>
	public class LiveStrokeDto
	{
		public string Id { get; set; } = string.Empty;
		public List<List<double>> Pts { get; set; } = new();
		/// <summary>Colour.</summary>
		public string C { get; set; } = "#000000";
		/// <summary>Width.</summary>
		public double W { get; set; } = 2;
		/// <summary>Milliseconds since the class started; students use it to line pen and voice up.</summary>
		public long Ts { get; set; }
		public int CurrentBoard { get; set; } = 1;
		/// <summary>"stroke" (a normal pen mark) or "eraser" (drawn with destination-out so it wipes what is under it). Absent = stroke. Kept so a late joiner's board shows the erasing, not thick black lines.</summary>
		public string? StrokeType { get; set; }
		/// <summary>Set by the server, strictly increasing within a class. Clients use it to spot gaps after a reconnect.</summary>
		public long Seq { get; set; }
		/// <summary>Set by the server: the user who drew it (a student's id, or the teacher's on a student's board).</summary>
		public Guid? Author { get; set; }
	}

	public class StudentBoardSummary
	{
		public Guid StudentId { get; set; }
		public int CurrentBoard { get; set; }
		public int StrokeCount { get; set; }
	}

	public class LiveBoardSnapshot
	{
		public int CurrentBoard { get; set; }
		public List<int> Boards { get; set; } = new();
		/// <summary>All strokes on all boards, in drawing order; each has its board number in <c>currentBoard</c>.</summary>
		public List<LiveStrokeDto> Strokes { get; set; } = new();
		public long Seq { get; set; }
		public int SecondsRemaining { get; set; }

		/// <summary>Whose private board the teacher is currently showing to the whole class (null = none).</summary>
		public Guid? SharedStudentId { get; set; }
		/// <summary>Students only: their own private board as it stands.</summary>
		public LiveBoardSnapshot? MyBoard { get; set; }
		/// <summary>Teacher only: a line per student board, so the teacher can see who has been working.</summary>
		public List<StudentBoardSummary>? StudentBoards { get; set; }
	}

	/// <summary>A set of numbered boards with one current board — the teacher's, or one student's private set.</summary>
	public sealed class BoardSet
	{
		public int CurrentBoard = 1;
		public readonly Dictionary<int, List<LiveStrokeDto>> Boards = new() { [1] = new() };
	}

	/// <summary>
	/// What each live class's boards look like right now, kept in memory so a student who joins late
	/// or reconnects can be brought up to date. It is only a live view — the teacher's normal 1-minute
	/// batches are what get saved for replay, and students' private boards are not saved.
	/// </summary>
	public sealed class LiveBoardState
	{
		public const int MaxStrokesPerBoard = 20000;
		public const int MaxBoards = 200;

		public sealed class Room
		{
			public readonly object Lock = new();
			public long Seq;
			/// <summary>The teacher's board, which everyone in the class sees.</summary>
			public readonly BoardSet Main = new();
			/// <summary>Each student's private boards, created when that student first joins.</summary>
			public readonly Dictionary<Guid, BoardSet> Students = new();
			/// <summary>The student whose board the teacher is showing to the whole class, if any.</summary>
			public Guid? SharedStudentId;
		}

		public sealed record Member(Guid LiveClassId, Guid UserId, Guid SchoolId, bool IsTeacher);

		private readonly ConcurrentDictionary<Guid, Room> _rooms = new();
		private readonly ConcurrentDictionary<string, Member> _connections = new();

		public Room GetOrCreate(Guid liveClassId) => _rooms.GetOrAdd(liveClassId, _ => new Room());

		public bool TryGetRoom(Guid liveClassId, out Room room) => _rooms.TryGetValue(liveClassId, out room!);

		public void AddConnection(string connectionId, Member member) => _connections[connectionId] = member;

		public bool TryGetMember(string connectionId, out Member member) => _connections.TryGetValue(connectionId, out member!);

		public Member? RemoveConnection(string connectionId)
			=> _connections.TryRemove(connectionId, out var m) ? m : null;

		/// <summary>Every live connection in one class, with who it belongs to.</summary>
		public List<(string ConnectionId, Member Member)> ConnectionsOf(Guid liveClassId)
			=> _connections.Where(c => c.Value.LiveClassId == liveClassId).Select(c => (c.Key, c.Value)).ToList();

		/// <summary>The class is over: forget its boards and every connection that belonged to it.</summary>
		public void Remove(Guid liveClassId)
		{
			_rooms.TryRemove(liveClassId, out _);
			foreach (var kv in _connections.Where(c => c.Value.LiveClassId == liveClassId).ToList())
				_connections.TryRemove(kv.Key, out _);
		}

		/// <summary>A copy of a board set's current board. Caller must hold <c>room.Lock</c>.</summary>
		public static LiveBoardSnapshot SnapshotOf(BoardSet set, long seq, int secondsRemaining = 0)
			=> new()
			{
				CurrentBoard = set.CurrentBoard,
				Boards = set.Boards.Keys.OrderBy(k => k).ToList(),
				// Every stroke on every board, in the order drawn. Each carries its board number
				// (CurrentBoard), so a late joiner can rebuild all the boards, not just the one showing.
				Strokes = set.Boards.Values.SelectMany(b => b).OrderBy(x => x.Seq).ToList(),
				Seq = seq,
				SecondsRemaining = secondsRemaining
			};
	}
}
