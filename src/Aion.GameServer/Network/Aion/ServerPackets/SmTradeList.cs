using Aion.Commons.Network;
using Aion.GameServer.Services;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmTradeList : GameServerPacket
{
	public const int PacketOpCode = 253;

	private readonly SmTradeListPacketPlan _plan;

	public SmTradeList(SmTradeListPacketPlan plan)
		: base(PacketOpCode)
	{
		ArgumentNullException.ThrowIfNull(plan);
		if (plan.Status != SmTradeListPacketPlanStatus.Ready)
			throw new ArgumentException("Trade-list packet plans must be ready before serialization.", nameof(plan));

		// Java parity: network/aion/serverpackets/SM_TRADELIST.writeImpl.
		// This packet is available for byte-shape tests, but GameServerConnection still keeps BUY non-sending
		// until live price/legion/limited-item/runtime routing facts are wired and verified.
		_plan = plan;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		buffer.WriteD(_plan.TargetObjectId);
		buffer.WriteC((byte)_plan.TradeNpcTypeIndex);
		buffer.WriteD(_plan.BuyPriceModifier);
		buffer.WriteD(_plan.FixedAion45Modifier);
		buffer.WriteC(_plan.ShowBuyTab ? (byte)1 : (byte)0);
		buffer.WriteC(_plan.ShowSellTab ? (byte)1 : (byte)0);
		buffer.WriteH((short)_plan.TradeTabIds.Count);
		foreach (var tradeTabId in _plan.TradeTabIds)
			buffer.WriteD(tradeTabId);

		buffer.WriteH((short)_plan.LimitedItems.Count);
		foreach (var limitedItem in _plan.LimitedItems)
		{
			buffer.WriteD(limitedItem.ItemId);
			buffer.WriteH((short)limitedItem.BuyCount);
			buffer.WriteH((short)limitedItem.SellLimit);
		}
	}
}
