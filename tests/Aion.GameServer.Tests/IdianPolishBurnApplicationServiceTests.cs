using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class IdianPolishBurnApplicationServiceTests
{
	[Fact]
	public void ApplyBurnPlan_UpdatesInventoryAndCreatesLowAndExhaustedPackets()
	{
		var player = new Player
		{
			InventoryItems =
			[
				CreateItem(objectId: 10, itemId: 100, polishCharge: 350_000),
				CreateItem(objectId: 11, itemId: 101, polishCharge: 200_000),
				CreateItem(objectId: 12, itemId: 102, polishCharge: 500_000),
			],
		};
		var lowChargeUpdate = CreateItem(objectId: 10, itemId: 100, polishCharge: 250_000);
		var exhaustedUpdate = CreateItem(objectId: 11, itemId: 101);
		var unchangedPacketUpdate = CreateItem(objectId: 12, itemId: 102, polishCharge: 450_000);
		var plan = new IdianPolishBurnPlan(
			Changed: true,
			InventoryItems: [lowChargeUpdate, exhaustedUpdate, unchangedPacketUpdate],
			Burns:
			[
				new IdianPolishBurnResult(lowChargeUpdate, IdianPolishBurnUpdateKind.LowCharge, BurnAmount: 100_000),
				new IdianPolishBurnResult(exhaustedUpdate, IdianPolishBurnUpdateKind.Exhausted, BurnAmount: 250_000),
				new IdianPolishBurnResult(unchangedPacketUpdate, IdianPolishBurnUpdateKind.None, BurnAmount: 50_000),
			]);

		var result = IdianPolishBurnApplicationService.ApplyBurnPlan(player, plan, CreateItemTemplates());

		Assert.True(result.Changed);
		Assert.Same(player.InventoryItems, result.InventoryItems);
		Assert.Equal(250_000, player.InventoryItems.First(item => item.ObjectId == 10).IdianStone?.PolishCharge);
		Assert.Null(player.InventoryItems.First(item => item.ObjectId == 11).IdianStone);
		Assert.Equal(450_000, player.InventoryItems.First(item => item.ObjectId == 12).IdianStone?.PolishCharge);
		Assert.Collection(
			result.Packets,
			packet => AssertPolishChargePacket(packet, objectId: 10, polishCharge: 250_000),
			packet => AssertFullDecreasePacket(packet, objectId: 11));
	}

	[Fact]
	public void ApplyBurnPlan_UpdatesInventoryButSkipsPacketWhenTemplateMissing()
	{
		var player = new Player
		{
			InventoryItems =
			[
				CreateItem(objectId: 10, itemId: 999, polishCharge: 350_000),
			],
		};
		var lowChargeUpdate = CreateItem(objectId: 10, itemId: 999, polishCharge: 250_000);
		var plan = new IdianPolishBurnPlan(
			Changed: true,
			InventoryItems: [lowChargeUpdate],
			Burns: [new IdianPolishBurnResult(lowChargeUpdate, IdianPolishBurnUpdateKind.LowCharge, BurnAmount: 100_000)]);

		var result = IdianPolishBurnApplicationService.ApplyBurnPlan(player, plan, CreateItemTemplates());

		Assert.True(result.Changed);
		Assert.Equal(250_000, Assert.Single(player.InventoryItems).IdianStone?.PolishCharge);
		Assert.Empty(result.Packets);
	}

	[Fact]
	public void ApplyBurnPlan_NoOpsWhenPlanHasNoChanges()
	{
		var player = new Player
		{
			InventoryItems = [CreateItem(objectId: 10, itemId: 100, polishCharge: 350_000)],
		};

		var result = IdianPolishBurnApplicationService.ApplyBurnPlan(
			player,
			IdianPolishBurnPlan.NoChange(),
			CreateItemTemplates());

		Assert.False(result.Changed);
		Assert.Empty(result.InventoryItems);
		Assert.Empty(result.Packets);
		Assert.Equal(350_000, Assert.Single(player.InventoryItems).IdianStone?.PolishCharge);
	}

	private static void AssertPolishChargePacket(GameServerPacket packet, int objectId, int polishCharge)
	{
		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new PacketBuffer(payload);
		Assert.Equal(objectId, reader.ReadD());
		Assert.Equal(string.Empty, reader.ReadS());
		Assert.Equal(5, reader.ReadH());
		Assert.Equal(0x11, (int)reader.ReadC());
		Assert.Equal(polishCharge, reader.ReadD());
		Assert.Equal(0, reader.Remaining);
	}

	private static void AssertFullDecreasePacket(GameServerPacket packet, int objectId)
	{
		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new PacketBuffer(payload);
		Assert.Equal(objectId, reader.ReadD());
		Assert.Equal(string.Empty, reader.ReadS());
		var blobSize = reader.ReadH();
		Assert.True(blobSize > 0);
		reader.ReadB(blobSize);
		Assert.Equal(SmInventoryUpdateItem.DecreaseItemUse, reader.ReadH());
		Assert.Equal(0, reader.Remaining);
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}

	private static InventoryItem CreateItem(int objectId, int itemId, int polishCharge = 0)
	{
		var item = new InventoryItem
		{
			ObjectId = objectId,
			ItemId = itemId,
			Count = 1,
			Location = 0,
			IsEquipped = true,
			Slot = 1,
		};
		if (polishCharge > 0)
			item.IdianStone = new PlayerIdianStone(600, 1, polishCharge);
		return item;
	}

	private static ItemTemplateTable CreateItemTemplates()
	{
		return new ItemTemplateTable(
		[
			CreateTemplate(100, "item_100"),
			CreateTemplate(101, "item_101"),
			CreateTemplate(102, "item_102"),
		]);
	}

	private static ItemTemplateSummary CreateTemplate(int templateId, string name)
	{
		return new ItemTemplateSummary(
			templateId,
			name,
			0,
			1,
			1,
			"SWORD",
			"NORMAL",
			"COMMON",
			"PC_ALL",
			1,
			0,
			3,
			IdianInfo: new ItemIdianInfo(100_000, 100_000));
	}
}
