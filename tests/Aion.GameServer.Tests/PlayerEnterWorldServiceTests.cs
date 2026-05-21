using Aion.GameServer.Configuration;
using Aion.GameServer.Data;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Tests;

public sealed class PlayerEnterWorldServiceTests
{
	[Fact]
	public async Task EnterWorld_MarksPlayerOnlineAndStoresInWorld()
	{
		var repository = new CapturingEnterWorldRepository
		{
			Player = CreatePlayer(lastOnline: DateTime.Now.AddMinutes(-5)),
			Items = [new InventoryItem { ObjectId = 2001, ItemId = 100000094, IsEquipped = true, Location = 0, Slot = 1 }],
			WarehouseItems = [new InventoryItem { ObjectId = 2004, ItemId = 100000094, Location = 1, Slot = 1 }],
			AccountWarehouseItems = [new InventoryItem { ObjectId = 2005, ItemId = 100000094, Location = 2, Slot = 1 }],
			Skills = [new PlayerSkill { SkillId = 37, SkillLevel = 1 }],
			SkillCooldowns = new Dictionary<int, long> { [37] = 123456 },
			ItemCooldowns = new Dictionary<int, PlayerItemCooldown> { [7] = new(123456, 60) },
			Quests = [new PlayerQuestState(1001, "START", 2, 3, 0)],
			Titles = [new PlayerTitle(5, 0)],
			Motions = [new PlayerMotion(11, 0, true)],
			Emotions = [new PlayerEmotion(10, 0)],
			Recipes = [155000001],
			Macros = [new PlayerMacro(1, "<macro/>")],
			Mailbox = [new PlayerMail(9, 1001, "Sender", "Title", "Message", true, 0, 0, 10, 1, DateTime.Now)],
			BrokerSettlements = new PlayerBrokerSettlementSummary(2, 123456),
			Houses = [new PlayerHouse(50, 2001, 7001, DateTime.Now.AddDays(-1), null, false)],
			CraftCooldowns = new Dictionary<int, long> { [8] = 123456 },
			PortalCooldowns = new Dictionary<int, PlayerPortalCooldown> { [300030000] = new(300030000, 123456, 2) },
			LifeStats = new PlayerLifeStats(100, 200, 50),
			Friends = [new PlayerFriend(2002, "Friend", 0, "WARRIOR", "MALE", 210010000, null, string.Empty, "memo", false)],
			BlockedUsers = [new PlayerBlockedUser(2003, "Blocked", "reason")],
			AbyssRank = new PlayerAbyssRank(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15),
			Settings = new PlayerSettings { UiSettings = [1, 2], Shortcuts = [3], HouseBuddies = [4] },
			BindPoint = new PlayerBindPoint(210010000, 10, 20, 30, 0),
		};
		var world = CreateWorld();
		var service = CreateService(repository, world);

		var result = await service.EnterWorldAsync(accountId: 10, playerObjectId: 1001);

		Assert.Equal(EnterWorldCheckMessage.Ok, result.Message);
		Assert.Same(repository.Player, result.Player);
		Assert.True(repository.Player!.IsOnline);
		Assert.Single(repository.Player.InventoryItems);
		Assert.Single(repository.Player.WarehouseItems);
		Assert.Single(repository.Player.AccountWarehouseItems);
		Assert.Single(repository.Player.Skills);
		Assert.Single(repository.Player.SkillCooldowns);
		Assert.Single(repository.Player.ItemCooldowns);
		Assert.Single(repository.Player.Quests);
		Assert.Single(repository.Player.Titles);
		Assert.Single(repository.Player.Motions);
		Assert.Single(repository.Player.Emotions);
		Assert.Single(repository.Player.Recipes);
		Assert.Single(repository.Player.Macros);
		Assert.Single(repository.Player.Mailbox);
		Assert.True(repository.Player.BrokerSettlements.HasSettledItems);
		Assert.Single(repository.Player.Houses);
		Assert.Single(repository.Player.CraftCooldowns);
		Assert.Single(repository.Player.PortalCooldowns);
		Assert.NotNull(repository.Player.LifeStats);
		Assert.Single(repository.Player.Friends);
		Assert.Single(repository.Player.BlockedUsers);
		Assert.Equal(7, repository.Player.AbyssRank.Rank);
		Assert.NotNull(repository.Player.Settings.UiSettings);
		Assert.NotNull(repository.Player.BindPoint);
		Assert.Equal(1, repository.LoadItemsCalls);
		Assert.Equal(1, repository.LoadWarehouseItemsCalls);
		Assert.Equal(1, repository.LoadAccountWarehouseItemsCalls);
		Assert.Equal(1, repository.LoadSkillsCalls);
		Assert.Equal(1, repository.LoadSkillCooldownsCalls);
		Assert.Equal(1, repository.LoadItemCooldownsCalls);
		Assert.Equal(1, repository.LoadQuestsCalls);
		Assert.Equal(1, repository.LoadTitlesCalls);
		Assert.Equal(1, repository.LoadMotionsCalls);
		Assert.Equal(1, repository.LoadEmotionsCalls);
		Assert.Equal(1, repository.LoadRecipesCalls);
		Assert.Equal(1, repository.LoadMacrosCalls);
		Assert.Equal(1, repository.LoadMailboxCalls);
		Assert.Equal(1, repository.LoadBrokerSettlementsCalls);
		Assert.Equal(1, repository.LoadHousesCalls);
		Assert.Equal(1, repository.LoadCraftCooldownsCalls);
		Assert.Equal(1, repository.LoadPortalCooldownsCalls);
		Assert.Equal(1, repository.LoadLifeStatsCalls);
		Assert.Equal(1, repository.LoadFriendsCalls);
		Assert.Equal(1, repository.LoadBlockedUsersCalls);
		Assert.Equal(1, repository.LoadAbyssRankCalls);
		Assert.Equal(1, repository.LoadSettingsCalls);
		Assert.Equal(1, repository.LoadBindPointCalls);
		Assert.Equal(1, repository.MarkOnlineCalls);
		Assert.True(world.TryGetObject(1001, out var stored));
		Assert.Same(repository.Player, stored);
	}

