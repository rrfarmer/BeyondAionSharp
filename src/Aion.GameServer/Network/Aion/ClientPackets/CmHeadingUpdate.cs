using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmHeadingUpdate : GameClientPacket
{
	public CmHeadingUpdate(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public byte Heading { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_HEADING_UPDATE.readImpl.
		Heading = buffer.ReadC();
	}
}
