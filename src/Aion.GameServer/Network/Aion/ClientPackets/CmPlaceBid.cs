using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmPlaceBid : GameClientPacket
{
	public CmPlaceBid(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int ListIndex { get; private set; }

	public long BidOffer { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_PLACE_BID.readImpl.
		ListIndex = buffer.ReadD();
		BidOffer = buffer.ReadQ();
	}
}
