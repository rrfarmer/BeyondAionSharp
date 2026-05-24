using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class ItemChargeBurnApplicationServiceTests
{
	[Fact]
	public void ApplyBurnPlan_UpdatesInventoryAndPacketsOnlyChangedChargeBars()
	{
		var player = new Player
		{
			InventoryItems =
			[
				CreateItem(objectId: 10, itemId: 100, charge: 100_050),
				CreateItem(objectId: 11, itemId: 101, charge: 120_000),
				CreateItem(objectId: 12, itemId: 102, charge: 120_000),
			],
		};
		var firstUpdate = CreateItem(objectId: 10, itemId: 100, charge: 99_850);
		var secondUpdate = CreateItem(objectId: 11, itemId: 101, charge: 119_800);
		var plan = new ItemChargeBurnPlan(
			Changed: true,
			InventoryItems: [firstUpdate, secondUpdate],
			Burns:
			[
				new ItemChargeUpdateResult(firstUpdate, ChargeBarChanged: true, PointsDelta: -200),
				new ItemChargeUpdateResult(secondUpdate, ChargeBarChanged: false, PointsDelta: -200),
			]);

		var result = ItemChargeBurnApplicationService.ApplyBurnPlan(player, plan, CreateItemTemplates());

		Assert.True(result.Changed);
		Assert.Same(player.InventoryItems, result.InventoryItems);
		Assert.Equal(99_850, player.InventoryItems.First(item => item.ObjectId == 10).Charge);
		Assert.Equal(119_800, player.InventoryItems.First(item => item.ObjectId == 11).Charge);
		Assert.Equal(120_000, player.InventoryItems.First(item => item.ObjectId == 12).Charge);
		var packet = Assert.Single(result.Packets);
		AssertChargePacket(packet, objectId: 10, charge: 99_850);
	}

	[Fact]
	public void ApplyBurnPlan_UpdatesInventoryButSkipsPacketWhenTemplateMissing()
	{
		var player = new Player
		{
			InventoryItems =
			[
				CreateItem(objectId: 10, itemId: 999, charge: 100_050),
			],
		};
		var update = CreateItem(objectId: 10, itemId: 999, charge: 99_850);
		var plan = new ItemChargeBurnPlan(
			Changed: true,
			InventoryItems: [update],
			Burns: [new ItemChargeUpdateResult(update, ChargeBarChanged: true, PointsDelta: -200)]);

		var result = ItemChargeBurnApplicationService.ApplyBurnPlan(player, plan, CreateItemTemplates());

		Assert.True(result.Changed);
		Assert.Equal(99_850, Assert.Single(player.InventoryItems).Charge);
		Assert.Empty(result.Packets);
	}

	[Fact]
	public void ApplyBurnPlan_NoOpsWhenPlanHasNoChanges()
	{
		var player = new Player
		{
			InventoryItems = [CreateItem(objectId: 10, itemId: 100, charge: 100_050)],
		};

		var result = ItemChargeBurnApplicationService.ApplyBurnPlan(
			player,
			ItemChargeBurnPlan.NoChange(),
			CreateItemTemplates());

		Assert.False(result.Changed);
		Assert.Empty(result.InventoryItems);
		Assert.Empty(result.Packets);
		Assert.Equal(100_050, Assert.Single(player.InventoryItems).Charge);
	}

	private static void AssertChargePacket(GameServerPacket packet, int objectId, int charge)
	{
		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new PacketBuffer(payload);
		Assert.Equal(objectId, reader.ReadD());
		Assert.Equal(string.Empty, reader.ReadS());
		Assert.Equal(5, reader.ReadH());
		Assert.Equal(0x0f, (int)reader.ReadC());
		Assert.Equal(charge, reader.ReadD());
		Assert.Equal(0, reader.Remaining);
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}

	private static InventoryItem CreateItem(int objectId, int itemId, int charge)
	{
		return new InventoryItem
		{
			ObjectId = objectId,
			ItemId = itemId,
			Count = 1,
			Location = 0,
			IsEquipped = true,
			Charge = charge,
		};
	}

	private static ItemTemplateTable CreateItemTemplates()
	{
		return new ItemTemplateTable(
		[
			CreateTemplate(100, "item_10"),
			CreateTemplate(101, "item_11"),
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
			Improvement: new ItemImprovement(1, 2, 200, 100, 1000, 2000));
	}
}
