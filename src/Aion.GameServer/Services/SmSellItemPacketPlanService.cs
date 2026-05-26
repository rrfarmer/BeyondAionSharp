using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Services;

public enum SmSellItemPacketPlanStatus
{
	Ready,
	UnknownTradeNpcType,
}

public sealed record SmSellItemPacketPlanInput(
	int TargetObjectId,
	TradeListTemplateSummary? PurchaseTemplate,
	bool NpcCanSell,
	bool NpcCanBuy,
	bool NpcCanPurchase,
	GameServerPriceOptions? PriceOptions = null);

public sealed record SmSellItemPacketWriteField(string Type, string Name, int Value);

public sealed record SmSellItemPacketPlan(
	SmSellItemPacketPlanStatus Status,
	int TargetObjectId,
	int TradeNpcTypeIndex,
	int BuyPriceRate,
	bool ShowBuyTab,
	bool ShowSellTab,
	IReadOnlyList<int> TradeTabIds,
	IReadOnlyList<SmSellItemPacketWriteField> JavaWriteOrder,
	string JavaSource,
	bool IsLive = false);

public static class SmSellItemPacketPlanService
{
	public static SmSellItemPacketPlan CreatePlan(SmSellItemPacketPlanInput input)
	{
		// Java parity: network/aion/serverpackets/SM_SELL_ITEM constructor and writeImpl.
		var npcType = input.PurchaseTemplate?.NpcType ?? "NORMAL";
		var status = TryGetTradeNpcTypeIndex(npcType, out var tradeNpcTypeIndex)
			? SmSellItemPacketPlanStatus.Ready
			: SmSellItemPacketPlanStatus.UnknownTradeNpcType;
		var buyPriceRate = input.PurchaseTemplate?.BuyPriceRate
			?? PricesService.GetVendorSellModifier(input.PriceOptions ?? new GameServerPriceOptions());
		var tradeTabIds = input.PurchaseTemplate?.GoodsListIds ?? Array.Empty<int>();
		var showBuyTab = input.NpcCanSell;
		var showSellTab = input.NpcCanBuy || input.NpcCanPurchase;
		return new SmSellItemPacketPlan(
			status,
			input.TargetObjectId,
			tradeNpcTypeIndex,
			buyPriceRate,
			showBuyTab,
			showSellTab,
			tradeTabIds,
			CreateWriteOrder(input.TargetObjectId, tradeNpcTypeIndex, buyPriceRate, showBuyTab, showSellTab, tradeTabIds),
			"SM_SELL_ITEM(Npc) + writeImpl",
			IsLive: false);
	}

	private static IReadOnlyList<SmSellItemPacketWriteField> CreateWriteOrder(
		int targetObjectId,
		int tradeNpcTypeIndex,
		int buyPriceRate,
		bool showBuyTab,
		bool showSellTab,
		IReadOnlyList<int> tradeTabIds)
	{
		var fields = new List<SmSellItemPacketWriteField>
		{
			new("D", "targetObjectId", targetObjectId),
			new("C", "tradeNpcType.index", tradeNpcTypeIndex),
			new("D", "buyPriceRate", buyPriceRate),
			new("C", "showBuyTab", showBuyTab ? 1 : 0),
			new("C", "showSellTab", showSellTab ? 1 : 0),
			new("H", "tradeTabCount", tradeTabIds.Count),
		};

		foreach (var tradeTabId in tradeTabIds)
			fields.Add(new SmSellItemPacketWriteField("D", "tradeTabId", tradeTabId));

		return fields.AsReadOnly();
	}

	private static bool TryGetTradeNpcTypeIndex(string npcType, out int index)
	{
		index = npcType switch
		{
			"NORMAL" => 1,
			"ABYSS" => 2,
			"LEGION_COIN" => 3,
			"REWARD" => 4,
			"ABYSS_KINAH" => 5,
			_ => 0,
		};

		return index != 0;
	}
}
