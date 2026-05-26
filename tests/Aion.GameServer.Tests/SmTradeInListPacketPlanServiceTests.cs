using Aion.GameServer.Dataholders;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class SmTradeInListPacketPlanServiceTests
{
	[Fact]
	public void CreatePlan_ModelsJavaWriteOrderWithoutGoodsOrLegionFiltering()
	{
		var plan = SmTradeInListPacketPlanService.CreatePlan(
			new SmTradeInListPacketPlanInput(
				TargetObjectId: 9001,
				TradeInList: new TradeListTemplateSummary(
					205315,
					[39, 40],
					NpcType: "REWARD")));

		Assert.Equal(SmTradeInListPacketPlanStatus.Ready, plan.Status);
		Assert.Equal(4, plan.TradeNpcTypeIndex);
		Assert.Equal(100, plan.BuyPriceModifier);
		Assert.Equal(100, plan.FixedAion45Modifier);
		Assert.Equal([39, 40], plan.TradeTabIds);
		Assert.False(plan.IsLive);
		Assert.Equal(
			[
				new SmTradeInListPacketWriteField("D", "npc.objectId", 9001),
				new SmTradeInListPacketWriteField("C", "tradeNpcType.index", 4),
				new SmTradeInListPacketWriteField("D", "buyPriceModifier", 100),
				new SmTradeInListPacketWriteField("D", "fixedAion45Modifier", 100),
				new SmTradeInListPacketWriteField("H", "tradeTabCount", 2),
				new SmTradeInListPacketWriteField("D", "tradeTabId", 39),
				new SmTradeInListPacketWriteField("D", "tradeTabId", 40),
			],
			plan.JavaWriteOrder);
	}

	[Theory]
	[InlineData("NORMAL", 1)]
	[InlineData("ABYSS", 2)]
	[InlineData("LEGION_COIN", 3)]
	[InlineData("REWARD", 4)]
	[InlineData("ABYSS_KINAH", 5)]
	public void CreatePlan_MapsJavaTradeNpcTypeIndexes(string npcType, int expectedIndex)
	{
		var plan = SmTradeInListPacketPlanService.CreatePlan(
			new SmTradeInListPacketPlanInput(
				TargetObjectId: 9001,
				TradeInList: new TradeListTemplateSummary(205315, [39], NpcType: npcType)));

		Assert.Equal(SmTradeInListPacketPlanStatus.Ready, plan.Status);
		Assert.Equal(expectedIndex, plan.TradeNpcTypeIndex);
		Assert.Equal(expectedIndex, plan.JavaWriteOrder[1].Value);
	}

	[Fact]
	public void CreatePlan_ReportsUnknownTradeNpcTypeWithoutClaimingReady()
	{
		var plan = SmTradeInListPacketPlanService.CreatePlan(
			new SmTradeInListPacketPlanInput(
				TargetObjectId: 9001,
				TradeInList: new TradeListTemplateSummary(205315, [39], NpcType: "CUSTOM")));

		Assert.Equal(SmTradeInListPacketPlanStatus.UnknownTradeNpcType, plan.Status);
		Assert.Equal(0, plan.TradeNpcTypeIndex);
		Assert.Empty(plan.JavaWriteOrder);
		Assert.False(plan.IsLive);
	}

	[Theory]
	[InlineData(0, new[] { 39 })]
	[InlineData(205315, new int[0])]
	public void CreatePlan_ReportsInvalidTradeInListForJavaWriteGuards(int npcId, int[] tradeTabIds)
	{
		var plan = SmTradeInListPacketPlanService.CreatePlan(
			new SmTradeInListPacketPlanInput(
				TargetObjectId: 9001,
				TradeInList: new TradeListTemplateSummary(npcId, tradeTabIds)));

		Assert.Equal(SmTradeInListPacketPlanStatus.InvalidTradeInList, plan.Status);
		Assert.Empty(plan.JavaWriteOrder);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void SmTradeInList_WritesJavaPayloadOrderFromReadyPlan()
	{
		var plan = SmTradeInListPacketPlanService.CreatePlan(
			new SmTradeInListPacketPlanInput(
				TargetObjectId: 9001,
				TradeInList: new TradeListTemplateSummary(
					205315,
					[39, 40],
					NpcType: "NORMAL")));

		Assert.Equal(SmTradeInList.PacketOpCode, new SmTradeInList(plan).OpCode);
		Assert.Equal(
			Convert.FromHexString("2923000001640000006400000002002700000028000000"),
			SerializeUnencryptedPayload(new SmTradeInList(plan)));
	}

	[Fact]
	public void SmTradeInList_RejectsNonReadyPlans()
	{
		var plan = SmTradeInListPacketPlanService.CreatePlan(
			new SmTradeInListPacketPlanInput(
				TargetObjectId: 9001,
				TradeInList: new TradeListTemplateSummary(205315, [39], NpcType: "CUSTOM")));

		var ex = Assert.Throws<ArgumentException>(() => new SmTradeInList(plan));
		Assert.Contains("ready", ex.Message, StringComparison.Ordinal);
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}
}
