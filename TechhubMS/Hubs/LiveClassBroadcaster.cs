using Microsoft.AspNetCore.SignalR;
using TechHub.Service.Interface;

namespace TechhubMS.Hubs
{
	/// <summary>Lets the services push events to a live class's board connections without knowing about SignalR.</summary>
	public class LiveClassBroadcaster : ILiveClassBroadcaster
	{
		private readonly IHubContext<LiveBoardHub> _hub;
		private readonly LiveBoardState _state;

		public LiveClassBroadcaster(IHubContext<LiveBoardHub> hub, LiveBoardState state)
		{
			_hub = hub;
			_state = state;
		}

		public async Task ClassEndedAsync(Guid liveClassId, string reason)
		{
			await _hub.Clients.Group(LiveBoardHub.GroupName(liveClassId)).SendAsync("ClassEnded", new { liveClassId, reason });
			_state.Remove(liveClassId);
		}

		public Task WarningAsync(Guid liveClassId, int minutesLeft)
			=> _hub.Clients.Group(LiveBoardHub.GroupName(liveClassId)).SendAsync("Warning", new { liveClassId, minutesLeft });

		public Task SpeakChangedAsync(Guid liveClassId, Guid studentId, bool canSpeak)
			=> _hub.Clients.Group(LiveBoardHub.GroupName(liveClassId)).SendAsync("SpeakChanged", new { liveClassId, studentId, canSpeak });
	}
}
