using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmUnwrapItem : GameClientPacket
{
	public CmUnwrapItem(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int ObjectId { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_UNWRAP_ITEM.readImpl.
		ObjectId = buffer.ReadD();
	}
}
