using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class EquipmentObserverBurnWorkflowServiceTests
{
	[Fact]
	public async Task ApplyObserverBurnsAsync_AppliesIdianBeforeChargeAndRequestsBothPersistenceBoundaries()
	{
		var player = new Player
		{
			ObjectId = 1001,
			InventoryItems =
			[
				CreateItem(objectId: 10, itemId: 100, charge: 100_050, polishCharge: 350_000),
			],
		};
		var idianSavedObjects = new List<int>();
		var chargeSavedObjects = new List<int>();

		var result = await EquipmentObserverBurnWorkflowService.ApplyObserverBurnsAsync(
			player,
			CreateItemTemplates(),
			EquipmentObserverBurnEvent.Attack,
			skillId: 0,
			saveIdianPolishBurnAsync: (_, plan, _) =>
			{
				idianSavedObjects.AddRange(plan.Burns.Select(burn => burn.ItemUpdate.ObjectId));
				return Task.FromResult(true);
			},
			saveItemChargeBurnAsync: (_, plan, _) =>
			{
				chargeSavedObjects.AddRange(plan.Burns.Select(burn => burn.ItemUpdate.ObjectId));
				return Task.FromResult(true);
			});

		Assert.True(result.Changed);
		Assert.True(result.Persisted);
		Assert.Equal([10], idianSavedObjects);
		Assert.Equal([10], chargeSavedObjects);
		var item = Assert.Single(player.InventoryItems);
		Assert.Equal(250_000, item.IdianStone?.PolishCharge);
		Assert.Equal(99_850, item.Charge);
		Assert.Collection(
			result.Packets,
			packet => AssertPolishChargePacket(packet, objectId: 10, polishCharge: 250_000),
			packet => AssertChargePacket(packet, objectId: 10, charge: 99_850));
	}

	[Fact]
	public async Task ApplyObserverBurnsAsync_SkipsSkillAttackButAllowsDotAttacked()
	{
		var skillAttackPlayer = new Player
		{
			ObjectId = 1001,
			InventoryItems =
			[
				CreateItem(objectId: 10, itemId: 100, charge: 100_050, polishCharge: 350_000),
			],
		};
		var dotAttackedPlayer = new Player
		{
			ObjectId = 1002,
			InventoryItems =
			[
				CreateItem(objectId: 10, itemId: 100, charge: 100_050, polishCharge: 350_000),
			],
		};

		var skillAttack = await EquipmentObserverBurnWorkflowService.ApplyObserverBurnsAsync(
			skillAttackPlayer,
			CreateItemTemplates(),
			EquipmentObserverBurnEvent.Attack,
			skillId: 2001);
		var dotAttacked = await EquipmentObserverBurnWorkflowService.ApplyObserverBurnsAsync(
			dotAttackedPlayer,
			CreateItemTemplates(),
			EquipmentObserverBurnEvent.DotAttacked,
			skillId: 2001);

		Assert.False(skillAttack.Changed);
		Assert.Empty(skillAttack.Packets);
		Assert.True(skillAttack.Persisted);
		Assert.Equal(350_000, Assert.Single(skillAttackPlayer.InventoryItems).IdianStone?.PolishCharge);
		Assert.Equal(100_050, Assert.Single(skillAttackPlayer.InventoryItems).Charge);

		Assert.True(dotAttacked.Changed);
		var item = Assert.Single(dotAttackedPlayer.InventoryItems);
		Assert.Equal(290_000, item.IdianStone?.PolishCharge);
		Assert.Equal(99_950, item.Charge);
		Assert.Collection(
			dotAttacked.Packets,
			packet => AssertPolishChargePacket(packet, objectId: 10, polishCharge: 290_000),
			packet => AssertChargePacket(packet, objectId: 10, charge: 99_950));
	}

	[Fact]
	public async Task ApplyObserverBurnsAsync_ReportsPersistenceFailureAfterApplyingPackets()
	{
		var player = new Player
		{
			ObjectId = 1001,
			InventoryItems =
			[
				CreateItem(objectId: 10, itemId: 100, charge: 100_050, polishCharge: 350_000),
			],
		};

		var result = await EquipmentObserverBurnWorkflowService.ApplyObserverBurnsAsync(
			player,
			CreateItemTemplates(),
			EquipmentObserverBurnEvent.Attack,
			skillId: 0,
			saveIdianPolishBurnAsync: (_, _, _) => Task.FromResult(false),
			saveItemChargeBurnAsync: (_, _, _) => Task.FromResult(true));

		Assert.True(result.Changed);
		Assert.False(result.Persisted);
		Assert.False(result.IdianPersisted);
		Assert.True(result.ChargePersisted);
		Assert.Equal(2, result.Packets.Count);
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

	private static InventoryItem CreateItem(int objectId, int itemId, int charge, int polishCharge)
	{
		return new InventoryItem
		{
			ObjectId = objectId,
			ItemId = itemId,
			Count = 1,
			Location = 0,
			IsEquipped = true,
			Slot = 1,
			Charge = charge,
			IdianStone = new PlayerIdianStone(600, 1, polishCharge),
		};
	}

	private static ItemTemplateTable CreateItemTemplates()
	{
		return new ItemTemplateTable(
		[
			new ItemTemplateSummary(
				100,
				"item_100",
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
				Improvement: new ItemImprovement(ChargeWay: 1, Level: 2, BurnAttack: 200, BurnDefend: 100, Price1: 1000, Price2: 2000),
				IdianInfo: new ItemIdianInfo(BurnAttack: 100_000, BurnDefend: 60_000)),
			new ItemTemplateSummary(
				600,
				"idian_600",
				0,
				1,
				1,
				"NONE",
				"NORMAL",
				"COMMON",
				"PC_ALL",
				1,
				0,
				0,
				PolishSetId: 12),
		]);
	}
}
