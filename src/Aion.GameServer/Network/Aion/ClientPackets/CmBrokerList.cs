using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmBrokerList : GameClientPacket
{
	public CmBrokerList(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int BrokerObjectId { get; private set; }

	public byte SortType { get; private set; }

	public int Page { get; private set; }

	public int ListMask { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_BROKER_LIST.readImpl.
		BrokerObjectId = buffer.ReadD();
		SortType = buffer.ReadC();
		Page = buffer.ReadH();
		ListMask = buffer.ReadH();
	}
}