	[Fact]
	public async Task EnterWorld_ReturnsConnectionErrorWhenPlayerIsMissingOrDuplicate()
	{
		var repository = new CapturingEnterWorldRepository();
		var world = CreateWorld();
		var service = CreateService(repository, world);

		var missing = await service.EnterWorldAsync(accountId: 10, playerObjectId: 1001);

		Assert.Equal(EnterWorldCheckMessage.ConnectionError, missing.Message);

		repository.Player = CreatePlayer();
		world.TryAddObject(1001, new object());

		var duplicate = await service.EnterWorldAsync(accountId: 10, playerObjectId: 1001);

		Assert.Equal(EnterWorldCheckMessage.ConnectionError, duplicate.Message);
		Assert.Equal(0, repository.MarkOnlineCalls);
	}

	[Fact]
	public async Task EnterWorld_ReturnsReentryTimeForOnlineOrRecentPlayer()
	{
		var repository = new CapturingEnterWorldRepository { Player = CreatePlayer(isOnline: true) };
		var service = CreateService(repository, CreateWorld());

		var online = await service.EnterWorldAsync(accountId: 10, playerObjectId: 1001);

		Assert.Equal(EnterWorldCheckMessage.ReentryTime, online.Message);

		repository = new CapturingEnterWorldRepository { Player = CreatePlayer(lastOnline: DateTime.Now.AddSeconds(-5)) };
		service = CreateService(repository, CreateWorld());

		var recent = await service.EnterWorldAsync(accountId: 10, playerObjectId: 1001);

		Assert.Equal(EnterWorldCheckMessage.ReentryTime, recent.Message);
	}

	[Fact]
	public async Task EnterWorld_RemovesWorldObjectWhenOnlineUpdateFails()
	{
		var repository = new CapturingEnterWorldRepository
		{
			Player = CreatePlayer(lastOnline: DateTime.Now.AddMinutes(-5)),
			MarkOnlineResult = false,
		};
		var world = CreateWorld();
		var service = CreateService(repository, world);

		var result = await service.EnterWorldAsync(accountId: 10, playerObjectId: 1001);

		Assert.Equal(EnterWorldCheckMessage.ConnectionError, result.Message);
		Assert.False(world.TryGetObject(1001, out _));
	}

