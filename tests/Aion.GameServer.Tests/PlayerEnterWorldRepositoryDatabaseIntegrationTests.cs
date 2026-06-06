using System.Globalization;
using Aion.Commons.Database;
using Aion.GameServer.Data;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Legion;
using Aion.GameServer.Services;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class PlayerEnterWorldRepositoryDatabaseIntegrationTests
{
	private const int PlayerObjectId = 1001;

	[Fact]
	public async Task SavePlayerPetDopingBagAsync_WritesJavaCsvAgainstJavaSchema_WhenEnabled()
	{
		if (Environment.GetEnvironmentVariable("AION_GAMESERVER_DB_INTEGRATION") != "1")
			return;

		// Java source breadcrumbs: PlayerPetsDAO.saveDopingBag writes food, drink, then scroll slots to player_pets.dopings.
		InitializeDatabaseFactory();
		await InitializeSchemaAsync();
		await SeedPlayerAsync();
		await ExecuteNonQueryAsync(
			"""
			INSERT INTO player_pets (id, player_id, template_id, decoration, name)
			VALUES (7001, 1001, 900210, 188051001, 'Doping Mate')
			""");

		var repository = new MySqlPlayerEnterWorldRepository(
			new GameServerRuntimeContext(),
			NullLogger<MySqlPlayerEnterWorldRepository>.Instance);

		var saved = await repository.SavePlayerPetDopingBagAsync(
			PlayerObjectId,
			petObjectId: 7001,
			itemIds: [166000001, 162000001, 164000001, 0]);

		Assert.True(saved);
		Assert.Equal("166000001,162000001,164000001,0", await ExecuteScalarStringAsync("SELECT dopings FROM player_pets WHERE id = 7001"));
	}

	[Fact]
	public async Task SavePlayerLogoutAsync_WritesRetuningInventoryFieldsAgainstJavaSchema_WhenEnabled()
	{
		if (Environment.GetEnvironmentVariable("AION_GAMESERVER_DB_INTEGRATION") != "1")
			return;

		// Java source breadcrumbs: PlayerLeaveWorldService.leaveWorld -> PlayerService.storePlayer
		// -> InventoryDAO.store(player) -> InventoryDAO.UPDATE_QUERY.
		InitializeDatabaseFactory();
		await InitializeSchemaAsync();
		await SeedPlayerAsync();
		await SeedInventoryItemAsync(9401, itemId: 110100001, count: 1);

		var repository = new MySqlPlayerEnterWorldRepository(
			new GameServerRuntimeContext(),
			NullLogger<MySqlPlayerEnterWorldRepository>.Instance);
		var lastOnline = new DateTime(2026, 5, 30, 12, 34, 56, DateTimeKind.Local);
		var player = new Player
		{
			ObjectId = PlayerObjectId,
			Name = "PurifyIntegration",
			Exp = 1234,
			RecoverableExp = 56,
			Dp = 78,
			Position = new WorldPosition(210010000, 11, 22, 33, 44),
			InventoryItems =
			[
				new InventoryItem
				{
					ObjectId = 9401,
					ItemId = 110100001,
					Count = 1,
					Color = 0x123456,
					ColorExpires = 789,
					Creator = "retune-test",
					ExpireTime = 456,
					ActivationCount = 2,
					OwnerId = PlayerObjectId,
					IsEquipped = false,
					IsSoulBound = true,
					Slot = 0,
					Location = 0,
					Enchant = 1,
					EnchantBonus = 7,
					ItemSkin = 110100099,
					FusionedItem = 0,
					OptionalSocket = 4,
					OptionalFusionSocket = 0,
					Charge = 12,
					TuneCount = 3,
					RandomBonus = 99,
					FusionRandomBonus = 0,
					Tempering = 5,
					PackCount = 1,
					IsAmplified = true,
					BuffSkill = 321,
					RandomPlumeBonus = 8,
				},
			],
		};

		var saved = await repository.SavePlayerLogoutAsync(player, lastOnline);

		Assert.True(saved);
		Assert.Equal(7, await ExecuteScalarLongAsync("SELECT enchant_bonus FROM inventory WHERE item_unique_id = 9401"));
		Assert.Equal(4, await ExecuteScalarLongAsync("SELECT optional_socket FROM inventory WHERE item_unique_id = 9401"));
		Assert.Equal(3, await ExecuteScalarLongAsync("SELECT tune_count FROM inventory WHERE item_unique_id = 9401"));
		Assert.Equal(99, await ExecuteScalarLongAsync("SELECT rnd_bonus FROM inventory WHERE item_unique_id = 9401"));
		Assert.Equal(1, await ExecuteScalarLongAsync("SELECT is_soul_bound FROM inventory WHERE item_unique_id = 9401"));
		Assert.Equal(321, await ExecuteScalarLongAsync("SELECT buff_skill FROM inventory WHERE item_unique_id = 9401"));
		Assert.Equal(8, await ExecuteScalarLongAsync("SELECT rnd_plume_bonus FROM inventory WHERE item_unique_id = 9401"));
		Assert.Equal(1234, await ExecuteScalarLongAsync("SELECT exp FROM players WHERE id = 1001"));
		Assert.Equal(0, await ExecuteScalarLongAsync("SELECT online FROM players WHERE id = 1001"));
	}

	[Fact]
	public async Task SavePlayerLogoutAsync_DeletesTrackedInventoryRowsAgainstJavaSchema_WhenEnabled()
	{
		if (Environment.GetEnvironmentVariable("AION_GAMESERVER_DB_INTEGRATION") != "1")
			return;

		// Java source breadcrumbs: PlayerLeaveWorldService.leaveWorld -> PlayerService.storePlayer
		// -> InventoryDAO.store(player) -> player.getDirtyItemsToUpdate() including Storage.deletedItems.
		InitializeDatabaseFactory();
		await InitializeSchemaAsync();
		await SeedPlayerAsync();
		await SeedInventoryItemAsync(9501, itemId: 110100001, count: 1);

		var repository = new MySqlPlayerEnterWorldRepository(
			new GameServerRuntimeContext(),
			NullLogger<MySqlPlayerEnterWorldRepository>.Instance);
		var player = new Player
		{
			ObjectId = PlayerObjectId,
			Name = "PurifyIntegration",
			Position = new WorldPosition(210010000, 11, 22, 33, 44),
			InventoryItems = Array.Empty<InventoryItem>(),
		};
		player.TrackDeletedItem(new InventoryItem
		{
			ObjectId = 9501,
			ItemId = 110100001,
			Count = 1,
			OwnerId = PlayerObjectId,
			Location = 0,
			PersistentState = InventoryItemPersistentState.Updated,
		});

		var saved = await repository.SavePlayerLogoutAsync(player, new DateTime(2026, 5, 30, 13, 0, 0, DateTimeKind.Local));

		Assert.True(saved);
		Assert.Equal(0, await ExecuteScalarLongAsync("SELECT COUNT(*) FROM inventory WHERE item_unique_id = 9501"));
		Assert.Empty(player.DeletedInventoryItems);
	}

	[Fact]
	public async Task SavePlayerLogoutAsync_WritesAllCurrentRowsFromDirtyInventoryStorageAgainstJavaSchema_WhenEnabled()
	{
		if (Environment.GetEnvironmentVariable("AION_GAMESERVER_DB_INTEGRATION") != "1")
			return;

		// Java source breadcrumbs: Player.getDirtyItemsToUpdate adds storage.getItemsWithKinah()
		// for UPDATE_REQUIRED storages, not just the individually dirty item rows.
		InitializeDatabaseFactory();
		await InitializeSchemaAsync();
		await SeedPlayerAsync();
		await SeedInventoryItemAsync(9601, itemId: 110100001, count: 1, enchant: 0);
		await SeedInventoryItemAsync(9602, itemId: 110100002, count: 1, enchant: 0);

		var repository = new MySqlPlayerEnterWorldRepository(
			new GameServerRuntimeContext(),
			NullLogger<MySqlPlayerEnterWorldRepository>.Instance);
		var player = new Player
		{
			ObjectId = PlayerObjectId,
			Name = "PurifyIntegration",
			Position = new WorldPosition(210010000, 11, 22, 33, 44),
			InventoryItems =
			[
				new InventoryItem
				{
					ObjectId = 9601,
					ItemId = 110100001,
					Count = 1,
					OwnerId = PlayerObjectId,
					Location = 0,
					Enchant = 7,
					PersistentState = InventoryItemPersistentState.UpdateRequired,
				},
				new InventoryItem
				{
					ObjectId = 9602,
					ItemId = 110100002,
					Count = 1,
					OwnerId = PlayerObjectId,
					Location = 0,
					Enchant = 9,
					PersistentState = InventoryItemPersistentState.Updated,
				},
			],
		};

		var saved = await repository.SavePlayerLogoutAsync(player, new DateTime(2026, 5, 30, 13, 15, 0, DateTimeKind.Local));

		Assert.True(saved);
		Assert.Equal(7, await ExecuteScalarLongAsync("SELECT enchant FROM inventory WHERE item_unique_id = 9601"));
		Assert.Equal(9, await ExecuteScalarLongAsync("SELECT enchant FROM inventory WHERE item_unique_id = 9602"));
	}

	[Fact]
	public async Task SavePlayerLogoutAsync_PersistsLoadedLegionWarehouseRowsLikeJava_WhenEnabled()
	{
		if (Environment.GetEnvironmentVariable("AION_GAMESERVER_DB_INTEGRATION") != "1")
			return;

		// Java source breadcrumbs: InventoryDAO.store(player) recomputes item_owner through
		// getItemOwnerId, so LEGION_WAREHOUSE rows are written under the player's legion id.
		InitializeDatabaseFactory();
		await InitializeSchemaAsync();
		await SeedPlayerAsync();
		await SeedInventoryItemAsync(9651, itemId: 110100001, count: 1, ownerId: PlayerObjectId, location: 3);

		var repository = new MySqlPlayerEnterWorldRepository(
			new GameServerRuntimeContext(),
			NullLogger<MySqlPlayerEnterWorldRepository>.Instance);
		var player = new Player
		{
			ObjectId = PlayerObjectId,
			AccountId = 1,
			LegionId = 501,
			Name = "LegionLogoutIntegration",
			Position = new WorldPosition(210010000, 11, 22, 33, 44),
			InventoryItems =
			[
				new InventoryItem
				{
					ObjectId = 9651,
					ItemId = 110100001,
					Count = 9,
					OwnerId = PlayerObjectId,
					Location = 3,
					Enchant = 11,
					PersistentState = InventoryItemPersistentState.UpdateRequired,
				},
			],
		};

		var saved = await repository.SavePlayerLogoutAsync(player, new DateTime(2026, 5, 30, 13, 20, 0, DateTimeKind.Local));

		Assert.True(saved);
		Assert.Equal(501, await ExecuteScalarLongAsync("SELECT item_owner FROM inventory WHERE item_unique_id = 9651"));
		Assert.Equal(3, await ExecuteScalarLongAsync("SELECT item_location FROM inventory WHERE item_unique_id = 9651"));
		Assert.Equal(9, await ExecuteScalarLongAsync("SELECT item_count FROM inventory WHERE item_unique_id = 9651"));
		Assert.Equal(11, await ExecuteScalarLongAsync("SELECT enchant FROM inventory WHERE item_unique_id = 9651"));
		Assert.Equal(501, player.InventoryItems.Single().OwnerId);
	}

	[Fact]
	public async Task SavePeriodicPlayerItemsAsync_ReplacesCurrentItemStonesEvenWhenItemRowIsCleanAgainstJavaSchema_WhenEnabled()
	{
		if (Environment.GetEnvironmentVariable("AION_GAMESERVER_DB_INTEGRATION") != "1")
			return;

		// Java source breadcrumbs: PlayerEnterWorldService.ItemUpdateTask.run calls
		// InventoryDAO.store(player), then ItemStoneListDAO.save(player.getAllItems()).
		InitializeDatabaseFactory();
		await InitializeSchemaAsync();
		await SeedPlayerAsync();
		await SeedInventoryItemAsync(9701, itemId: 110100001, count: 1);
		await SeedItemStoneAsync(9701, itemId: 167000001, slot: 0, category: 0);
		await SeedItemStoneAsync(9701, itemId: 168000001, slot: 0, category: 1, procCount: 7);

		var repository = new MySqlPlayerEnterWorldRepository(
			new GameServerRuntimeContext(),
			NullLogger<MySqlPlayerEnterWorldRepository>.Instance);
		var player = new Player
		{
			ObjectId = PlayerObjectId,
			Name = "PeriodicStoneIntegration",
			InventoryItems =
			[
				new InventoryItem
				{
					ObjectId = 9701,
					ItemId = 110100001,
					Count = 1,
					OwnerId = PlayerObjectId,
					Location = 0,
					PersistentState = InventoryItemPersistentState.Updated,
					ManaStones =
					[
						new ItemStoneSocket(167000010, 1),
					],
					FusionStones =
					[
						new ItemStoneSocket(167100010, 2),
					],
					Godstone = new PlayerGodstone(168000010, ProcCount: 44),
					IdianStone = new PlayerIdianStone(169000010, PolishNumber: 9, PolishCharge: 250),
				},
			],
		};

		var saved = await repository.SavePeriodicPlayerItemsAsync(player);

		Assert.True(saved);
		Assert.Equal(4, await ExecuteScalarLongAsync("SELECT COUNT(*) FROM item_stones WHERE item_unique_id = 9701"));
		Assert.Equal(0, await ExecuteScalarLongAsync("SELECT COUNT(*) FROM item_stones WHERE item_unique_id = 9701 AND item_id IN (167000001, 168000001)"));
		Assert.Equal(167000010, await ExecuteScalarLongAsync("SELECT item_id FROM item_stones WHERE item_unique_id = 9701 AND category = 0 AND slot = 1"));
		Assert.Equal(168000010, await ExecuteScalarLongAsync("SELECT item_id FROM item_stones WHERE item_unique_id = 9701 AND category = 1 AND slot = 0"));
		Assert.Equal(44, await ExecuteScalarLongAsync("SELECT proc_count FROM item_stones WHERE item_unique_id = 9701 AND category = 1 AND slot = 0"));
		Assert.Equal(167100010, await ExecuteScalarLongAsync("SELECT item_id FROM item_stones WHERE item_unique_id = 9701 AND category = 2 AND slot = 2"));
		Assert.Equal(169000010, await ExecuteScalarLongAsync("SELECT item_id FROM item_stones WHERE item_unique_id = 9701 AND category = 3 AND slot = 0"));
		Assert.Equal(9, await ExecuteScalarLongAsync("SELECT polishNumber FROM item_stones WHERE item_unique_id = 9701 AND category = 3 AND slot = 0"));
		Assert.Equal(250, await ExecuteScalarLongAsync("SELECT polishCharge FROM item_stones WHERE item_unique_id = 9701 AND category = 3 AND slot = 0"));
		Assert.Equal(StoragePersistentState.Updated, player.InventoryStoragePersistentState);
	}

	[Fact]
	public async Task SavePeriodicPlayerItemsAsync_PersistsLoadedLegionWarehouseRowsLikeJava_WhenEnabled()
	{
		if (Environment.GetEnvironmentVariable("AION_GAMESERVER_DB_INTEGRATION") != "1")
			return;

		// Java source breadcrumbs: PlayerEnterWorldService.ItemUpdateTask.run calls
		// InventoryDAO.store(player), which maps LEGION_WAREHOUSE dirty rows to legion id.
		InitializeDatabaseFactory();
		await InitializeSchemaAsync();
		await SeedPlayerAsync();
		await SeedInventoryItemAsync(9751, itemId: 110100001, count: 1, ownerId: PlayerObjectId, location: 3);

		var repository = new MySqlPlayerEnterWorldRepository(
			new GameServerRuntimeContext(),
			NullLogger<MySqlPlayerEnterWorldRepository>.Instance);
		var player = new Player
		{
			ObjectId = PlayerObjectId,
			AccountId = 1,
			LegionId = 501,
			Name = "PeriodicLegionWarehouseIntegration",
			InventoryItems =
			[
				new InventoryItem
				{
					ObjectId = 9751,
					ItemId = 110100001,
					Count = 12,
					OwnerId = PlayerObjectId,
					Location = 3,
					Enchant = 13,
					PersistentState = InventoryItemPersistentState.UpdateRequired,
				},
			],
		};

		var saved = await repository.SavePeriodicPlayerItemsAsync(player);

		Assert.True(saved);
		Assert.Equal(501, await ExecuteScalarLongAsync("SELECT item_owner FROM inventory WHERE item_unique_id = 9751"));
		Assert.Equal(3, await ExecuteScalarLongAsync("SELECT item_location FROM inventory WHERE item_unique_id = 9751"));
		Assert.Equal(12, await ExecuteScalarLongAsync("SELECT item_count FROM inventory WHERE item_unique_id = 9751"));
		Assert.Equal(13, await ExecuteScalarLongAsync("SELECT enchant FROM inventory WHERE item_unique_id = 9751"));
		Assert.Equal(501, player.InventoryItems.Single().OwnerId);
		Assert.Equal(StoragePersistentState.Updated, player.InventoryStoragePersistentState);
	}

	[Fact]
	public async Task SavePeriodicPlayerItemsAsync_DeletesTrackedLegionWarehouseRowsLikeJava_WhenEnabled()
	{
		if (Environment.GetEnvironmentVariable("AION_GAMESERVER_DB_INTEGRATION") != "1")
			return;

		// Java source breadcrumbs: LegionStorageProxy.delete -> Storage.delete marks the
		// location-3 item deleted, and InventoryDAO.store deletes it by item_unique_id.
		InitializeDatabaseFactory();
		await InitializeSchemaAsync();
		await SeedPlayerAsync();
		await SeedInventoryItemAsync(9761, itemId: 110100001, count: 1, ownerId: 501, location: 3);

		var repository = new MySqlPlayerEnterWorldRepository(
			new GameServerRuntimeContext(),
			NullLogger<MySqlPlayerEnterWorldRepository>.Instance);
		var player = new Player
		{
			ObjectId = PlayerObjectId,
			AccountId = 1,
			LegionId = 501,
			Name = "PeriodicLegionDeleteIntegration",
			InventoryItems =
			[
				new InventoryItem
				{
					ObjectId = 9761,
					ItemId = 110100001,
					Count = 1,
					OwnerId = 501,
					Location = 3,
					PersistentState = InventoryItemPersistentState.Updated,
				},
			],
		};
		player.TrackDeletedItem(player.InventoryItems.Single());

		var saved = await repository.SavePeriodicPlayerItemsAsync(player);

		Assert.True(saved);
		Assert.Equal(0, await ExecuteScalarLongAsync("SELECT COUNT(*) FROM inventory WHERE item_unique_id = 9761"));
		Assert.Empty(player.DeletedLegionWarehouseItems);
		Assert.Equal(StoragePersistentState.Updated, player.LegionWarehouseStoragePersistentState);
	}

	[Fact]
	public async Task SavePeriodicPlayerGeneralAsync_WritesAbyssRankWithoutChangingRankingPositionAgainstJavaSchema_WhenEnabled()
	{
		if (Environment.GetEnvironmentVariable("AION_GAMESERVER_DB_INTEGRATION") != "1")
			return;

		// Java source breadcrumbs: PlayerEnterWorldService.GeneralUpdateTask.run calls
		// AbyssRankDAO.storeAbyssRank(player), whose INSERT/UPDATE columns exclude rank_pos.
		InitializeDatabaseFactory();
		await InitializeSchemaAsync();
		await SeedPlayerAsync();
		await ExecuteNonQueryAsync(
			"""
			INSERT INTO abyss_rank (
				player_id, daily_ap, weekly_ap, ap, `rank`, max_rank, rank_pos, old_rank_pos,
				daily_kill, weekly_kill, all_kill, last_kill, last_ap, last_update,
				daily_gp, weekly_gp, gp, last_gp)
			VALUES (1001, 1, 2, 3, 1, 1, 77, 55, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13)
			""");

		var repository = new MySqlPlayerEnterWorldRepository(
			new GameServerRuntimeContext(),
			NullLogger<MySqlPlayerEnterWorldRepository>.Instance);
		var player = new Player
		{
			ObjectId = PlayerObjectId,
			Name = "PeriodicAbyssIntegration",
			Position = new WorldPosition(210010000, 11, 22, 33, 44),
			AbyssRank = new PlayerAbyssRank(
				DailyAp: 101,
				WeeklyAp: 202,
				Ap: 303,
				DailyGp: 404,
				WeeklyGp: 505,
				Gp: 606,
				Rank: 7,
				DailyKill: 8,
				WeeklyKill: 9,
				AllKill: 10,
				MaxRank: 11,
				LastKill: 12,
				LastAp: 13,
				LastGp: 14,
				RankingListPosition: 99),
		};

		var saved = await repository.SavePeriodicPlayerGeneralAsync(player);

		Assert.True(saved);
		Assert.Equal(101, await ExecuteScalarLongAsync("SELECT daily_ap FROM abyss_rank WHERE player_id = 1001"));
		Assert.Equal(202, await ExecuteScalarLongAsync("SELECT weekly_ap FROM abyss_rank WHERE player_id = 1001"));
		Assert.Equal(303, await ExecuteScalarLongAsync("SELECT ap FROM abyss_rank WHERE player_id = 1001"));
		Assert.Equal(7, await ExecuteScalarLongAsync("SELECT `rank` FROM abyss_rank WHERE player_id = 1001"));
		Assert.Equal(8, await ExecuteScalarLongAsync("SELECT daily_kill FROM abyss_rank WHERE player_id = 1001"));
		Assert.Equal(9, await ExecuteScalarLongAsync("SELECT weekly_kill FROM abyss_rank WHERE player_id = 1001"));
		Assert.Equal(10, await ExecuteScalarLongAsync("SELECT all_kill FROM abyss_rank WHERE player_id = 1001"));
		Assert.Equal(11, await ExecuteScalarLongAsync("SELECT max_rank FROM abyss_rank WHERE player_id = 1001"));
		Assert.Equal(12, await ExecuteScalarLongAsync("SELECT last_kill FROM abyss_rank WHERE player_id = 1001"));
		Assert.Equal(13, await ExecuteScalarLongAsync("SELECT last_ap FROM abyss_rank WHERE player_id = 1001"));
		Assert.Equal(404, await ExecuteScalarLongAsync("SELECT daily_gp FROM abyss_rank WHERE player_id = 1001"));
		Assert.Equal(505, await ExecuteScalarLongAsync("SELECT weekly_gp FROM abyss_rank WHERE player_id = 1001"));
		Assert.Equal(606, await ExecuteScalarLongAsync("SELECT gp FROM abyss_rank WHERE player_id = 1001"));
		Assert.Equal(14, await ExecuteScalarLongAsync("SELECT last_gp FROM abyss_rank WHERE player_id = 1001"));
		Assert.Equal(77, await ExecuteScalarLongAsync("SELECT rank_pos FROM abyss_rank WHERE player_id = 1001"));
		Assert.Equal(55, await ExecuteScalarLongAsync("SELECT old_rank_pos FROM abyss_rank WHERE player_id = 1001"));
	}

	[Fact]
	public async Task SavePeriodicPlayerGeneralAsync_ReplacesLiveSkillListAgainstJavaSchema_WhenEnabled()
	{
		if (Environment.GetEnvironmentVariable("AION_GAMESERVER_DB_INTEGRATION") != "1")
			return;

		// Java source breadcrumbs: PlayerEnterWorldService.GeneralUpdateTask.run calls
		// PlayerSkillListDAO.storeSkills(player), whose DB shape is player_id/skill_id/skill_level.
		InitializeDatabaseFactory();
		await InitializeSchemaAsync();
		await SeedPlayerAsync();
		await ExecuteNonQueryAsync(
			"""
			INSERT INTO player_skills (player_id, skill_id, skill_level)
			VALUES (1001, 37, 1), (1001, 99999, 1)
			""");

		var repository = new MySqlPlayerEnterWorldRepository(
			new GameServerRuntimeContext(),
			NullLogger<MySqlPlayerEnterWorldRepository>.Instance);
		var player = new Player
		{
			ObjectId = PlayerObjectId,
			Name = "PeriodicSkillIntegration",
			Position = new WorldPosition(210010000, 11, 22, 33, 44),
			Skills =
			[
				new PlayerSkill { SkillId = 37, SkillLevel = 2 },
				new PlayerSkill { SkillId = 43, SkillLevel = 1 },
			],
		};

		var saved = await repository.SavePeriodicPlayerGeneralAsync(player);

		Assert.True(saved);
		Assert.Equal(2, await ExecuteScalarLongAsync("SELECT COUNT(*) FROM player_skills WHERE player_id = 1001"));
		Assert.Equal(2, await ExecuteScalarLongAsync("SELECT skill_level FROM player_skills WHERE player_id = 1001 AND skill_id = 37"));
		Assert.Equal(1, await ExecuteScalarLongAsync("SELECT skill_level FROM player_skills WHERE player_id = 1001 AND skill_id = 43"));
		Assert.Equal(0, await ExecuteScalarLongAsync("SELECT COUNT(*) FROM player_skills WHERE player_id = 1001 AND skill_id = 99999"));
	}

	[Fact]
	public async Task SavePeriodicPlayerGeneralAsync_ReplacesLiveQuestListAgainstJavaSchema_WhenEnabled()
	{
		if (Environment.GetEnvironmentVariable("AION_GAMESERVER_DB_INTEGRATION") != "1")
			return;

		// Java source breadcrumbs: PlayerEnterWorldService.GeneralUpdateTask.run calls
		// PlayerQuestListDAO.store(player), whose DB shape includes nullable repeat/reward/complete columns.
		InitializeDatabaseFactory();
		await InitializeSchemaAsync();
		await SeedPlayerAsync();
		await ExecuteNonQueryAsync(
			"""
			INSERT INTO player_quests (
				player_id, quest_id, status, quest_vars, flags, complete_count, next_repeat_time, reward, complete_time
			)
			VALUES
				(1001, 5001, 'START', 1, 0, 0, NULL, NULL, NULL),
				(1001, 9999, 'START', 9, 9, 0, NULL, NULL, NULL)
			""");

		var repository = new MySqlPlayerEnterWorldRepository(
			new GameServerRuntimeContext(),
			NullLogger<MySqlPlayerEnterWorldRepository>.Instance);
		var nextRepeatTime = new DateTimeOffset(2026, 6, 1, 9, 30, 0, TimeSpan.Zero);
		var completeTime = new DateTimeOffset(2026, 6, 1, 10, 15, 0, TimeSpan.Zero);
		var player = new Player
		{
			ObjectId = PlayerObjectId,
			Name = "PeriodicQuestIntegration",
			Position = new WorldPosition(210010000, 11, 22, 33, 44),
			Quests =
			[
				new PlayerQuestState(
					QuestId: 5001,
					Status: "REWARD",
					QuestVars: 0x12345,
					Flags: 2,
					CompleteCount: 3,
					RewardGroup: 4,
					NextRepeatTime: nextRepeatTime,
					CompleteTime: completeTime),
				new PlayerQuestState(
					QuestId: 5002,
					Status: "COMPLETE",
					QuestVars: 0,
					Flags: 0,
					CompleteCount: 1),
			],
		};

		var saved = await repository.SavePeriodicPlayerGeneralAsync(player);

		Assert.True(saved);
		Assert.Equal(2, await ExecuteScalarLongAsync("SELECT COUNT(*) FROM player_quests WHERE player_id = 1001"));
		Assert.Equal(0, await ExecuteScalarLongAsync("SELECT COUNT(*) FROM player_quests WHERE player_id = 1001 AND quest_id = 9999"));
		Assert.Equal("REWARD", await ExecuteScalarStringAsync("SELECT status FROM player_quests WHERE player_id = 1001 AND quest_id = 5001"));
		Assert.Equal(0x12345, await ExecuteScalarLongAsync("SELECT quest_vars FROM player_quests WHERE player_id = 1001 AND quest_id = 5001"));
		Assert.Equal(2, await ExecuteScalarLongAsync("SELECT flags FROM player_quests WHERE player_id = 1001 AND quest_id = 5001"));
		Assert.Equal(3, await ExecuteScalarLongAsync("SELECT complete_count FROM player_quests WHERE player_id = 1001 AND quest_id = 5001"));
		Assert.Equal(4, await ExecuteScalarLongAsync("SELECT reward FROM player_quests WHERE player_id = 1001 AND quest_id = 5001"));
		Assert.Equal("COMPLETE", await ExecuteScalarStringAsync("SELECT status FROM player_quests WHERE player_id = 1001 AND quest_id = 5002"));
		Assert.Equal(1, await ExecuteScalarLongAsync("SELECT complete_count FROM player_quests WHERE player_id = 1001 AND quest_id = 5002"));
		Assert.Equal(1, await ExecuteScalarLongAsync("SELECT COUNT(*) FROM player_quests WHERE player_id = 1001 AND quest_id = 5002 AND reward IS NULL AND next_repeat_time IS NULL AND complete_time IS NULL"));
	}

	[Fact]
	public async Task SavePeriodicPlayerGeneralAsync_UpsertsLiveHousesAgainstJavaSchema_WhenEnabled()
	{
		if (Environment.GetEnvironmentVariable("AION_GAMESERVER_DB_INTEGRATION") != "1")
			return;

		// Java source breadcrumbs: PlayerEnterWorldService.GeneralUpdateTask.run calls
		// player.getHouses().forEach(House::save), whose HousesDAO.storeHouse columns
		// are id/address/building_id/player_id/acquire_time/settings/next_pay/sign_notice.
		InitializeDatabaseFactory();
		await InitializeSchemaAsync();
		await SeedPlayerAsync();
		await ExecuteNonQueryAsync(
			"""
			INSERT INTO houses (id, address, building_id, player_id, acquire_time, settings, next_pay, sign_notice)
			VALUES (8101, 710001, 9001, 1001, '2026-01-01 01:02:03', 1, '2026-01-08 01:02:03', 'old notice')
			""");

		var repository = new MySqlPlayerEnterWorldRepository(
			new GameServerRuntimeContext(),
			NullLogger<MySqlPlayerEnterWorldRepository>.Instance);
		var player = new Player
		{
			ObjectId = PlayerObjectId,
			Name = "PeriodicHouseIntegration",
			Position = new WorldPosition(210010000, 11, 22, 33, 44),
			Houses =
			[
				new PlayerHouse(
					ObjectId: 8101,
					AddressId: 710001,
					BuildingId: 9101,
					AcquiredTime: new DateTime(2026, 2, 3, 4, 5, 6, DateTimeKind.Local),
					NextPay: new DateTime(2026, 2, 10, 4, 5, 6, DateTimeKind.Local),
					IsInactive: false,
					DoorState: PlayerHouse.DoorClosedExceptFriends,
					ShowOwnerName: false,
					SignNotice: "fresh notice"),
				new PlayerHouse(
					ObjectId: 8102,
					AddressId: 710002,
					BuildingId: 9102,
					AcquiredTime: null,
					NextPay: null,
					IsInactive: true,
					DoorState: PlayerHouse.DoorClosed,
					ShowOwnerName: true),
			],
		};

		var saved = await repository.SavePeriodicPlayerGeneralAsync(player);

		Assert.True(saved);
		Assert.Equal(2, await ExecuteScalarLongAsync("SELECT COUNT(*) FROM houses WHERE player_id = 1001"));
		Assert.Equal(9101, await ExecuteScalarLongAsync("SELECT building_id FROM houses WHERE id = 8101"));
		Assert.Equal(PlayerHouse.CreateSettings(PlayerHouse.DoorClosedExceptFriends, showOwnerName: false), await ExecuteScalarLongAsync("SELECT settings FROM houses WHERE id = 8101"));
		Assert.Equal("fresh notice", await ExecuteScalarStringAsync("SELECT sign_notice FROM houses WHERE id = 8101"));
		Assert.Equal(9102, await ExecuteScalarLongAsync("SELECT building_id FROM houses WHERE id = 8102"));
		Assert.Equal(710002, await ExecuteScalarLongAsync("SELECT address FROM houses WHERE id = 8102"));
		Assert.Equal(PlayerHouse.CreateSettings(PlayerHouse.DoorClosed, showOwnerName: true), await ExecuteScalarLongAsync("SELECT settings FROM houses WHERE id = 8102"));
		Assert.Equal(1, await ExecuteScalarLongAsync("SELECT COUNT(*) FROM houses WHERE id = 8102 AND acquire_time IS NULL AND next_pay IS NULL AND sign_notice IS NULL"));
	}

	[Fact]
	public async Task SavePlayerCraftCooldownsAsync_ReplacesRowsAndKeepsOnlyActiveCooldownsAgainstJavaSchema_WhenEnabled()
	{
		if (Environment.GetEnvironmentVariable("AION_GAMESERVER_DB_INTEGRATION") != "1")
			return;

		// Java source breadcrumbs: CraftCooldownsDAO.storeCraftCooldowns deletes all rows,
		// skips cooldowns older than System.currentTimeMillis(), and inserts active rows.
		InitializeDatabaseFactory();
		await InitializeSchemaAsync();
		await SeedPlayerAsync();
		await ExecuteNonQueryAsync(
			"""
			INSERT INTO craft_cooldowns (player_id, delay_id, reuse_time)
			VALUES (1001, 10, 10000), (1001, 11, 11000)
			""");

		var repository = new MySqlPlayerEnterWorldRepository(
			new GameServerRuntimeContext(),
			NullLogger<MySqlPlayerEnterWorldRepository>.Instance);

		var saved = await repository.SavePlayerCraftCooldownsAsync(
			PlayerObjectId,
			new Dictionary<int, long>
			{
				[77] = 20_000,
				[78] = 500,
			},
			nowMillis: 1_000);

		Assert.True(saved);
		Assert.Equal(1, await ExecuteScalarLongAsync("SELECT COUNT(*) FROM craft_cooldowns WHERE player_id = 1001"));
		Assert.Equal(20_000, await ExecuteScalarLongAsync("SELECT reuse_time FROM craft_cooldowns WHERE player_id = 1001 AND delay_id = 77"));
		Assert.Equal(0, await ExecuteScalarLongAsync("SELECT COUNT(*) FROM craft_cooldowns WHERE player_id = 1001 AND delay_id IN (10, 11, 78)"));
	}

	[Fact]
	public async Task SavePlayerLogoutAsync_WritesCraftCooldownsAfterPortalCooldownsAgainstJavaSchema_WhenEnabled()
	{
		if (Environment.GetEnvironmentVariable("AION_GAMESERVER_DB_INTEGRATION") != "1")
			return;

		// Java source breadcrumbs: PlayerService.storePlayer calls PortalCooldownsDAO,
		// CraftCooldownsDAO, then HouseObjectCooldownsDAO during logout persistence.
		InitializeDatabaseFactory();
		await InitializeSchemaAsync();
		await SeedPlayerAsync();
		await ExecuteNonQueryAsync(
			"""
			INSERT INTO craft_cooldowns (player_id, delay_id, reuse_time)
			VALUES (1001, 10, 10000), (1001, 11, 11000)
			""");

		var repository = new MySqlPlayerEnterWorldRepository(
			new GameServerRuntimeContext(),
			NullLogger<MySqlPlayerEnterWorldRepository>.Instance);
		var player = new Player
		{
			ObjectId = PlayerObjectId,
			Name = "PurifyIntegration",
			Position = new WorldPosition(210010000, 11, 22, 33, 44),
			PortalCooldowns = new Dictionary<int, PlayerPortalCooldown>
			{
				[300030000] = new PlayerPortalCooldown(300030000, 4_102_444_800_000, EntryCount: 2),
			},
			CraftCooldowns = new Dictionary<int, long>
			{
				[77] = 4_102_444_800_000,
				[78] = 500,
			},
		};

		var saved = await repository.SavePlayerLogoutAsync(player, new DateTime(2026, 5, 30, 13, 30, 0, DateTimeKind.Local));

		Assert.True(saved);
		Assert.Equal(1, await ExecuteScalarLongAsync("SELECT COUNT(*) FROM portal_cooldowns WHERE player_id = 1001"));
		Assert.Equal(2, await ExecuteScalarLongAsync("SELECT entry_count FROM portal_cooldowns WHERE player_id = 1001 AND world_id = 300030000"));
		Assert.Equal(1, await ExecuteScalarLongAsync("SELECT COUNT(*) FROM craft_cooldowns WHERE player_id = 1001"));
		Assert.Equal(4_102_444_800_000, await ExecuteScalarLongAsync("SELECT reuse_time FROM craft_cooldowns WHERE player_id = 1001 AND delay_id = 77"));
		Assert.Equal(0, await ExecuteScalarLongAsync("SELECT COUNT(*) FROM craft_cooldowns WHERE player_id = 1001 AND delay_id IN (10, 11, 78)"));
	}

	[Fact]
	public async Task SaveItemPurificationMutation_WritesInventoryStonesAndAbyssRankAgainstJavaSchema_WhenEnabled()
	{
		if (Environment.GetEnvironmentVariable("AION_GAMESERVER_DB_INTEGRATION") != "1")
			return;

		// Java source breadcrumbs: InventoryDAO, ItemStoneListDAO, AbyssRankDAO, and game-server/sql/aion_gs.sql.
		InitializeDatabaseFactory();
		await InitializeSchemaAsync();
		await SeedPlayerAsync();
		await SeedInventoryItemAsync(9001, itemId: 186000001, count: 5);
		await SeedInventoryItemAsync(9002, itemId: 186000002, count: 1);
		await SeedInventoryItemAsync(9003, itemId: 100000401, count: 1, enchant: 15);
		await SeedItemStoneAsync(9002, itemId: 167000001, slot: 0, category: 0);
		await SeedItemStoneAsync(9003, itemId: 168000001, slot: 0, category: 1, procCount: 7);

		var repository = new MySqlPlayerEnterWorldRepository(
			new GameServerRuntimeContext(),
			NullLogger<MySqlPlayerEnterWorldRepository>.Instance);
		var targetItem = new InventoryItem
		{
			ObjectId = 9101,
			ItemId = 100000402,
			Count = 1,
			OwnerId = PlayerObjectId,
			Location = 0,
			Enchant = 10,
			EnchantBonus = 3,
			ItemSkin = 100000401,
			OptionalSocket = 2,
			OptionalFusionSocket = 1,
			TuneCount = 4,
			RandomBonus = 55,
			FusionRandomBonus = 66,
			Tempering = 7,
			PackCount = 1,
			IsAmplified = true,
			BuffSkill = 1234,
			RandomPlumeBonus = 8,
			ManaStones =
			[
				new ItemStoneSocket(167000010, 0),
			],
			FusionStones =
			[
				new ItemStoneSocket(167100010, 1),
			],
			Godstone = new PlayerGodstone(168000010, ProcCount: 44),
			IdianStone = new PlayerIdianStone(169000010, PolishNumber: 9, PolishCharge: 250),
		};
		var updatedRank = new PlayerAbyssRank(
			DailyAp: 10,
			WeeklyAp: 20,
			Ap: 700,
			DailyGp: 30,
			WeeklyGp: 40,
			Gp: 50,
			Rank: 3,
			DailyKill: 1,
			WeeklyKill: 2,
			AllKill: 3,
			MaxRank: 4,
			LastKill: 5,
			LastAp: 6,
			LastGp: 7,
			RankingListPosition: 8);

		var saved = await repository.SaveItemPurificationMutationAsync(
			PlayerObjectId,
			materialItemUpdates:
			[
				new InventoryItem { ObjectId = 9001, Count = 2 },
			],
			deletedMaterialItemObjectIds:
			[
				9002,
			],
			baseItemUpdate: null,
			deletedBaseItemObjectId: 9003,
			updatedTargetItems: [],
			addedTargetItems:
			[
				targetItem,
			],
			abyssRank: updatedRank);

		Assert.True(saved);
		Assert.Equal(2, await ExecuteScalarLongAsync("SELECT item_count FROM inventory WHERE item_unique_id = 9001"));
		Assert.Equal(0, await ExecuteScalarLongAsync("SELECT COUNT(*) FROM inventory WHERE item_unique_id IN (9002, 9003)"));
		Assert.Equal(0, await ExecuteScalarLongAsync("SELECT COUNT(*) FROM item_stones WHERE item_unique_id IN (9002, 9003)"));
		Assert.Equal(100000402, await ExecuteScalarLongAsync("SELECT item_id FROM inventory WHERE item_unique_id = 9101"));
		Assert.Equal(10, await ExecuteScalarLongAsync("SELECT enchant FROM inventory WHERE item_unique_id = 9101"));
		Assert.Equal(3, await ExecuteScalarLongAsync("SELECT enchant_bonus FROM inventory WHERE item_unique_id = 9101"));
		Assert.Equal(2, await ExecuteScalarLongAsync("SELECT optional_socket FROM inventory WHERE item_unique_id = 9101"));
		Assert.Equal(1, await ExecuteScalarLongAsync("SELECT optional_fusion_socket FROM inventory WHERE item_unique_id = 9101"));
		Assert.Equal(4, await ExecuteScalarLongAsync("SELECT tune_count FROM inventory WHERE item_unique_id = 9101"));
		Assert.Equal(55, await ExecuteScalarLongAsync("SELECT rnd_bonus FROM inventory WHERE item_unique_id = 9101"));
		Assert.Equal(66, await ExecuteScalarLongAsync("SELECT fusion_rnd_bonus FROM inventory WHERE item_unique_id = 9101"));
		Assert.Equal(7, await ExecuteScalarLongAsync("SELECT tempering FROM inventory WHERE item_unique_id = 9101"));
		Assert.Equal(1, await ExecuteScalarLongAsync("SELECT is_amplified FROM inventory WHERE item_unique_id = 9101"));
		Assert.Equal(1234, await ExecuteScalarLongAsync("SELECT buff_skill FROM inventory WHERE item_unique_id = 9101"));
		Assert.Equal(8, await ExecuteScalarLongAsync("SELECT rnd_plume_bonus FROM inventory WHERE item_unique_id = 9101"));
		Assert.Equal(4, await ExecuteScalarLongAsync("SELECT COUNT(*) FROM item_stones WHERE item_unique_id = 9101"));
		Assert.Equal(167000010, await ExecuteScalarLongAsync("SELECT item_id FROM item_stones WHERE item_unique_id = 9101 AND category = 0 AND slot = 0"));
		Assert.Equal(168000010, await ExecuteScalarLongAsync("SELECT item_id FROM item_stones WHERE item_unique_id = 9101 AND category = 1 AND slot = 0"));
		Assert.Equal(44, await ExecuteScalarLongAsync("SELECT proc_count FROM item_stones WHERE item_unique_id = 9101 AND category = 1 AND slot = 0"));
		Assert.Equal(167100010, await ExecuteScalarLongAsync("SELECT item_id FROM item_stones WHERE item_unique_id = 9101 AND category = 2 AND slot = 1"));
		Assert.Equal(169000010, await ExecuteScalarLongAsync("SELECT item_id FROM item_stones WHERE item_unique_id = 9101 AND category = 3 AND slot = 0"));
		Assert.Equal(9, await ExecuteScalarLongAsync("SELECT polishNumber FROM item_stones WHERE item_unique_id = 9101 AND category = 3 AND slot = 0"));
		Assert.Equal(250, await ExecuteScalarLongAsync("SELECT polishCharge FROM item_stones WHERE item_unique_id = 9101 AND category = 3 AND slot = 0"));
		Assert.Equal(700, await ExecuteScalarLongAsync("SELECT ap FROM abyss_rank WHERE player_id = 1001"));
		Assert.Equal(3, await ExecuteScalarLongAsync("SELECT `rank` FROM abyss_rank WHERE player_id = 1001"));
		Assert.Equal(10, await ExecuteScalarLongAsync("SELECT daily_ap FROM abyss_rank WHERE player_id = 1001"));
		Assert.Equal(20, await ExecuteScalarLongAsync("SELECT weekly_ap FROM abyss_rank WHERE player_id = 1001"));
		Assert.Equal(8, await ExecuteScalarLongAsync("SELECT rank_pos FROM abyss_rank WHERE player_id = 1001"));
		Assert.Equal(50, await ExecuteScalarLongAsync("SELECT gp FROM abyss_rank WHERE player_id = 1001"));
	}

	[Fact]
	public async Task SaveItemPurificationMutation_RollsBackPriorUpdatesWhenRequiredDeleteFailsAgainstJavaSchema_WhenEnabled()
	{
		if (Environment.GetEnvironmentVariable("AION_GAMESERVER_DB_INTEGRATION") != "1")
			return;

		// Java source breadcrumbs: InventoryDAO.deleteItems/updateItems commit by category; C# keeps this write set atomic.
		InitializeDatabaseFactory();
		await InitializeSchemaAsync();
		await SeedPlayerAsync();
		await SeedInventoryItemAsync(9201, itemId: 186000001, count: 5);

		var repository = new MySqlPlayerEnterWorldRepository(
			new GameServerRuntimeContext(),
			NullLogger<MySqlPlayerEnterWorldRepository>.Instance);

		var saved = await repository.SaveItemPurificationMutationAsync(
			PlayerObjectId,
			materialItemUpdates:
			[
				new InventoryItem { ObjectId = 9201, Count = 2 },
			],
			deletedMaterialItemObjectIds:
			[
				9299,
			],
			baseItemUpdate: null,
			deletedBaseItemObjectId: null,
			updatedTargetItems: [],
			addedTargetItems: [],
			abyssRank: null);

		Assert.False(saved);
		Assert.Equal(5, await ExecuteScalarLongAsync("SELECT item_count FROM inventory WHERE item_unique_id = 9201"));
		Assert.Equal(0, await ExecuteScalarLongAsync("SELECT COUNT(*) FROM inventory WHERE item_unique_id = 9299"));
	}

	[Fact]
	public async Task SaveItemChargeAllMutation_RollsBackPriorChargeUpdatesWhenLaterItemMissingAgainstJavaSchema_WhenEnabled()
	{
		if (Environment.GetEnvironmentVariable("AION_GAMESERVER_DB_INTEGRATION") != "1")
			return;

		// Java source breadcrumbs: ItemChargeService.chargeItems, ChargeInfo.updateChargePoints,
		// InventoryDAO.UPDATE_QUERY charge column. C# repository keeps this charge-all write set atomic.
		InitializeDatabaseFactory();
		await InitializeSchemaAsync();
		await SeedPlayerAsync();
		await SeedInventoryItemAsync(9301, itemId: 100000401, count: 1, charge: 0);

		var repository = new MySqlPlayerEnterWorldRepository(
			new GameServerRuntimeContext(),
			NullLogger<MySqlPlayerEnterWorldRepository>.Instance);

		var saved = await repository.SaveItemChargeAllMutationAsync(
			PlayerObjectId,
			chargedItems:
			[
				new InventoryItem { ObjectId = 9301, Charge = ItemChargeService.Level1ChargePoints },
				new InventoryItem { ObjectId = 9399, Charge = ItemChargeService.Level1ChargePoints },
			],
			kinahItem: null,
			abyssRank: null);

		Assert.False(saved);
		Assert.Equal(0, await ExecuteScalarLongAsync("SELECT charge FROM inventory WHERE item_unique_id = 9301"));
		Assert.Equal(0, await ExecuteScalarLongAsync("SELECT COUNT(*) FROM inventory WHERE item_unique_id = 9399"));
	}

	[Fact]
	public async Task LoadPlayerQuests_HydratesRewardGroupAndRepeatTimesAgainstJavaSchema_WhenEnabled()
	{
		if (Environment.GetEnvironmentVariable("AION_GAMESERVER_DB_INTEGRATION") != "1")
			return;

		// Java parity: dao/PlayerQuestListDAO.SELECT_QUERY reads nullable reward, next_repeat_time, and complete_time.
		InitializeDatabaseFactory();
		await InitializeSchemaAsync();
		await SeedPlayerAsync();
		await ExecuteNonQueryAsync(
			"""
			INSERT INTO player_quests (
				player_id, quest_id, status, quest_vars, flags, complete_count, next_repeat_time, reward, complete_time
			)
			VALUES
				(1001, 5001, 'COMPLETE', 0, 0, 2, '2026-05-25 09:00:00', 3, '2026-05-24 13:10:00'),
				(1001, 5002, 'COMPLETE', 0, 0, 1, NULL, NULL, NULL)
			""");

		var repository = new MySqlPlayerEnterWorldRepository(
			new GameServerRuntimeContext(),
			NullLogger<MySqlPlayerEnterWorldRepository>.Instance);

		var quests = await repository.LoadPlayerQuestsAsync(PlayerObjectId);

		Assert.Equal(2, quests.Count);
		Assert.Equal(3, quests.Single(quest => quest.QuestId == 5001).RewardGroup);
		Assert.NotNull(quests.Single(quest => quest.QuestId == 5001).NextRepeatTime);
		Assert.NotNull(quests.Single(quest => quest.QuestId == 5001).CompleteTime);
		Assert.Null(quests.Single(quest => quest.QuestId == 5002).RewardGroup);
		Assert.Null(quests.Single(quest => quest.QuestId == 5002).NextRepeatTime);
		Assert.Null(quests.Single(quest => quest.QuestId == 5002).CompleteTime);
	}

	[Fact]
	public async Task LoadPlayerNpcFactions_HydratesActiveSlotsAndTimeLimitsAgainstJavaSchema_WhenEnabled()
	{
		if (Environment.GetEnvironmentVariable("AION_GAMESERVER_DB_INTEGRATION") != "1")
			return;

		// Java parity: dao/PlayerNpcFactionsDAO.SELECT_QUERY and NpcFactions.addNpcFaction slot/time-limit rules.
		InitializeDatabaseFactory();
		await InitializeSchemaAsync();
		await SeedPlayerAsync();
		await ExecuteNonQueryAsync(
			"""
			INSERT INTO player_npc_factions (player_id, faction_id, active, time, state, quest_id)
			VALUES
				(1001, 2, true, 1000, 'COMPLETE', 35007),
				(1001, 8, true, -1, 'NOTING', 47000)
			""");

		var npcFactions = new NpcFactionTable(
		[
			new NpcFactionSummary(2, "Alabaster Order", 1129000, "DAILY", 30, 99, "ELYOS", [799803], 0),
			new NpcFactionSummary(8, "Kaisinel Academy", 1129006, "MENTOR", 10, 39, "ELYOS", [799813], 0),
		]);
		var repository = new MySqlPlayerEnterWorldRepository(
			new GameServerRuntimeContext(),
			NullLogger<MySqlPlayerEnterWorldRepository>.Instance);

		var snapshot = await repository.LoadPlayerNpcFactionsAsync(PlayerObjectId, npcFactions, currentEpochSeconds: 1500);

		Assert.True(snapshot.HasActiveFaction(2));
		Assert.True(snapshot.HasActiveFaction(8));
		Assert.True(snapshot.CanStartQuest(isMentorQuest: false, currentEpochSeconds: 1500));
		Assert.False(snapshot.CanStartQuest(isMentorQuest: true, currentEpochSeconds: 1500));
		Assert.True(snapshot.CanStartQuest(isMentorQuest: true, currentEpochSeconds: 1501));
	}

	[Fact]
	public async Task LoadPlayerAsync_HydratesLegionLevelForTradeListFilteringAgainstJavaSchema_WhenEnabled()
	{
		if (Environment.GetEnvironmentVariable("AION_GAMESERVER_DB_INTEGRATION") != "1")
			return;

		// Java source breadcrumbs: LegionDAO.loadLegion reads legion info fields written by SM_LEGION_INFO.
		InitializeDatabaseFactory();
		await InitializeSchemaAsync();
		await SeedPlayerAsync();
		await ExecuteNonQueryAsync(
			"""
			INSERT INTO legions (
				id, name, level, disband_time, contribution_points,
				occupied_legion_dominion, last_legion_dominion, current_legion_dominion)
			VALUES (5001, 'Hydrated Legion', 4, 1771234567, 55000, 5, 6, 7)
			""");
		await ExecuteNonQueryAsync(
			"""
			INSERT INTO legion_members (legion_id, player_id, `rank`)
			VALUES (5001, 1001, 'VOLUNTEER')
			""");

		var repository = new MySqlPlayerEnterWorldRepository(
			new GameServerRuntimeContext(),
			NullLogger<MySqlPlayerEnterWorldRepository>.Instance);

		var player = await repository.LoadPlayerAsync(accountId: 1, playerObjectId: PlayerObjectId);

		Assert.NotNull(player);
		Assert.Equal(5001, player.LegionId);
		Assert.Equal(4, player.LegionLevel);
		Assert.Equal("Hydrated Legion", player.LegionName);
		Assert.Equal(1_771_234_567, player.LegionDisbandTime);
		Assert.True(player.IsLegionDisbanding);
		Assert.Equal(55_000, player.LegionContributionPoints);
		Assert.Equal(5, player.LegionOccupiedLegionDominion);
		Assert.Equal(6, player.LegionLastLegionDominion);
		Assert.Equal(7, player.LegionCurrentLegionDominion);
	}

	[Fact]
	public async Task SaveLegionCurrentDominionAsync_WritesJavaLegionDominionColumn_WhenEnabled()
	{
		if (Environment.GetEnvironmentVariable("AION_GAMESERVER_DB_INTEGRATION") != "1")
			return;

		// Java source breadcrumbs: LegionService.joinLegionDominion -> Legion.setCurrentLegionDominion
		// -> LegionDAO.storeLegion writes current_legion_dominion.
		InitializeDatabaseFactory();
		await InitializeSchemaAsync();
		await SeedPlayerAsync();
		await ExecuteNonQueryAsync(
			"""
			INSERT INTO legions (id, name, level, current_legion_dominion)
			VALUES (5001, 'Hydrated Legion', 4, 0)
			""");

		var repository = new MySqlPlayerEnterWorldRepository(
			new GameServerRuntimeContext(),
			NullLogger<MySqlPlayerEnterWorldRepository>.Instance);

		var saved = await repository.SaveLegionCurrentDominionAsync(5001, 5);

		Assert.True(saved);
		Assert.Equal(5, await ExecuteScalarLongAsync("SELECT current_legion_dominion FROM legions WHERE id = 5001"));
	}

	[Fact]
	public async Task TryAddLegionDominionParticipantAsync_InsertsOnceLikeJavaLocationJoin_WhenEnabled()
	{
		if (Environment.GetEnvironmentVariable("AION_GAMESERVER_DB_INTEGRATION") != "1")
			return;

		// Java source breadcrumbs: LegionDominionLocation.join rejects existing legion ids before
		// LegionDominionDAO.storeNewInfo inserts legion_dominion_participants.
		InitializeDatabaseFactory();
		await InitializeSchemaAsync();

		var repository = new MySqlPlayerEnterWorldRepository(
			new GameServerRuntimeContext(),
			NullLogger<MySqlPlayerEnterWorldRepository>.Instance);

		var inserted = await repository.TryAddLegionDominionParticipantAsync(5, 5001);
		var duplicate = await repository.TryAddLegionDominionParticipantAsync(5, 5001);

		Assert.True(inserted);
		Assert.False(duplicate);
		Assert.Equal(
			1,
			await ExecuteScalarLongAsync(
				"SELECT COUNT(*) FROM legion_dominion_participants WHERE legion_dominion_id = 5 AND legion_id = 5001"));
	}

	[Fact]
	public async Task LoadPlayerAsync_HydratesLatestLegionAnnouncementAgainstJavaSchema_WhenEnabled()
	{
		if (Environment.GetEnvironmentVariable("AION_GAMESERVER_DB_INTEGRATION") != "1")
			return;

		// Java source breadcrumbs: LegionDAO.loadAnnouncement reads legion_announcement_list ordered by date DESC LIMIT 1.
		InitializeDatabaseFactory();
		await InitializeSchemaAsync();
		await SeedPlayerAsync();
		await ExecuteNonQueryAsync(
			"""
			INSERT INTO legions (id, name, level, disband_time)
			VALUES (5001, 'Hydrated Legion', 4, 0)
			""");
		await ExecuteNonQueryAsync(
			"""
			INSERT INTO legion_members (legion_id, player_id, `rank`)
			VALUES (5001, 1001, 'VOLUNTEER')
			""");
		await ExecuteNonQueryAsync(
			"""
			INSERT INTO legion_announcement_list (legion_id, announcement, date)
			VALUES
				(5001, 'Old notice', '2026-01-01 00:00:00'),
				(5001, 'Current notice', '2026-02-01 00:00:00')
			""");

		var repository = new MySqlPlayerEnterWorldRepository(
			new GameServerRuntimeContext(),
			NullLogger<MySqlPlayerEnterWorldRepository>.Instance);

		var player = await repository.LoadPlayerAsync(accountId: 1, playerObjectId: PlayerObjectId);

		Assert.NotNull(player);
		Assert.Equal("Current notice", player.LegionAnnouncement);
		Assert.True(player.LegionAnnouncementEpochSeconds > 0);
	}

	[Fact]
	public async Task SaveLegionAnnouncementAsync_ReplacesAndClearsAgainstJavaSchema_WhenEnabled()
	{
		if (Environment.GetEnvironmentVariable("AION_GAMESERVER_DB_INTEGRATION") != "1")
			return;

		// Java source breadcrumbs: LegionDAO.saveAnnouncement deletes all existing rows and inserts one non-null announcement.
		InitializeDatabaseFactory();
		await InitializeSchemaAsync();
		await ExecuteNonQueryAsync(
			"""
			INSERT INTO legions (id, name, level, disband_time)
			VALUES (5001, 'Hydrated Legion', 4, 0)
			""");
		await ExecuteNonQueryAsync(
			"""
			INSERT INTO legion_announcement_list (legion_id, announcement, date)
			VALUES
				(5001, 'Old notice', '2026-01-01 00:00:00'),
				(5001, 'Older notice', '2025-01-01 00:00:00')
			""");

		var repository = new MySqlPlayerEnterWorldRepository(
			new GameServerRuntimeContext(),
			NullLogger<MySqlPlayerEnterWorldRepository>.Instance);

		var saved = await repository.SaveLegionAnnouncementAsync(
			5001,
			"Current notice",
			DateTimeOffset.FromUnixTimeSeconds(1_771_234_500));

		Assert.True(saved);
		Assert.Equal(1, await ExecuteScalarLongAsync("SELECT COUNT(*) FROM legion_announcement_list WHERE legion_id = 5001"));
		Assert.Equal(
			"Current notice",
			await ExecuteScalarStringAsync("SELECT announcement FROM legion_announcement_list WHERE legion_id = 5001"));

		var cleared = await repository.SaveLegionAnnouncementAsync(5001, null, null);

		Assert.True(cleared);
		Assert.Equal(0, await ExecuteScalarLongAsync("SELECT COUNT(*) FROM legion_announcement_list WHERE legion_id = 5001"));
	}

	[Fact]
	public async Task LoadPlayerAsync_DefaultsLegionFactsWhenNoLegionMemberAgainstJavaSchema_WhenEnabled()
	{
		if (Environment.GetEnvironmentVariable("AION_GAMESERVER_DB_INTEGRATION") != "1")
			return;

		// Java source breadcrumbs: SM_TRADELIST/DialogService use player.getLegion() == null ? 0 : legion level.
		InitializeDatabaseFactory();
		await InitializeSchemaAsync();
		await SeedPlayerAsync();

		var repository = new MySqlPlayerEnterWorldRepository(
			new GameServerRuntimeContext(),
			NullLogger<MySqlPlayerEnterWorldRepository>.Instance);

		var player = await repository.LoadPlayerAsync(accountId: 1, playerObjectId: PlayerObjectId);

		Assert.NotNull(player);
		Assert.Equal(0, player.LegionId);
		Assert.Equal(0, player.LegionLevel);
		Assert.Equal(string.Empty, player.LegionName);
		Assert.Equal(0, player.LegionDisbandTime);
		Assert.False(player.IsLegionDisbanding);
		Assert.Equal(0, player.LegionContributionPoints);
		Assert.Equal(0, player.LegionOccupiedLegionDominion);
		Assert.Equal(0, player.LegionLastLegionDominion);
		Assert.Equal(0, player.LegionCurrentLegionDominion);
	}

	[Fact]
	public async Task LoadLegionEmblemAsync_HydratesCustomEmblemAgainstJavaSchema_WhenEnabled()
	{
		if (Environment.GetEnvironmentVariable("AION_GAMESERVER_DB_INTEGRATION") != "1")
			return;

		// Java source breadcrumbs: LegionService.getLegion -> LegionDAO.loadLegion + LegionDAO.loadLegionEmblem.
		InitializeDatabaseFactory();
		await InitializeSchemaAsync();
		await ExecuteNonQueryAsync(
			"""
			INSERT INTO legions (id, name, level, disband_time)
			VALUES (5001, 'Hydrated Legion', 4, 0)
			""");
		await ExecuteNonQueryAsync(
			"""
			INSERT INTO legion_emblems (legion_id, emblem_id, color_a, color_r, color_g, color_b, emblem_type, emblem_data)
			VALUES (5001, 7, 255, 10, 20, 30, 'CUSTOM', X'01020304')
			""");

		var repository = new MySqlPlayerEnterWorldRepository(
			new GameServerRuntimeContext(),
			NullLogger<MySqlPlayerEnterWorldRepository>.Instance);

		var emblem = await repository.LoadLegionEmblemAsync(5001);

		Assert.NotNull(emblem);
		Assert.Equal(5001, emblem.LegionId);
		Assert.Equal("Hydrated Legion", emblem.LegionName);
		Assert.Equal(7, emblem.EmblemId);
		Assert.Equal(0x80, emblem.EmblemType);
		Assert.Equal(255, emblem.ColorA);
		Assert.Equal(10, emblem.ColorR);
		Assert.Equal(20, emblem.ColorG);
		Assert.Equal(30, emblem.ColorB);
		Assert.Equal([1, 2, 3, 4], emblem.CustomEmblemData);
	}

	[Fact]
	public async Task SaveLegionEmblemMutationAsync_PersistsDefaultEmblemAndKinahAgainstJavaSchema_WhenEnabled()
	{
		if (Environment.GetEnvironmentVariable("AION_GAMESERVER_DB_INTEGRATION") != "1")
			return;

		// Java source breadcrumbs: LegionService.storeLegionEmblem -> LegionDAO.storeLegionEmblem + Inventory.decreaseKinah.
		InitializeDatabaseFactory();
		await InitializeSchemaAsync();
		await SeedPlayerAsync();
		await SeedInventoryItemAsync(9901, itemId: 182400001, count: 700);
		await ExecuteNonQueryAsync(
			"""
			INSERT INTO legions (id, name, level, disband_time)
			VALUES (5001, 'Mutation Legion', 2, 0)
			""");

		var repository = new MySqlPlayerEnterWorldRepository(
			new GameServerRuntimeContext(),
			NullLogger<MySqlPlayerEnterWorldRepository>.Instance);
		var kinah = new InventoryItem
		{
			ObjectId = 9901,
			ItemId = 182400001,
			OwnerId = PlayerObjectId,
			Location = 0,
			Count = 600,
		};
		var emblem = new LegionEmblemSnapshot(
			5001,
			"Mutation Legion",
			EmblemId: 12,
			EmblemType: 0,
			ColorA: 200,
			ColorR: 21,
			ColorG: 22,
			ColorB: 23,
			CustomEmblemData: Array.Empty<byte>());

		var saved = await repository.SaveLegionEmblemMutationAsync(PlayerObjectId, 5001, emblem, kinah);

		Assert.True(saved);
		Assert.Equal(600, await ExecuteScalarLongAsync("SELECT item_count FROM inventory WHERE item_unique_id = 9901"));
		Assert.Equal(12, await ExecuteScalarLongAsync("SELECT emblem_id FROM legion_emblems WHERE legion_id = 5001"));
		Assert.Equal(200, await ExecuteScalarLongAsync("SELECT color_a FROM legion_emblems WHERE legion_id = 5001"));
		Assert.Equal(21, await ExecuteScalarLongAsync("SELECT color_r FROM legion_emblems WHERE legion_id = 5001"));
		Assert.Equal(22, await ExecuteScalarLongAsync("SELECT color_g FROM legion_emblems WHERE legion_id = 5001"));
		Assert.Equal(23, await ExecuteScalarLongAsync("SELECT color_b FROM legion_emblems WHERE legion_id = 5001"));
		Assert.Equal("DEFAULT", await ExecuteScalarStringAsync("SELECT emblem_type FROM legion_emblems WHERE legion_id = 5001"));
		Assert.Equal(0, await ExecuteScalarLongAsync("SELECT COUNT(*) FROM legion_emblems WHERE legion_id = 5001 AND emblem_data IS NOT NULL"));
	}

	[Fact]
	public async Task SaveLegionEmblemMutationAsync_PersistsCustomEmblemDataAgainstJavaSchema_WhenEnabled()
	{
		if (Environment.GetEnvironmentVariable("AION_GAMESERVER_DB_INTEGRATION") != "1")
			return;

		// Java source breadcrumbs: LegionService.uploadEmblemData -> LegionEmblem.setCustomEmblemData -> LegionDAO.storeLegionEmblem.
		InitializeDatabaseFactory();
		await InitializeSchemaAsync();
		await SeedPlayerAsync();
		await SeedInventoryItemAsync(9902, itemId: 182400001, count: 700);
		await ExecuteNonQueryAsync(
			"""
			INSERT INTO legions (id, name, level, disband_time)
			VALUES (5002, 'Custom Mutation Legion', 3, 0)
			""");

		var repository = new MySqlPlayerEnterWorldRepository(
			new GameServerRuntimeContext(),
			NullLogger<MySqlPlayerEnterWorldRepository>.Instance);
		var kinah = new InventoryItem
		{
			ObjectId = 9902,
			ItemId = 182400001,
			OwnerId = PlayerObjectId,
			Location = 0,
			Count = 600,
		};
		var emblem = new LegionEmblemSnapshot(
			5002,
			"Custom Mutation Legion",
			EmblemId: 6,
			EmblemType: 0x80,
			ColorA: 200,
			ColorR: 21,
			ColorG: 22,
			ColorB: 23,
			CustomEmblemData: [0x10, 0x20, 0x30, 0x40]);

		var saved = await repository.SaveLegionEmblemMutationAsync(PlayerObjectId, 5002, emblem, kinah);

		Assert.True(saved);
		Assert.Equal(600, await ExecuteScalarLongAsync("SELECT item_count FROM inventory WHERE item_unique_id = 9902"));
		Assert.Equal("CUSTOM", await ExecuteScalarStringAsync("SELECT emblem_type FROM legion_emblems WHERE legion_id = 5002"));
		Assert.Equal(4, await ExecuteScalarLongAsync("SELECT OCTET_LENGTH(emblem_data) FROM legion_emblems WHERE legion_id = 5002"));
		var loaded = await repository.LoadLegionEmblemAsync(5002);
		Assert.NotNull(loaded);
		Assert.Equal([0x10, 0x20, 0x30, 0x40], loaded.CustomEmblemData);
	}

	private static void InitializeDatabaseFactory()
	{
		DatabaseFactory.Initialize(
			server: Environment.GetEnvironmentVariable("AION_GAMESERVER_DB_HOST") ?? "localhost",
			userId: Environment.GetEnvironmentVariable("AION_GAMESERVER_DB_USER") ?? "root",
			password: Environment.GetEnvironmentVariable("AION_GAMESERVER_DB_PASSWORD") ?? "aion",
			database: Environment.GetEnvironmentVariable("AION_GAMESERVER_DB_NAME") ?? "aion_gs",
			port: int.Parse(Environment.GetEnvironmentVariable("AION_GAMESERVER_DB_PORT") ?? "3307"));
	}

	private static async Task InitializeSchemaAsync()
	{
		var sql = await File.ReadAllTextAsync(FindGameSchemaPath());
		await using var connection = DatabaseFactory.GetConnection();
		await connection.OpenAsync();
		foreach (var statement in SplitSqlStatements(sql))
		{
			await using var command = connection.CreateCommand();
			command.CommandText = statement;
			await command.ExecuteNonQueryAsync();
		}
	}

	private static string FindGameSchemaPath()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory != null)
		{
			var candidate = Path.Combine(directory.FullName, "game-server", "sql", "aion_gs.sql");
			if (File.Exists(candidate))
				return candidate;
			directory = directory.Parent;
		}

		throw new FileNotFoundException("Could not find game-server/sql/aion_gs.sql from test output directory.", "game-server/sql/aion_gs.sql");
	}

	private static IEnumerable<string> SplitSqlStatements(string sql)
	{
		var lines = sql.Split('\n')
			.Select(line => line.TrimEnd('\r'))
			.Where(line => !line.TrimStart().StartsWith("--", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(line));
		return string.Join('\n', lines)
			.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Where(statement => !string.IsNullOrWhiteSpace(statement));
	}

	private static async Task SeedPlayerAsync()
	{
		await ExecuteNonQueryAsync(
			"""
			INSERT INTO players (
				id, name, account_id, account_name, exp, recoverexp, old_level, x, y, z, heading, world_id,
				gender, race, player_class, creation_date
			)
			VALUES (1001, 'PurifyIntegration', 1, 'integration', 0, 0, 0, 0, 0, 0, 0, 210010000,
				'MALE', 'ELYOS', 'RANGER', CURRENT_TIMESTAMP)
			""");
	}

	private static Task SeedInventoryItemAsync(
		int objectId,
		int itemId,
		long count,
		int enchant = 0,
		int charge = 0,
		int ownerId = PlayerObjectId,
		int location = 0)
	{
		return ExecuteNonQueryAsync(
			$"""
			INSERT INTO inventory (
				item_unique_id, item_id, item_count, item_owner, item_location, enchant, charge
			)
			VALUES ({objectId}, {itemId}, {count}, {ownerId}, {location}, {enchant}, {charge})
			""");
	}

	private static Task SeedItemStoneAsync(
		int itemObjectId,
		int itemId,
		int slot,
		int category,
		int polishNumber = 0,
		int polishCharge = 0,
		int procCount = 0)
	{
		return ExecuteNonQueryAsync(
			$"""
			INSERT INTO item_stones (item_unique_id, item_id, slot, category, polishNumber, polishCharge, proc_count)
			VALUES ({itemObjectId}, {itemId}, {slot}, {category}, {polishNumber}, {polishCharge}, {procCount})
			""");
	}

	private static async Task ExecuteNonQueryAsync(string sql)
	{
		await using var connection = DatabaseFactory.GetConnection();
		await connection.OpenAsync();
		await using var command = connection.CreateCommand();
		command.CommandText = sql;
		await command.ExecuteNonQueryAsync();
	}

	private static async Task<long> ExecuteScalarLongAsync(string sql)
	{
		await using var connection = DatabaseFactory.GetConnection();
		await connection.OpenAsync();
		await using var command = connection.CreateCommand();
		command.CommandText = sql;
		var value = await command.ExecuteScalarAsync();
		Assert.NotNull(value);
		return Convert.ToInt64(value);
	}

	private static async Task<string> ExecuteScalarStringAsync(string sql)
	{
		await using var connection = DatabaseFactory.GetConnection();
		await connection.OpenAsync();
		await using var command = connection.CreateCommand();
		command.CommandText = sql;
		var value = await command.ExecuteScalarAsync();
		Assert.NotNull(value);
		return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
	}
}
