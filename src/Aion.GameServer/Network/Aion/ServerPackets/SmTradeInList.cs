using Aion.Commons.Network;
using Aion.GameServer.Services;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmTradeInList : GameServerPacket
{
	public const int PacketOpCode = 151;

	private readonly SmTradeInListPacketPlan _plan;

	public SmTradeInList(SmTradeInListPacketPlan plan)
		: base(PacketOpCode)
	{
		ArgumentNullException.ThrowIfNull(plan);
		if (plan.Status != SmTradeInListPacketPlanStatus.Ready)
			throw new ArgumentException("Trade-in list packet plans must be ready before serialization.", nameof(plan));

		// Java parity: network/aion/serverpackets/SM_TRADE_IN_LIST.writeImpl.
		// This packet is available for byte-shape tests, but GameServerConnection still keeps
		// TRADE_IN non-sending until live controller routing and Java runtime vectors are ready.
		_plan = plan;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		buffer.WriteD(_plan.TargetObjectId);
		buffer.WriteC((byte)_plan.TradeNpcTypeIndex);
		buffer.WriteD(_plan.BuyPriceModifier);
		buffer.WriteD(_plan.FixedAion45Modifier);
		buffer.WriteH((short)_plan.TradeTabIds.Count);
		foreach (var tradeTabId in _plan.TradeTabIds)
			buffer.WriteD(tradeTabId);
	}
}
