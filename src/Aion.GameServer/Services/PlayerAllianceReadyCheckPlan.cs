using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum PlayerAllianceReadyCheckCommand
{
	Cancel = 20,
	Start = 21,
	AutoCancel = 22,
	Ready = 23,
	NotReady = 24,
}

public sealed record PlayerAllianceReadyCheckPlan(
	int AllianceId,
	int PlayerObjectId,
	PlayerAllianceReadyCheckCommand Command,
	int ReadyStatusBefore,
	int ReadyStatusAfter,
	IReadOnlyList<PlayerAllianceReadyCheckPacketIntent> PacketIntents);

public sealed record PlayerAllianceReadyCheckPacketIntent(
	int Sequence,
	int RecipientObjectId,
	int PlayerObjectId,
	int StatusCode)
{
	public GameServerPacket CreatePacket()
	{
		// Java parity: model/team/alliance/events/CheckAllianceReadyEvent sends SM_ALLIANCE_READY_CHECK to each alliance member.
		return new SmAllianceReadyCheck(PlayerObjectId, StatusCode);
	}
}
