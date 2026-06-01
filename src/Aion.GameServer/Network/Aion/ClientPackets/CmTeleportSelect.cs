using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmTeleportSelect : GameClientPacket
{
	public CmTeleportSelect(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int TargetObjectId { get; private set; }

	public int LocationId { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_TELEPORT_SELECT.readImpl.
		TargetObjectId = buffer.ReadD();
		LocationId = buffer.ReadD();
		_ = buffer.ReadSignedH();
	}
}