	[Fact]
	public async Task LeaveWorld_RemovesPlayerFromWorldAndPersistsLogoutState()
	{
		var player = CreatePlayer(lastOnline: DateTime.Now.AddMinutes(-5));
		player.IsOnline = true;
		player.Position = new WorldPosition(210010000, 11, 22, 33, 44);
		player.LifeStats = new PlayerLifeStats(333, 444, 55);
		player.SkillCooldowns = new Dictionary<int, long> { [10] = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeMilliseconds() };
		player.ItemCooldowns = new Dictionary<int, PlayerItemCooldown> { [20] = new(DateTimeOffset.UtcNow.AddMinutes(6).ToUnixTimeMilliseconds(), 120) };
		player.Mailbox =
		[
			new PlayerMail(1, player.ObjectId, "Sender", "Title", "Message", true, 0, 0, 0, 0, DateTime.Now),
			new PlayerMail(2, player.ObjectId, "Sender", "Title", "Message", false, 0, 0, 0, 1, DateTime.Now),
		];
		var repository = new CapturingEnterWorldRepository { Player = player };
		var world = CreateWorld();
		world.TryAddObject(player.ObjectId, player);
		var service = CreateService(repository, world);

		await service.LeaveWorldAsync(player);

		Assert.False(player.IsOnline);
		Assert.NotNull(player.LastOnline);
		Assert.False(world.TryGetObject(player.ObjectId, out _));
		Assert.Equal(1, repository.SaveLogoutCalls);
		Assert.Same(player, repository.SavedLogoutPlayer);
		Assert.NotNull(repository.SavedLogoutPlayer);
		Assert.Equal(new PlayerLifeStats(333, 444, 55), repository.SavedLogoutPlayer!.LifeStats);
		Assert.Single(repository.SavedLogoutPlayer.SkillCooldowns);
		Assert.Single(repository.SavedLogoutPlayer.ItemCooldowns);
		Assert.Equal(player.LastOnline, repository.LogoutLastOnline);
	}

	[Fact]
	public async Task MacroMutations_UpdateLoadedPlayerAndRepository()
	{
		var player = CreatePlayer();
		player.Macros = [new PlayerMacro(1, "<old/>")];
		var repository = new CapturingEnterWorldRepository { Player = player };
		var service = CreateService(repository, CreateWorld());

		await service.SaveMacroAsync(player, 1, "<updated/>");
		await service.SaveMacroAsync(player, 2, "<new/>");
		await service.DeleteMacroAsync(player, 1);

		Assert.Equal([2], player.Macros.Select(macro => macro.Id).ToArray());
		Assert.Equal("<new/>", player.Macros.Single().Xml);
		Assert.Equal(2, repository.SaveMacroCalls);
		Assert.Equal(new PlayerMacro(2, "<new/>"), repository.SavedMacro);
		Assert.Equal(1, repository.DeleteMacroCalls);
		Assert.Equal(1, repository.DeletedMacroId);
	}

	[Fact]
	public async Task DeleteRecipe_RemovesLoadedRecipeAndRepositoryRow()
	{
		var player = CreatePlayer();
		player.Recipes = [155000001, 155000002];
		var repository = new CapturingEnterWorldRepository { Player = player };
		var service = CreateService(repository, CreateWorld());

		var deleted = await service.DeleteRecipeAsync(player, 155000001);
		var missing = await service.DeleteRecipeAsync(player, 155000003);

		Assert.True(deleted);
		Assert.False(missing);
		Assert.Equal([155000002], player.Recipes);
		Assert.Equal(1, repository.DeleteRecipeCalls);
		Assert.Equal(155000001, repository.DeletedRecipeId);
	}

	private static PlayerEnterWorldService CreateService(CapturingEnterWorldRepository repository, GameWorld world)
	{
		return new PlayerEnterWorldService(
			new GameServerOptions(),
			repository,
			world,
			NullLogger<PlayerEnterWorldService>.Instance);
	}

