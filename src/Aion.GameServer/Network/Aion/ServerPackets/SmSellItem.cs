using Aion.Commons.Network;
using Aion.GameServer.Services;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmSellItem : GameServerPacket
{
	public const int PacketOpCode = 62;

	private readonly SmSellItemPacketPlan _plan;

	public SmSellItem(SmSellItemPacketPlan plan)
		: base(PacketOpCode)
	{
		ArgumentNullException.ThrowIfNull(plan);
		if (plan.Status != SmSellItemPacketPlanStatus.Ready)
			throw new ArgumentException("Sell-item packet plans must be ready before serialization.", nameof(plan));

		// Java parity: network/aion/serverpackets/SM_SELL_ITEM.writeImpl.
		// The packet is available for byte-shape tests; dialog routing still keeps sell-window sends staged.
		_plan = plan;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		buffer.WriteD(_plan.TargetObjectId);
		buffer.WriteC((byte)_plan.TradeNpcTypeIndex);
		buffer.WriteD(_plan.BuyPriceRate);
		buffer.WriteC(_plan.ShowBuyTab ? (byte)1 : (byte)0);
		buffer.WriteC(_plan.ShowSellTab ? (byte)1 : (byte)0);
		buffer.WriteH((short)_plan.TradeTabIds.Count);
		foreach (var tradeTabId in _plan.TradeTabIds)
			buffer.WriteD(tradeTabId);
	}
}
