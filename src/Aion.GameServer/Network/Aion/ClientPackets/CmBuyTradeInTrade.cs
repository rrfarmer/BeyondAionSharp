using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmBuyTradeInTrade : GameClientPacket
{
	public CmBuyTradeInTrade(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int SellerObjectId { get; private set; }

	public byte Mask { get; private set; }

	public int ItemId { get; private set; }

	public int Count { get; private set; }

	public int TradeInListCount { get; private set; }

	public IReadOnlyList<int> TradeInItemObjectIds { get; private set; } = Array.Empty<int>();

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_BUY_TRADE_IN_TRADE.readImpl.
		SellerObjectId = buffer.ReadD();
		Mask = buffer.ReadC();
		ItemId = buffer.ReadD();
		Count = buffer.ReadD();
		TradeInListCount = buffer.ReadH();

		var tradeInItemObjectIds = new List<int>(TradeInListCount);
		for (var i = 0; i < TradeInListCount; i++)
			tradeInItemObjectIds.Add(buffer.ReadD());

		TradeInItemObjectIds = tradeInItemObjectIds;
	}
}