	private static GameWorld CreateWorld()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		world.Initialize();
		return world;
	}

	private static Player CreatePlayer(bool isOnline = false, DateTime? lastOnline = null)
	{
		return new Player
		{
			ObjectId = 1001,
			AccountId = 10,
			Name = "Character",
			PlayerClass = "WARRIOR",
			Race = "ELYOS",
			Gender = "MALE",
			WarehouseNpcExpands = 1,
			WarehouseBonusExpands = 2,
			IsOnline = isOnline,
			LastOnline = lastOnline,
			Position = new WorldPosition(210010000, 1, 2, 3, 32),
		};
	}

	private sealed class CapturingEnterWorldRepository : IPlayerEnterWorldRepository
	{
		public Player? Player { get; set; }

		public bool MarkOnlineResult { get; init; } = true;

		public IReadOnlyList<InventoryItem> Items { get; init; } = Array.Empty<InventoryItem>();

		public IReadOnlyList<InventoryItem> WarehouseItems { get; init; } = Array.Empty<InventoryItem>();

		public IReadOnlyList<InventoryItem> AccountWarehouseItems { get; init; } = Array.Empty<InventoryItem>();

		public IReadOnlyList<PlayerSkill> Skills { get; init; } = Array.Empty<PlayerSkill>();

		public IReadOnlyDictionary<int, long> SkillCooldowns { get; init; } = new Dictionary<int, long>();

		public IReadOnlyDictionary<int, PlayerItemCooldown> ItemCooldowns { get; init; } = new Dictionary<int, PlayerItemCooldown>();

		public IReadOnlyList<PlayerQuestState> Quests { get; init; } = Array.Empty<PlayerQuestState>();

		public IReadOnlyList<PlayerTitle> Titles { get; init; } = Array.Empty<PlayerTitle>();

		public IReadOnlyList<PlayerMotion> Motions { get; init; } = Array.Empty<PlayerMotion>();

		public IReadOnlyList<PlayerEmotion> Emotions { get; init; } = Array.Empty<PlayerEmotion>();

		public IReadOnlyList<int> Recipes { get; init; } = Array.Empty<int>();

		public IReadOnlyList<PlayerMacro> Macros { get; init; } = Array.Empty<PlayerMacro>();

		public IReadOnlyList<PlayerMail> Mailbox { get; init; } = Array.Empty<PlayerMail>();

		public PlayerBrokerSettlementSummary BrokerSettlements { get; init; } = PlayerBrokerSettlementSummary.Empty;

		public IReadOnlyList<PlayerHouse> Houses { get; init; } = Array.Empty<PlayerHouse>();

		public IReadOnlyDictionary<int, long> CraftCooldowns { get; init; } = new Dictionary<int, long>();

		public IReadOnlyDictionary<int, PlayerPortalCooldown> PortalCooldowns { get; init; } = new Dictionary<int, PlayerPortalCooldown>();

		public PlayerLifeStats? LifeStats { get; init; }

		public IReadOnlyList<PlayerFriend> Friends { get; init; } = Array.Empty<PlayerFriend>();

		public IReadOnlyList<PlayerBlockedUser> BlockedUsers { get; init; } = Array.Empty<PlayerBlockedUser>();

		public PlayerAbyssRank AbyssRank { get; init; } = PlayerAbyssRank.Default();

		public PlayerSettings Settings { get; init; } = new();

		public PlayerBindPoint? BindPoint { get; init; }

		public int LoadItemsCalls { get; private set; }

		public int LoadWarehouseItemsCalls { get; private set; }

		public int LoadAccountWarehouseItemsCalls { get; private set; }

		public int LoadSkillsCalls { get; private set; }

		public int LoadSkillCooldownsCalls { get; private set; }

		public int LoadItemCooldownsCalls { get; private set; }

		public int LoadQuestsCalls { get; private set; }

		public int LoadTitlesCalls { get; private set; }

		public int LoadMotionsCalls { get; private set; }

		public int LoadEmotionsCalls { get; private set; }

		public int LoadRecipesCalls { get; private set; }

		public int LoadMacrosCalls { get; private set; }

		public int LoadMailboxCalls { get; private set; }

		public int LoadBrokerSettlementsCalls { get; private set; }

		public int LoadHousesCalls { get; private set; }

		public int LoadCraftCooldownsCalls { get; private set; }

		public int LoadPortalCooldownsCalls { get; private set; }

		public int LoadLifeStatsCalls { get; private set; }

		public int LoadFriendsCalls { get; private set; }

		public int LoadBlockedUsersCalls { get; private set; }

		public int LoadAbyssRankCalls { get; private set; }

		public int LoadSettingsCalls { get; private set; }

		public int LoadBindPointCalls { get; private set; }

		public int MarkOnlineCalls { get; private set; }

		public DateTime? LastOnline { get; private set; }

		public int SaveLogoutCalls { get; private set; }

		public Player? SavedLogoutPlayer { get; private set; }

		public DateTime? LogoutLastOnline { get; private set; }

		public int SaveMacroCalls { get; private set; }

		public PlayerMacro? SavedMacro { get; private set; }

		public int DeleteMacroCalls { get; private set; }

		public int DeletedMacroId { get; private set; }

		public int DeleteRecipeCalls { get; private set; }

		public int DeletedRecipeId { get; private set; }

		public int SaveItemChargeMutationCalls { get; private set; }

		public InventoryItem? ChargedItem { get; private set; }

		public InventoryItem? ChargePaymentKinahItem { get; private set; }

		public PlayerAbyssRank? ChargePaymentAbyssRank { get; private set; }

		public int SaveItemChargeAllMutationCalls { get; private set; }

		public IReadOnlyList<InventoryItem> ChargeAllChargedItems { get; private set; } = Array.Empty<InventoryItem>();

		public InventoryItem? ChargeAllPaymentKinahItem { get; private set; }

		public PlayerAbyssRank? ChargeAllPaymentAbyssRank { get; private set; }

		public int SaveIdianPolishMutationCalls { get; private set; }

		public InventoryItem? IdianPolishTargetItem { get; private set; }

		public InventoryItem? IdianPolishSourceItemUpdate { get; private set; }

		public int? IdianPolishDeletedSourceItemObjectId { get; private set; }

		public int SaveItemChargeActionMutationCalls { get; private set; }

		public IReadOnlyList<InventoryItem> ChargeActionChargedItems { get; private set; } = Array.Empty<InventoryItem>();

		public InventoryItem? ChargeActionSourceItemUpdate { get; private set; }

		public int? ChargeActionDeletedSourceItemObjectId { get; private set; }

		public int SaveEquipmentMutationCalls { get; private set; }

		public IReadOnlyList<InventoryItem> EquipmentItems { get; private set; } = Array.Empty<InventoryItem>();

		public Task<Player?> LoadPlayerAsync(int accountId, int playerObjectId, CancellationToken cancellationToken = default)
		{
			return Task.FromResult(Player);
		}

		public Task<IReadOnlyList<InventoryItem>> LoadPlayerItemsAsync(int playerObjectId, CancellationToken cancellationToken = default)
		{
			LoadItemsCalls++;
			return Task.FromResult(Items);
		}

		public Task<IReadOnlyList<InventoryItem>> LoadPlayerWarehouseItemsAsync(int playerObjectId, CancellationToken cancellationToken = default)
		{
			LoadWarehouseItemsCalls++;
			return Task.FromResult(WarehouseItems);
		}

		public Task<IReadOnlyList<InventoryItem>> LoadAccountWarehouseItemsAsync(int accountId, CancellationToken cancellationToken = default)
		{
			LoadAccountWarehouseItemsCalls++;
			return Task.FromResult(AccountWarehouseItems);
		}

		public Task<IReadOnlyList<PlayerSkill>> LoadPlayerSkillsAsync(int playerObjectId, CancellationToken cancellationToken = default)
		{
			LoadSkillsCalls++;
			return Task.FromResult(Skills);
		}

		public Task<IReadOnlyDictionary<int, long>> LoadPlayerSkillCooldownsAsync(int playerObjectId, CancellationToken cancellationToken = default)
		{
			LoadSkillCooldownsCalls++;
			return Task.FromResult(SkillCooldowns);
		}

		public Task<IReadOnlyDictionary<int, PlayerItemCooldown>> LoadPlayerItemCooldownsAsync(int playerObjectId, CancellationToken cancellationToken = default)
		{
			LoadItemCooldownsCalls++;
			return Task.FromResult(ItemCooldowns);
		}

		public Task<IReadOnlyList<PlayerQuestState>> LoadPlayerQuestsAsync(int playerObjectId, CancellationToken cancellationToken = default)
		{
			LoadQuestsCalls++;
			return Task.FromResult(Quests);
		}

		public Task<IReadOnlyList<PlayerTitle>> LoadPlayerTitlesAsync(int playerObjectId, CancellationToken cancellationToken = default)
		{
			LoadTitlesCalls++;
			return Task.FromResult(Titles);
		}

		public Task<IReadOnlyList<PlayerMotion>> LoadPlayerMotionsAsync(int playerObjectId, CancellationToken cancellationToken = default)
		{
			LoadMotionsCalls++;
			return Task.FromResult(Motions);
		}

		public Task<IReadOnlyList<PlayerEmotion>> LoadPlayerEmotionsAsync(int playerObjectId, CancellationToken cancellationToken = default)
		{
			LoadEmotionsCalls++;
			return Task.FromResult(Emotions);
		}

		public Task<IReadOnlyList<int>> LoadPlayerRecipesAsync(int playerObjectId, CancellationToken cancellationToken = default)
		{
			LoadRecipesCalls++;
			return Task.FromResult(Recipes);
		}

		public Task<bool> DeletePlayerRecipeAsync(int playerObjectId, int recipeId, CancellationToken cancellationToken = default)
		{
			DeleteRecipeCalls++;
			DeletedRecipeId = recipeId;
			return Task.FromResult(true);
		}

		public Task<IReadOnlyList<PlayerMacro>> LoadPlayerMacrosAsync(int playerObjectId, CancellationToken cancellationToken = default)
		{
			LoadMacrosCalls++;
			return Task.FromResult(Macros);
		}

		public Task<bool> SavePlayerMacroAsync(int playerObjectId, PlayerMacro macro, CancellationToken cancellationToken = default)
		{
			SaveMacroCalls++;
			SavedMacro = macro;
			return Task.FromResult(true);
		}

		public Task<bool> DeletePlayerMacroAsync(int playerObjectId, int macroId, CancellationToken cancellationToken = default)
		{
			DeleteMacroCalls++;
			DeletedMacroId = macroId;
			return Task.FromResult(true);
		}

		public Task<IReadOnlyList<PlayerMail>> LoadPlayerMailboxAsync(int playerObjectId, CancellationToken cancellationToken = default)
		{
			LoadMailboxCalls++;
			return Task.FromResult(Mailbox);
		}

		public Task<PlayerBrokerSettlementSummary> LoadBrokerSettlementsAsync(int playerObjectId, string race, CancellationToken cancellationToken = default)
		{
			LoadBrokerSettlementsCalls++;
			return Task.FromResult(BrokerSettlements);
		}

		public Task<IReadOnlyList<PlayerHouse>> LoadPlayerHousesAsync(int playerObjectId, CancellationToken cancellationToken = default)
		{
			LoadHousesCalls++;
			return Task.FromResult(Houses);
		}

		public Task<IReadOnlyDictionary<int, long>> LoadPlayerCraftCooldownsAsync(int playerObjectId, CancellationToken cancellationToken = default)
		{
			LoadCraftCooldownsCalls++;
			return Task.FromResult(CraftCooldowns);
		}

		public Task<IReadOnlyDictionary<int, PlayerPortalCooldown>> LoadPlayerPortalCooldownsAsync(int playerObjectId, CancellationToken cancellationToken = default)
		{
			LoadPortalCooldownsCalls++;
			return Task.FromResult(PortalCooldowns);
		}

		public Task<PlayerLifeStats?> LoadPlayerLifeStatsAsync(int playerObjectId, CancellationToken cancellationToken = default)
		{
			LoadLifeStatsCalls++;
			return Task.FromResult(LifeStats);
		}

		public Task<IReadOnlyList<PlayerFriend>> LoadPlayerFriendsAsync(int playerObjectId, CancellationToken cancellationToken = default)
		{
			LoadFriendsCalls++;
			return Task.FromResult(Friends);
		}

		public Task<IReadOnlyList<PlayerBlockedUser>> LoadPlayerBlockedUsersAsync(int playerObjectId, CancellationToken cancellationToken = default)
		{
			LoadBlockedUsersCalls++;
			return Task.FromResult(BlockedUsers);
		}

		public Task<PlayerAbyssRank> LoadPlayerAbyssRankAsync(int playerObjectId, CancellationToken cancellationToken = default)
		{
			LoadAbyssRankCalls++;
			return Task.FromResult(AbyssRank);
		}

		public Task<PlayerSettings> LoadPlayerSettingsAsync(int playerObjectId, CancellationToken cancellationToken = default)
		{
			LoadSettingsCalls++;
			return Task.FromResult(Settings);
		}

		public Task<PlayerBindPoint?> LoadPlayerBindPointAsync(int playerObjectId, CancellationToken cancellationToken = default)
		{
			LoadBindPointCalls++;
			return Task.FromResult(BindPoint);
		}

		public Task<bool> MarkPlayerOnlineAsync(int playerObjectId, DateTime lastOnline, CancellationToken cancellationToken = default)
		{
			MarkOnlineCalls++;
			LastOnline = lastOnline;
			return Task.FromResult(MarkOnlineResult);
		}

		public Task<bool> SaveItemChargeMutationAsync(
			int playerObjectId,
			InventoryItem chargedItem,
			InventoryItem? kinahItem,
			PlayerAbyssRank? abyssRank,
			CancellationToken cancellationToken = default)
		{
			SaveItemChargeMutationCalls++;
			ChargedItem = chargedItem;
			ChargePaymentKinahItem = kinahItem;
			ChargePaymentAbyssRank = abyssRank;
			return Task.FromResult(true);
		}

		public Task<bool> SaveItemChargeAllMutationAsync(
			int playerObjectId,
			IReadOnlyList<InventoryItem> chargedItems,
			InventoryItem? kinahItem,
			PlayerAbyssRank? abyssRank,
			CancellationToken cancellationToken = default)
		{
			SaveItemChargeAllMutationCalls++;
			ChargeAllChargedItems = chargedItems;
			ChargeAllPaymentKinahItem = kinahItem;
			ChargeAllPaymentAbyssRank = abyssRank;
			return Task.FromResult(true);
		}

		public Task<bool> SaveIdianPolishMutationAsync(
			int playerObjectId,
			InventoryItem? targetItem,
			InventoryItem? sourceItemUpdate,
			int? deletedSourceItemObjectId,
			CancellationToken cancellationToken = default)
		{
			SaveIdianPolishMutationCalls++;
			IdianPolishTargetItem = targetItem;
			IdianPolishSourceItemUpdate = sourceItemUpdate;
			IdianPolishDeletedSourceItemObjectId = deletedSourceItemObjectId;
			return Task.FromResult(true);
		}

		public Task<bool> SaveItemChargeActionMutationAsync(
			int playerObjectId,
			IReadOnlyList<InventoryItem> chargedItems,
			InventoryItem? sourceItemUpdate,
			int? deletedSourceItemObjectId,
			CancellationToken cancellationToken = default)
		{
			SaveItemChargeActionMutationCalls++;
			ChargeActionChargedItems = chargedItems;
			ChargeActionSourceItemUpdate = sourceItemUpdate;
			ChargeActionDeletedSourceItemObjectId = deletedSourceItemObjectId;
			return Task.FromResult(true);
		}

		public Task<bool> SaveEquipmentMutationAsync(
			int playerObjectId,
			IReadOnlyList<InventoryItem> items,
			InventoryItem? kinahItem = null,
			CancellationToken cancellationToken = default)
		{
			SaveEquipmentMutationCalls++;
			EquipmentItems = items;
			return Task.FromResult(true);
		}

		public Task<bool> SavePlayerLogoutAsync(Player player, DateTime lastOnline, CancellationToken cancellationToken = default)
		{
			SaveLogoutCalls++;
			SavedLogoutPlayer = player;
			LogoutLastOnline = lastOnline;
			return Task.FromResult(true);
		}
	}
}
