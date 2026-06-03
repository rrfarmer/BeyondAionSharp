using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmLegionSendEmblem : GameClientPacket
{
	public CmLegionSendEmblem(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int LegionId { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_LEGION_SEND_EMBLEM.readImpl.
		LegionId = buffer.ReadD();
	}
}
