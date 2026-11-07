using System;
using System.Threading.Tasks;
using TechHub.Core;
using TechHub.Core.Model;
using TechHub.Core.ViewModel.LiveClassModels;

namespace TechHub.Service.Interface
{
	/// <summary>Who someone is in a particular live class (used by the board hub to authorise every connection).</summary>
	public class LiveClassAccess
	{
		public Guid LiveClassId { get; set; }
		public Guid SchoolId { get; set; }
		public string Status { get; set; } = string.Empty;
		public bool IsTeacher { get; set; }
		public bool IsStudent { get; set; }
		public DateTime? EndsAt { get; set; }
	}

	/// <summary>
	/// Live classes: a teacher plus up to three students the teacher picked. Audio is carried by
	/// LiveKit, the board by SignalR; the teacher's normal board recording keeps running, so every
	/// class becomes a replay lesson.
	/// </summary>
	public interface ILiveClassService
	{
		// ── Teacher ───────────────────────────────────────────────────────────
		Task<BaseResponse> CreateAsync(CreateLiveClassViewModel model, AuthenticatedUserClaims claims);
		Task<BaseResponse> StartAsync(Guid liveClassId, AuthenticatedUserClaims claims);
		Task<BaseResponse> EndAsync(Guid liveClassId, AuthenticatedUserClaims claims);
		Task<BaseResponse> CancelAsync(Guid liveClassId, AuthenticatedUserClaims claims);
		Task<BaseResponse> SetStudentSpeakAsync(Guid liveClassId, Guid studentId, bool allow, AuthenticatedUserClaims claims);

		// ── Teacher or picked student ─────────────────────────────────────────
		Task<BaseResponse> GetAsync(Guid liveClassId, AuthenticatedUserClaims claims);
		Task<BaseResponse> MineAsync(AuthenticatedUserClaims claims);
		Task<BaseResponse> JoinAsync(Guid liveClassId, AuthenticatedUserClaims claims);

		// ── Used by the board hub and the timer ───────────────────────────────
		Task<LiveClassAccess?> GetAccessAsync(Guid liveClassId, Guid userId, Guid schoolId);
		Task RecordStudentLeftAsync(Guid liveClassId, Guid studentId);

		/// <summary>One timer pass: sends 5/2-minute warnings, ends classes that ran out of time, expires stale unstarted ones.</summary>
		Task TickAsync();
	}

	/// <summary>Pushes live-class events to everyone connected to the board (implemented over SignalR in the API project).</summary>
	public interface ILiveClassBroadcaster
	{
		Task ClassEndedAsync(Guid liveClassId, string reason);
		Task WarningAsync(Guid liveClassId, int minutesLeft);
		Task SpeakChangedAsync(Guid liveClassId, Guid studentId, bool canSpeak);
	}
}
