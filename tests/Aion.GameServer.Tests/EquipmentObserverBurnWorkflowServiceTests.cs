using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

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
	public async Task ApplyObserverBurnsAsync_PassesCleanupSealContextToExhaustedIdianFullUpdate()
	{
		var player = new Player
		{
			ObjectId = 1001,
			InventoryItems =
			[
				CreateItem(objectId: 10, itemId: 100, charge: 0, polishCharge: 100_000),
			],
		};

		var result = await EquipmentObserverBurnWorkflowService.ApplyObserverBurnsAsync(
			player,
			CreateItemTemplates(validEquipmentSlots: 0),
			EquipmentObserverBurnEvent.Attack,
			skillId: 0,
			itemRestrictionCleanups: CreateItemRestrictionCleanups());

		Assert.True(result.Changed);
		Assert.True(result.Persisted);
		Assert.Null(Assert.Single(player.InventoryItems).IdianStone);
		var packet = Assert.Single(result.Packets);
		AssertFullDecreasePacket(packet, objectId: 10, expectedCleanupSealFlag: 3);
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
	public async Task ApplyObserverBurnsAndSendPacketsAsync_SendsIncomingAttackedPacketsToDefender()
	{
		var player = new Player
		{
			ObjectId = 2002,
			InventoryItems =
			[
				CreateItem(objectId: 10, itemId: 100, charge: 100_050, polishCharge: 350_000),
			],
		};
		var registry = new CapturingConnectionRegistry();
		var fanout = new EquipmentObserverBurnFanoutService(registry);

		var result = await fanout.ApplyObserverBurnsAndSendPacketsAsync(
			player,
			CreateItemTemplates(),
			EquipmentObserverBurnEvent.Attacked,
			skillId: 0);

		Assert.True(result.Workflow.Changed);
		Assert.Equal(2, result.SentCount);
		Assert.Equal([2002, 2002], registry.SentPackets.Select(sent => sent.PlayerObjectId));
		var item = Assert.Single(player.InventoryItems);
		Assert.Equal(290_000, item.IdianStone?.PolishCharge);
		Assert.Equal(99_950, item.Charge);
		Assert.Collection(
			registry.SentPackets.Select(sent => sent.Packet),
			packet => AssertPolishChargePacket(packet, objectId: 10, polishCharge: 290_000),
			packet => AssertChargePacket(packet, objectId: 10, charge: 99_950));
	}

	[Fact]
	public async Task ApplyObserverBurnsAndSendPacketsAsync_SendsOnlyIdianForNonzeroIncomingSkillAttacked()
	{
		var player = new Player
		{
			ObjectId = 2002,
			InventoryItems =
			[
				CreateItem(objectId: 10, itemId: 100, charge: 100_050, polishCharge: 350_000),
			],
		};
		var registry = new CapturingConnectionRegistry();
		var fanout = new EquipmentObserverBurnFanoutService(registry);

		var result = await fanout.ApplyObserverBurnsAndSendPacketsAsync(
			player,
			CreateItemTemplates(),
			EquipmentObserverBurnEvent.Attacked,
			skillId: 2001);

		Assert.True(result.Workflow.Changed);
		Assert.Equal(1, result.SentCount);
		var sent = Assert.Single(registry.SentPackets);
		Assert.Equal(2002, sent.PlayerObjectId);
		var item = Assert.Single(player.InventoryItems);
		Assert.Equal(290_000, item.IdianStone?.PolishCharge);
		Assert.Equal(100_050, item.Charge);
		AssertPolishChargePacket(sent.Packet, objectId: 10, polishCharge: 290_000);
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

	[Fact]
	public async Task ApplyObserverBurnsAsync_ChargePersistenceFailureMarksEquipmentDirty()
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
			saveIdianPolishBurnAsync: (_, _, _) => Task.FromResult(true),
			saveItemChargeBurnAsync: (_, _, _) => Task.FromResult(false));

		Assert.True(result.Changed);
		Assert.False(result.Persisted);
		Assert.True(result.IdianPersisted);
		Assert.False(result.ChargePersisted);
		Assert.Equal(StoragePersistentState.UpdateRequired, player.EquipmentPersistentState);
		Assert.Equal(99_850, Assert.Single(player.InventoryItems).Charge);
	}

	[Fact]
	public async Task ApplyObserverBurnsAsync_ChargePersistenceSuccessLeavesEquipmentStateClean()
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
			saveIdianPolishBurnAsync: (_, _, _) => Task.FromResult(true),
			saveItemChargeBurnAsync: (_, _, _) => Task.FromResult(true));

		Assert.True(result.Changed);
		Assert.True(result.Persisted);
		Assert.Equal(StoragePersistentState.Updated, player.EquipmentPersistentState);
		Assert.Equal(99_850, Assert.Single(player.InventoryItems).Charge);
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

	private static void AssertFullDecreasePacket(GameServerPacket packet, int objectId, int expectedCleanupSealFlag)
	{
		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new PacketBuffer(payload);
		Assert.Equal(objectId, reader.ReadD());
		Assert.Equal(string.Empty, reader.ReadS());
		var blobSize = reader.ReadH();
		Assert.True(blobSize > 0);
		var blob = reader.ReadB(blobSize);
		AssertGeneralInfoCleanupSealFlag(blob, expectedItemMask: 1, expectedFlag: expectedCleanupSealFlag);
		Assert.Equal(SmInventoryUpdateItem.DecreaseItemUse, reader.ReadH());
		Assert.Equal(0, reader.Remaining);
	}

	private static void AssertGeneralInfoCleanupSealFlag(byte[] blob, int expectedItemMask, int expectedFlag)
	{
		using var reader = new PacketBuffer(blob);
		Assert.Equal(0x00, (int)reader.ReadC());
		Assert.Equal(expectedItemMask, reader.ReadH());
		Assert.Equal(1, reader.ReadQ());
		Assert.Equal(string.Empty, reader.ReadS());
		Assert.Equal(0, (int)reader.ReadC());
		reader.ReadD();
		reader.ReadD();
		reader.ReadD();
		Assert.Equal(expectedFlag, reader.ReadH());
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

	private static ItemTemplateTable CreateItemTemplates(long validEquipmentSlots = 3)
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
				validEquipmentSlots,
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

	private static ItemRestrictionCleanupTable CreateItemRestrictionCleanups()
	{
		return new ItemRestrictionCleanupTable(
		[
			new ItemRestrictionCleanupSummary(ItemId: 100, AccountWarehouse: 0, LegionWarehouse: 1),
		]);
	}

	private sealed class CapturingConnectionRegistry : IGameClientConnectionRegistry
	{
		public List<SentPacketRecord> SentPackets { get; } = [];

		public void RegisterPlayerConnection(int playerObjectId, GameServerConnection connection)
		{
		}

		public void UnregisterPlayerConnection(int playerObjectId, GameServerConnection connection)
		{
		}

		public bool TryGetOnlinePlayerByName(string playerName, out Player? player)
		{
			player = null;
			return false;
		}

		public void ForEachOnlinePlayer(Action<Player> action)
		{
		}

		public Task<bool> SendPacketToPlayerAsync(int playerObjectId, GameServerPacket packet)
		{
			SentPackets.Add(new SentPacketRecord(playerObjectId, packet));
			return Task.FromResult(true);
		}

		public Task<int> BroadcastToWorldAsync(GameServerPacket packet, Func<Player, bool>? filter = null)
		{
			return Task.FromResult(0);
		}

		public Task<int> BroadcastToVisiblePlayersAsync(
			WorldPosition sourcePosition,
			int sourceObjectId,
			GameServerPacket packet,
			bool includeSourcePlayer = false,
			Func<Player, bool>? filter = null)
		{
			return Task.FromResult(0);
		}

		public Task<int> RefreshHousingVisibilityAsync(
			IReadOnlyList<WorldHouse> houses,
			HousingTemplateTable? housingTemplates,
			int? playerObjectId = null)
		{
			return Task.FromResult(0);
		}

		public Task<int> RefreshNpcVisibilityAsync(IReadOnlyList<IWorldNpcObject> npcs, int? playerObjectId = null)
		{
			return Task.FromResult(0);
		}

		public Task<int> BroadcastHouseUpdateAsync(WorldHouse house, HousingTemplateTable? housingTemplates)
		{
			return Task.FromResult(0);
		}

		public Task<bool> NotifyMailReceivedAsync(int recipientObjectId, PlayerMail mail)
		{
			return Task.FromResult(false);
		}

		public Task<bool> NotifyBrokerSettledAsync(int sellerObjectId, long settledKinah)
		{
			return Task.FromResult(false);
		}
	}

	private sealed record SentPacketRecord(int PlayerObjectId, GameServerPacket Packet);
}
