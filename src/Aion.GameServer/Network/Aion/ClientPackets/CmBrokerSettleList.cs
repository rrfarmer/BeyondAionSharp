using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmBrokerSettleList : GameClientPacket
{
	public CmBrokerSettleList(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int BrokerObjectId { get; private set; }

	public int StartPageIndex { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_BROKER_SETTLE_LIST.readImpl.
		BrokerObjectId = buffer.ReadD();
		StartPageIndex = buffer.ReadH();
	}
}
