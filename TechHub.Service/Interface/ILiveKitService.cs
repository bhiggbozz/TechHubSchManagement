using System;
using System.Threading.Tasks;

namespace TechHub.Service.Interface
{
	/// <summary>Talks to the self-hosted LiveKit server: issues join tokens and manages rooms.</summary>
	public interface ILiveKitService
	{
		bool IsConfigured { get; }

		/// <summary>wss:// address browsers connect to.</summary>
		string ClientUrl { get; }

		/// <summary>
		/// Short-lived token for one person to join one room. Audio only — nobody can publish video —
		/// and a student can publish (speak) only when <paramref name="canSpeak"/> is true.
		/// </summary>
		string CreateJoinToken(string roomName, string identity, string displayName, bool canSpeak, TimeSpan ttl, bool canPublishData = false);

		/// <summary>Creates the room capped at <paramref name="maxParticipants"/>. False when LiveKit is unreachable or refuses.</summary>
		Task<bool> CreateRoomAsync(string roomName, int maxParticipants, int emptyTimeoutSeconds);

		/// <summary>Closes the room and disconnects everyone in it. Best effort.</summary>
		Task DeleteRoomAsync(string roomName);

		/// <summary>Grants or removes a connected participant's right to publish audio. False when it could not be applied.</summary>
		Task<bool> SetCanSpeakAsync(string roomName, string identity, bool canSpeak);
	}
}
