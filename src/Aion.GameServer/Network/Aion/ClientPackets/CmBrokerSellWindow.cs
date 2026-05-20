using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmBrokerSellWindow : GameClientPacket
{
	public CmBrokerSellWindow(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int ItemObjectId { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_BROKER_SELL_WINDOW.readImpl.
		ItemObjectId = buffer.ReadD();
	}
}
