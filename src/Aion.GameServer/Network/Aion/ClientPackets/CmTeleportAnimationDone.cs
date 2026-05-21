using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmTeleportAnimationDone : GameClientPacket
{
	public CmTeleportAnimationDone(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_TELEPORT_ANIMATION_DONE.readImpl has no payload.
	}
}
