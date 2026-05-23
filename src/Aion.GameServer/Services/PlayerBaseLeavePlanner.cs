using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum PlayerBaseLeavePacketIntentKind
{
	LeaveGroupMember,
	SystemMessage,
}

public sealed record PlayerBaseLeaveSideEffectPlan(
	int PlayerObjectId,
	bool IsOnline,
	bool WasRegisteredToTeamInstance,
	IReadOnlyList<PlayerBaseLeavePacketIntent> PacketIntents,
	bool WouldScheduleInstanceKick,
	TimeSpan? InstanceKickDelay,
	bool WouldNotifyEventServiceOnLeftTeam);

public sealed record PlayerBaseLeavePacketIntent(
	int Sequence,
	int RecipientObjectId,
	PlayerBaseLeavePacketIntentKind Kind,
	SmSystemMessage? SystemMessage = null)
{
	public GameServerPacket CreatePacket()
	{
		// Java parity: model/team/common/events/PlayerLeavedEvent sends these packets to the leaved player when online.
		return Kind switch
		{
			PlayerBaseLeavePacketIntentKind.LeaveGroupMember => new SmLeaveGroupMember(),
			PlayerBaseLeavePacketIntentKind.SystemMessage when SystemMessage != null => SystemMessage,
			_ => throw new InvalidOperationException("Base leave packet intent is missing packet metadata."),
		};
	}
}

public sealed class PlayerBaseLeavePlanner
{
	public PlayerBaseLeaveSideEffectPlan CreateLeaveSideEffectPlan(
		int playerObjectId,
		bool isOnline,
		bool wasRegisteredToTeamInstance)
	{
		// Java parity: model/team/common/events/PlayerLeavedEvent.handleEvent packet and deferred instance-kick side effects.
		var intents = new List<PlayerBaseLeavePacketIntent>();
		var sequence = 0;
		var shouldScheduleKick = false;
		if (isOnline)
		{
			intents.Add(new PlayerBaseLeavePacketIntent(
				sequence++,
				playerObjectId,
				PlayerBaseLeavePacketIntentKind.LeaveGroupMember));

			if (wasRegisteredToTeamInstance)
			{
				intents.Add(new PlayerBaseLeavePacketIntent(
					sequence++,
					playerObjectId,
					PlayerBaseLeavePacketIntentKind.SystemMessage,
					SmSystemMessage.LeaveInstanceNotParty()));
				shouldScheduleKick = true;
			}
		}

		return new PlayerBaseLeaveSideEffectPlan(
			playerObjectId,
			isOnline,
			wasRegisteredToTeamInstance,
			intents,
			shouldScheduleKick,
			shouldScheduleKick ? TimeSpan.FromSeconds(30) : null,
			WouldNotifyEventServiceOnLeftTeam: true);
	}
}
