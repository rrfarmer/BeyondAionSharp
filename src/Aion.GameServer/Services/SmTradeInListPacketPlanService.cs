using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Services;

public enum SmTradeInListPacketPlanStatus
{
	Ready,
	UnknownTradeNpcType,
	InvalidTradeInList,
}

public sealed record SmTradeInListPacketPlanInput(
	int TargetObjectId,
	TradeListTemplateSummary TradeInList,
	int BuyPriceModifier = 100);

public sealed record SmTradeInListPacketWriteField(string Type, string Name, int Value);

public sealed record SmTradeInListPacketPlan(
	SmTradeInListPacketPlanStatus Status,
	int TargetObjectId,
	int NpcId,
	int TradeNpcTypeIndex,
	int BuyPriceModifier,
	int FixedAion45Modifier,
	IReadOnlyList<int> TradeTabIds,
	IReadOnlyList<SmTradeInListPacketWriteField> JavaWriteOrder,
	string JavaSource,
	bool IsLive = false);

public static class SmTradeInListPacketPlanService
{
	private const int FixedAion45Modifier = 100;

	public static SmTradeInListPacketPlan CreatePlan(SmTradeInListPacketPlanInput input)
	{
		ArgumentNullException.ThrowIfNull(input.TradeInList);

		// Java parity breadcrumbs:
		// - services/DialogService.onDialogSelect TRADE_IN sends new SM_TRADE_IN_LIST(npc, tradeListTemplate, 100).
		// - network/aion/serverpackets/SM_TRADE_IN_LIST.writeImpl writes only when the template
		//   exists, has a non-zero npc id, and has at least one trade tab.
		var hasKnownTradeNpcType = TryGetTradeNpcTypeIndex(input.TradeInList.NpcType, out var tradeNpcTypeIndex);
		var status = !hasKnownTradeNpcType
			? SmTradeInListPacketPlanStatus.UnknownTradeNpcType
			: input.TradeInList.NpcId == 0 || input.TradeInList.GoodsListIds.Count == 0
				? SmTradeInListPacketPlanStatus.InvalidTradeInList
				: SmTradeInListPacketPlanStatus.Ready;
		var tradeTabIds = input.TradeInList.GoodsListIds;
		var javaWriteOrder = status == SmTradeInListPacketPlanStatus.Ready
			? CreateWriteOrder(
				input.TargetObjectId,
				tradeNpcTypeIndex,
				input.BuyPriceModifier,
				tradeTabIds)
			: Array.Empty<SmTradeInListPacketWriteField>();

		return new SmTradeInListPacketPlan(
			status,
			input.TargetObjectId,
			input.TradeInList.NpcId,
			tradeNpcTypeIndex,
			input.BuyPriceModifier,
			FixedAion45Modifier,
			tradeTabIds,
			javaWriteOrder,
			"SM_TRADE_IN_LIST(Npc, TradeListTemplate, int) + writeImpl",
			IsLive: false);
	}

	private static IReadOnlyList<SmTradeInListPacketWriteField> CreateWriteOrder(
		int targetObjectId,
		int tradeNpcTypeIndex,
		int buyPriceModifier,
		IReadOnlyList<int> tradeTabIds)
	{
		var fields = new List<SmTradeInListPacketWriteField>
		{
			new("D", "npc.objectId", targetObjectId),
			new("C", "tradeNpcType.index", tradeNpcTypeIndex),
			new("D", "buyPriceModifier", buyPriceModifier),
			new("D", "fixedAion45Modifier", FixedAion45Modifier),
			new("H", "tradeTabCount", tradeTabIds.Count),
		};

		foreach (var tradeTabId in tradeTabIds)
		{
			fields.Add(new SmTradeInListPacketWriteField("D", "tradeTabId", tradeTabId));
		}

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
