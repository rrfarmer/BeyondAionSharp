using Aion.GameServer.Configuration;
using Aion.GameServer.Data;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
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
			HouseObjectCooldowns = new Dictionary<int, long> { [9001] = 123456 },
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
		Assert.Single(repository.Player.HouseObjectCooldowns);
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
		Assert.Equal(1, repository.LoadHouseObjectCooldownsCalls);
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
	public async Task EnterWorld_LoadsNpcFactionsWhenStaticDataIsAvailable()
	{
		var dataManager = await DataManager.LoadAsync(FindRepoRoot(), validateWhenCacheChanges: false);
		var runtimeContext = new GameServerRuntimeContext();
		runtimeContext.SetDataManager(dataManager);
		var npcFactions = new PlayerNpcFactionsSnapshot(
		[
			new PlayerNpcFactionState(
				FactionId: 2,
				IsActive: true,
				IsMentor: false,
				TimeEpochSeconds: 1000,
				PlayerNpcFactionQuestState.Complete,
				QuestId: 35007),
		]);
		var repository = new CapturingEnterWorldRepository
		{
			Player = CreatePlayer(lastOnline: DateTime.Now.AddMinutes(-5)),
			NpcFactions = npcFactions,
		};
		var service = CreateService(repository, CreateWorld(), runtimeContext);

		var result = await service.EnterWorldAsync(accountId: 10, playerObjectId: 1001);

		Assert.Equal(EnterWorldCheckMessage.Ok, result.Message);
		Assert.Same(npcFactions, result.Player!.NpcFactions);
		Assert.Equal(1, repository.LoadNpcFactionsCalls);
		Assert.Same(dataManager.StaticData.NpcFactions, repository.NpcFactionTable);
		Assert.True(repository.NpcFactionCurrentEpochSeconds > 0);
	}

	[Fact]
	public async Task EnterWorld_ResetsDpAfterFiveMinutesOfflineForAdvancedClass()
	{
		var repository = new CapturingEnterWorldRepository
		{
			Player = CreatePlayer(lastOnline: DateTime.Now.AddMinutes(-6), playerClass: "RANGER", dp: 1200),
		};
		var service = CreateService(repository, CreateWorld(), out var registry);

		var result = await service.EnterWorldAsync(accountId: 10, playerObjectId: 1001);

		Assert.Equal(EnterWorldCheckMessage.Ok, result.Message);
		Assert.NotNull(result.Player);
		Assert.Equal(0, result.Player.Dp);
		Assert.True(result.Player.IsOnline);
		Assert.Equal(2, registry.Broadcasts.Count);
		var broadcast = registry.Broadcasts[0];
		Assert.Equal(result.Player.ObjectId, broadcast.SourceObjectId);
		Assert.True(broadcast.IncludeSourcePlayer);
		var dpInfo = Assert.IsType<SmDpInfo>(broadcast.Packet);
		Assert.Equal(result.Player.ObjectId, dpInfo.PlayerObjectId);
		Assert.Equal(0, dpInfo.CurrentDp);
		Assert.IsType<SmEmotion>(registry.Broadcasts[1].Packet);
		Assert.Collection(
			registry.SentPackets,
			delivery =>
			{
				Assert.Equal(result.Player.ObjectId, delivery.PlayerObjectId);
				Assert.IsType<SmStatsInfo>(delivery.Packet);
			},
			delivery =>
			{
				Assert.Equal(result.Player.ObjectId, delivery.PlayerObjectId);
				var dpStat = Assert.IsType<SmStatUpdateDp>(delivery.Packet);
				Assert.Equal(0, dpStat.CurrentDp);
			});
		Assert.Collection(
			registry.PacketOrder,
			packet => Assert.Same(dpInfo, packet),
			packet => Assert.IsType<SmStatsInfo>(packet),
			packet => Assert.IsType<SmEmotion>(packet),
			packet => Assert.IsType<SmStatUpdateDp>(packet));
	}

	[Fact]
	public async Task EnterWorld_KeepsDpForRecentOfflineOrStartingClass()
	{
		var recentRepository = new CapturingEnterWorldRepository
		{
			Player = CreatePlayer(lastOnline: DateTime.Now.AddMinutes(-4), playerClass: "RANGER", dp: 1200),
		};
		var recentService = CreateService(recentRepository, CreateWorld());

		var recent = await recentService.EnterWorldAsync(accountId: 10, playerObjectId: 1001);

		Assert.Equal(EnterWorldCheckMessage.Ok, recent.Message);
		Assert.Equal(1200, recent.Player!.Dp);

		var startingRepository = new CapturingEnterWorldRepository
		{
			Player = CreatePlayer(lastOnline: DateTime.Now.AddMinutes(-6), playerClass: "WARRIOR", dp: 1200),
		};
		var startingService = CreateService(startingRepository, CreateWorld());

		var starting = await startingService.EnterWorldAsync(accountId: 10, playerObjectId: 1001);

		Assert.Equal(EnterWorldCheckMessage.Ok, starting.Message);
		Assert.Equal(1200, starting.Player!.Dp);
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
	public async Task EnterWorld_ClearsCreaturePvpZoneCountersWhenOnlineUpdateRollbackRemovesPlayer()
	{
		var repository = new CapturingEnterWorldRepository
		{
			Player = CreatePlayer(lastOnline: DateTime.Now.AddMinutes(-5)),
			MarkOnlineResult = false,
		};
		var world = CreateWorld();
		var zoneCounterService = new CreaturePvpZoneCounterService();
		zoneCounterService.ApplyZoneEnter(1001, "PVP_87_210040000", CreaturePvpZoneCounterType.Pvp);
		zoneCounterService.ApplyZoneEnter(1001, "FORT_210040000", CreaturePvpZoneCounterType.Siege);
		var service = CreateService(repository, world, creaturePvpZoneCounterService: zoneCounterService);

		var result = await service.EnterWorldAsync(accountId: 10, playerObjectId: 1001);
		var staleLeave = zoneCounterService.ApplyZoneLeave(1001, "PVP_87_210040000", CreaturePvpZoneCounterType.Pvp);

		Assert.Equal(EnterWorldCheckMessage.ConnectionError, result.Message);
		Assert.False(world.TryGetObject(1001, out _));
		Assert.Equal(CreaturePvpZoneCounters.Empty, zoneCounterService.GetCounters(1001));
		Assert.Equal(CreaturePvpZoneMembershipTransitionStatus.NotInside, staleLeave.Status);
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
		SeedPendingQuestionResponses(player);
		var repository = new CapturingEnterWorldRepository { Player = player };
		var world = CreateWorld();
		world.TryAddObject(player.ObjectId, player);
		var service = CreateService(repository, world, out var registry);

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
		Assert.Equal(0, repository.SaveCraftCooldownsCalls);
		Assert.Equal(player.LastOnline, repository.LogoutLastOnline);
		Assert.Equal(0, player.ResponseRequester.Count);
		Assert.Null(player.PendingFriendRequest);
		Assert.Null(player.PendingChargeAllRequest);
		Assert.Null(player.PendingSoulBindRequest);
		Assert.Null(player.PendingRiftPortalRequest);
		Assert.Null(player.PendingKiskBindRequest);
		Assert.Null(player.PendingLeagueInviteRequest);
		Assert.Collection(
			registry.SentPackets.OrderBy(packet => packet.PlayerObjectId),
			delivery =>
			{
				Assert.Equal(player.ObjectId, delivery.PlayerObjectId);
				var message = Assert.IsType<SmSystemMessage>(delivery.Packet);
				Assert.Equal(1300487, message.MessageId);
			},
			delivery =>
			{
				Assert.Equal(2001, delivery.PlayerObjectId);
				Assert.IsType<SmFriendResponse>(delivery.Packet);
			},
			delivery =>
			{
				Assert.Equal(2002, delivery.PlayerObjectId);
				var message = Assert.IsType<SmSystemMessage>(delivery.Packet);
				Assert.Equal(1300190, message.MessageId);
			});
	}

	[Fact]
	public async Task LeaveWorld_RecordsDisabledRepurchaseStateRemovalWithoutMutatingPlayerItems()
	{
		var player = CreatePlayer(lastOnline: DateTime.Now.AddMinutes(-5));
		var repurchaseItem = new RepurchaseSourceItem(
			new InventoryItem
			{
				ObjectId = 7101,
				ItemId = 100000001,
				Count = 1,
				OwnerId = player.ObjectId,
			},
			RepurchasePrice: 1_200);
		player.RepurchaseItems = [repurchaseItem];
		var repository = new CapturingEnterWorldRepository { Player = player };
		var world = CreateWorld();
		world.TryAddObject(player.ObjectId, player);
		var logoutRepurchasePlans = new List<RepurchaseStateRemovePlan>();
		var service = CreateService(
			repository,
			world,
			out _,
			repurchaseStateRemovePlanObserver: logoutRepurchasePlans.Add);

		await service.LeaveWorldAsync(player);

		var plan = Assert.Single(logoutRepurchasePlans);
		Assert.Equal(RepurchaseStateRemovePlanStatus.SnapshotRemoved, plan.Status);
		Assert.Equal(player.ObjectId, plan.PlayerObjectId);
		Assert.Empty(plan.UpdatedSnapshots);
		Assert.True(plan.WouldRemoveMapEntry);
		Assert.False(plan.DidRemoveMapEntry);
		Assert.False(plan.IsLive);
		Assert.Same(repurchaseItem, Assert.Single(player.RepurchaseItems));
		Assert.Contains("RepurchaseService.removeRepurchaseItems", plan.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public async Task LeaveWorld_RecordsNoRepurchaseSnapshotWhenNoPlayerFactsAreAvailable()
	{
		var player = CreatePlayer(lastOnline: DateTime.Now.AddMinutes(-5));
		player.RepurchaseItems = [];
		var repository = new CapturingEnterWorldRepository { Player = player };
		var world = CreateWorld();
		world.TryAddObject(player.ObjectId, player);
		var logoutRepurchasePlans = new List<RepurchaseStateRemovePlan>();
		var service = CreateService(
			repository,
			world,
			out _,
			repurchaseStateRemovePlanObserver: logoutRepurchasePlans.Add);

		await service.LeaveWorldAsync(player);

		var plan = Assert.Single(logoutRepurchasePlans);
		Assert.Equal(RepurchaseStateRemovePlanStatus.NoSnapshot, plan.Status);
		Assert.Equal(player.ObjectId, plan.PlayerObjectId);
		Assert.Empty(plan.UpdatedSnapshots);
		Assert.True(plan.WouldRemoveMapEntry);
		Assert.False(plan.DidRemoveMapEntry);
		Assert.Empty(player.RepurchaseItems);
		Assert.Contains("remove absent player key is a no-op", plan.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public async Task LeaveWorld_RecordsFindGroupLogoutCleanupBeforeQuestionDenyLikeJava()
	{
		var player = CreatePlayer(lastOnline: DateTime.Now.AddMinutes(-5));
		player.IsOnline = true;
		Assert.True(player.ResponseRequester.PutRequest(
			901,
			new QuestionResponseRequest(player.ObjectId, QuestionResponseRequestKind.Unknown)));
		var repository = new CapturingEnterWorldRepository { Player = player };
		var world = CreateWorld();
		world.TryAddObject(player.ObjectId, player);
		var findGroupService = new FindGroupRecruitmentPlanService();
		findGroupService.AddRecruitment(player, "Solo", groupType: 1, nowEpochSeconds: 100);
		findGroupService.AddApplication(player, "Apply", groupType: 2, classId: 5, level: 65, nowEpochSeconds: 101);
		findGroupService.RegisterInstanceGroup(player, instanceMaskId: 0x11223344, message: "Entry", minMembers: 6, nowEpochSeconds: 102);
		var responseCountsObservedByFindGroupCleanup = new List<int>();
		var findGroupCleanupPlans = new List<FindGroupLogoutCleanupPlan>();
		var service = CreateService(
			repository,
			world,
			out _,
			findGroupService: findGroupService,
			findGroupLogoutCleanupPlanObserver: plan =>
			{
				responseCountsObservedByFindGroupCleanup.Add(player.ResponseRequester.Count);
				findGroupCleanupPlans.Add(plan);
			});

		await service.LeaveWorldAsync(player);

		var cleanup = Assert.Single(findGroupCleanupPlans);
		Assert.Equal([1], responseCountsObservedByFindGroupCleanup);
		Assert.Equal(player.ObjectId, cleanup.PlayerObjectId);
		Assert.NotNull(cleanup.RemovedRecruitment);
		Assert.NotNull(cleanup.RemovedApplication);
		Assert.NotNull(cleanup.RemovedInstanceGroup);
		Assert.Empty(cleanup.DirectPacketIntents);
		Assert.False(cleanup.DispatchLiveSideEffects);
		Assert.Empty(findGroupService.ShowRecruitments("ELYOS", nowEpochSeconds: 200).Recruitments);
		Assert.Empty(findGroupService.ShowApplications("ELYOS", nowEpochSeconds: 201).Applications);
		Assert.Empty(findGroupService.ShowInstanceGroups("ELYOS", nowEpochSeconds: 202).InstanceGroups);
		Assert.Equal(0, player.ResponseRequester.Count);
	}

	[Fact]
	public async Task LeaveWorld_UsesInjectedFindGroupLogoutCleanupWithoutObserverLikeJavaSingleton()
	{
		var player = CreatePlayer(lastOnline: DateTime.Now.AddMinutes(-5));
		player.IsOnline = true;
		var repository = new CapturingEnterWorldRepository { Player = player };
		var world = CreateWorld();
		world.TryAddObject(player.ObjectId, player);
		var findGroupService = new FindGroupRecruitmentPlanService();
		findGroupService.AddRecruitment(player, "Solo", groupType: 1, nowEpochSeconds: 100);
		findGroupService.AddApplication(player, "Apply", groupType: 2, classId: 5, level: 65, nowEpochSeconds: 101);
		findGroupService.RegisterInstanceGroup(player, instanceMaskId: 0x11223344, message: "Entry", minMembers: 6, nowEpochSeconds: 102);
		var service = CreateService(
			repository,
			world,
			out _,
			findGroupService: findGroupService);

		await service.LeaveWorldAsync(player);

		Assert.Empty(findGroupService.ShowRecruitments("ELYOS", nowEpochSeconds: 200).Recruitments);
		Assert.Empty(findGroupService.ShowApplications("ELYOS", nowEpochSeconds: 201).Applications);
		Assert.Empty(findGroupService.ShowInstanceGroups("ELYOS", nowEpochSeconds: 202).InstanceGroups);
		Assert.Equal(1, repository.SaveLogoutCalls);
	}

	[Fact]
	public async Task LeaveWorld_RemovesFindGroupStateCreatedByDisabledClientActionPlannerLikeJavaSingleton()
	{
		var player = CreatePlayer(lastOnline: DateTime.Now.AddMinutes(-5));
		player.IsOnline = true;
		var repository = new CapturingEnterWorldRepository { Player = player };
		var world = CreateWorld();
		world.TryAddObject(player.ObjectId, player);
		var findGroupService = new FindGroupRecruitmentPlanService();
		var clientActionPlanner = new FindGroupClientActionPlanService(findGroupService);
		var addRecruitment = clientActionPlanner.Plan(
			player,
			new FindGroupClientAction(Action: 2, Message: "Solo", GroupType: 1),
			nowEpochSeconds: 100);
		var addApplication = clientActionPlanner.Plan(
			player,
			new FindGroupClientAction(Action: 6, Message: "Apply", GroupType: 2, ClassId: 5, Level: 65),
			nowEpochSeconds: 101);
		var registerInstance = clientActionPlanner.Plan(
			player,
			new FindGroupClientAction(Action: 8, InstanceMaskId: 0x11223344, Message: "Entry", MinMembers: 6),
			nowEpochSeconds: 102);
		var service = CreateService(
			repository,
			world,
			out _,
			findGroupService: findGroupService);

		await service.LeaveWorldAsync(player);

		Assert.Equal(FindGroupClientActionPlanKind.AddRecruitment, addRecruitment.Kind);
		Assert.Equal(FindGroupClientActionPlanKind.AddApplication, addApplication.Kind);
		Assert.Equal(FindGroupClientActionPlanKind.RegisterInstanceGroup, registerInstance.Kind);
		Assert.Empty(findGroupService.ShowRecruitments("ELYOS", nowEpochSeconds: 200).Recruitments);
		Assert.Empty(findGroupService.ShowApplications("ELYOS", nowEpochSeconds: 201).Applications);
		Assert.Empty(findGroupService.ShowInstanceGroups("ELYOS", nowEpochSeconds: 202).InstanceGroups);
		Assert.Equal(1, repository.SaveLogoutCalls);
	}

	[Fact]
	public void CreateLogoutCraftCooldownSavePlan_RecordsJavaLogoutStoreBoundaryWithoutWriting()
	{
		var player = CreatePlayer(lastOnline: DateTime.Now.AddMinutes(-5));
		player.CraftCooldowns = new Dictionary<int, long>
		{
			[77] = 2_000,
			[78] = 500,
		};

		var plan = PlayerLogoutCraftCooldownSavePlanService.CreateDisabledPlan(player, currentTimeMillis: 1_000);

		Assert.Equal(PlayerLogoutCraftCooldownSavePlanStatus.DisabledNoWrite, plan.Status);
		Assert.Equal(player.ObjectId, plan.PlayerObjectId);
		Assert.NotNull(plan.PersistencePlan);
		Assert.Equal(CraftCooldownPersistencePlanStatus.DisabledNoWrite, plan.PersistencePlan!.Status);
		Assert.Equal(1, plan.PersistencePlan.InsertDescriptorCount);
		Assert.Equal(1, plan.PersistencePlan.SkippedExpiredCooldownCount);
		Assert.NotNull(plan.AdapterPlan);
		Assert.Equal(CraftCooldownPersistenceAdapterStatus.DisabledNoWrite, plan.AdapterPlan!.Status);
		Assert.True(plan.JavaDeletesBeforeInserts);
		Assert.Equal(2, plan.JavaConnectionOpenCount);
		Assert.Equal(1, plan.JavaStoreOrderAfterPortalCooldowns);
		Assert.Equal(1, plan.JavaStoreOrderBeforeHouseObjectCooldowns);
		Assert.True(plan.JavaSwallowsDeleteSqlExceptions);
		Assert.True(plan.JavaSwallowsInsertSqlExceptions);
		Assert.True(plan.WouldPersistCraftCooldowns);
		Assert.False(plan.DidPersistCraftCooldowns);
		Assert.False(plan.IsLive);
		Assert.Contains("PlayerService.storePlayer calls CraftCooldownsDAO.storeCraftCooldowns", plan.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreateLogoutCraftCooldownSavePlan_DeletesEvenWithoutActiveCraftCooldownsLikeJava()
	{
		var player = CreatePlayer(lastOnline: DateTime.Now.AddMinutes(-5));
		player.CraftCooldowns = new Dictionary<int, long> { [77] = 500 };

		var plan = PlayerLogoutCraftCooldownSavePlanService.CreateDisabledPlan(player, currentTimeMillis: 1_000);

		Assert.Equal(PlayerLogoutCraftCooldownSavePlanStatus.DisabledNoWrite, plan.Status);
		Assert.NotNull(plan.PersistencePlan);
		Assert.Equal(1, plan.PersistencePlan!.DeleteDescriptorCount);
		Assert.Equal(0, plan.PersistencePlan.InsertDescriptorCount);
		Assert.Equal(1, plan.PersistencePlan.SkippedExpiredCooldownCount);
		Assert.Equal(1, plan.JavaConnectionOpenCount);
		Assert.True(plan.WouldPersistCraftCooldowns);
		Assert.False(plan.DidPersistCraftCooldowns);
	}

	[Fact]
	public void CreateLogoutCraftCooldownSavePlan_HandlesMissingPlayerAsNotReady()
	{
		var plan = PlayerLogoutCraftCooldownSavePlanService.CreateDisabledPlan(player: null, currentTimeMillis: 1_000);

		Assert.Equal(PlayerLogoutCraftCooldownSavePlanStatus.PlayerMissing, plan.Status);
		Assert.Null(plan.PersistencePlan);
		Assert.Null(plan.AdapterPlan);
		Assert.False(plan.WouldPersistCraftCooldowns);
		Assert.False(plan.JavaDeletesBeforeInserts);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreateLogoutCraftCooldownLiveReadinessPlan_BlocksWhenConnectionAndErrorDecisionsAreMissing()
	{
		var player = CreatePlayer(lastOnline: DateTime.Now.AddMinutes(-5));
		player.CraftCooldowns = new Dictionary<int, long> { [77] = 2_000 };
		var savePlan = PlayerLogoutCraftCooldownSavePlanService.CreateDisabledPlan(player, currentTimeMillis: 1_000);

		var readiness = PlayerLogoutCraftCooldownLiveReadinessPlanService.CreatePlan(
			savePlan,
			PlayerLogoutCraftCooldownConnectionDecision.Unspecified,
			PlayerLogoutCraftCooldownErrorDecision.Unspecified,
			repositoryMethodAvailable: false,
			logoutSaveHookAvailable: false,
			databaseIntegrationTestAvailable: false);

		Assert.Equal(PlayerLogoutCraftCooldownLiveReadinessStatus.NotReady, readiness.Status);
		Assert.False(readiness.ReadyForLiveRepositoryWiring);
		Assert.Contains(PlayerLogoutCraftCooldownLiveReadinessCriterion.ConnectionBehaviorDecided, readiness.MissingCriteria);
		Assert.Contains(PlayerLogoutCraftCooldownLiveReadinessCriterion.ErrorBehaviorDecided, readiness.MissingCriteria);
		Assert.Contains(PlayerLogoutCraftCooldownLiveReadinessCriterion.RepositoryMethodAvailable, readiness.MissingCriteria);
		Assert.Contains(PlayerLogoutCraftCooldownLiveReadinessCriterion.LogoutSaveHookAvailable, readiness.MissingCriteria);
		Assert.Contains(PlayerLogoutCraftCooldownLiveReadinessCriterion.DatabaseIntegrationTestAvailable, readiness.MissingCriteria);
		Assert.False(readiness.IsLive);
	}

	[Fact]
	public void CreateLogoutCraftCooldownLiveReadinessPlan_AllowsDocumentedIntentionalConnectionDifferenceButStillRequiresWiring()
	{
		var player = CreatePlayer(lastOnline: DateTime.Now.AddMinutes(-5));
		player.CraftCooldowns = new Dictionary<int, long> { [77] = 2_000 };
		var savePlan = PlayerLogoutCraftCooldownSavePlanService.CreateDisabledPlan(player, currentTimeMillis: 1_000);

		var readiness = PlayerLogoutCraftCooldownLiveReadinessPlanService.CreatePlan(
			savePlan,
			PlayerLogoutCraftCooldownConnectionDecision.IntentionalDifferenceReuseLogoutConnectionDocumented,
			PlayerLogoutCraftCooldownErrorDecision.PreserveJavaSwallowSqlExceptionsPerOperation,
			repositoryMethodAvailable: false,
			logoutSaveHookAvailable: false,
			databaseIntegrationTestAvailable: false);

		Assert.Equal(PlayerLogoutCraftCooldownLiveReadinessStatus.NotReady, readiness.Status);
		Assert.DoesNotContain(PlayerLogoutCraftCooldownLiveReadinessCriterion.ConnectionBehaviorDecided, readiness.MissingCriteria);
		Assert.DoesNotContain(PlayerLogoutCraftCooldownLiveReadinessCriterion.ErrorBehaviorDecided, readiness.MissingCriteria);
		Assert.Contains(PlayerLogoutCraftCooldownLiveReadinessCriterion.RepositoryMethodAvailable, readiness.MissingCriteria);
		Assert.Contains(PlayerLogoutCraftCooldownLiveReadinessCriterion.LogoutSaveHookAvailable, readiness.MissingCriteria);
		Assert.Contains(PlayerLogoutCraftCooldownLiveReadinessCriterion.DatabaseIntegrationTestAvailable, readiness.MissingCriteria);
	}

	[Fact]
	public void CreateLogoutCraftCooldownLiveReadinessPlan_IsReadyOnlyWhenEveryGateIsPresent()
	{
		var player = CreatePlayer(lastOnline: DateTime.Now.AddMinutes(-5));
		player.CraftCooldowns = new Dictionary<int, long> { [77] = 2_000 };
		var savePlan = PlayerLogoutCraftCooldownSavePlanService.CreateDisabledPlan(player, currentTimeMillis: 1_000);

		var readiness = PlayerLogoutCraftCooldownLiveReadinessPlanService.CreatePlan(
			savePlan,
			PlayerLogoutCraftCooldownConnectionDecision.PreserveJavaSeparateConnections,
			PlayerLogoutCraftCooldownErrorDecision.PreserveJavaSwallowSqlExceptionsPerOperation,
			repositoryMethodAvailable: true,
			logoutSaveHookAvailable: true,
			databaseIntegrationTestAvailable: true);

		Assert.Equal(PlayerLogoutCraftCooldownLiveReadinessStatus.ReadyForLiveRepositoryWiring, readiness.Status);
		Assert.True(readiness.ReadyForLiveRepositoryWiring);
		Assert.Empty(readiness.MissingCriteria);
		Assert.Equal(PlayerLogoutCraftCooldownConnectionDecision.PreserveJavaSeparateConnections, readiness.ConnectionDecision);
		Assert.Equal(PlayerLogoutCraftCooldownErrorDecision.PreserveJavaSwallowSqlExceptionsPerOperation, readiness.ErrorDecision);
		Assert.False(readiness.IsLive);
		Assert.Contains("CraftCooldownsDAO.storeCraftCooldowns live wiring is gated", readiness.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreateLogoutCraftCooldownRepositoryContractPlan_RecordsFutureInterfaceAndJavaSqlWithoutChangingInterface()
	{
		var plan = PlayerLogoutCraftCooldownRepositoryContractPlanService.CreateDisabledPlan(
			PlayerLogoutCraftCooldownConnectionDecision.PreserveJavaSeparateConnections,
			PlayerLogoutCraftCooldownErrorDecision.PreserveJavaSwallowSqlExceptionsPerOperation);

		Assert.Equal(PlayerLogoutCraftCooldownRepositoryContractPlanStatus.DisabledContractPlanned, plan.Status);
		Assert.Equal("IPlayerEnterWorldRepository", plan.RepositoryInterfaceName);
		Assert.Equal("MySqlPlayerEnterWorldRepository", plan.RepositoryImplementationName);
		Assert.Equal(
			"Task<bool> SavePlayerCraftCooldownsAsync(int playerObjectId, IReadOnlyDictionary<int, long> cooldowns, long? nowMillis = null, CancellationToken cancellationToken = default)",
			plan.MethodSignature);
		Assert.Equal(CraftCooldownPersistencePlanService.JavaCraftCooldownDeleteSql, plan.DeleteSql);
		Assert.Equal(CraftCooldownPersistencePlanService.JavaCraftCooldownInsertSql, plan.InsertSql);
		Assert.Equal("SavedCraftCooldowns", plan.FakeRepositoryCaptureProperty);
		Assert.Equal(
			"SavePlayerCraftCooldownsAsync_ReplacesRowsAndKeepsOnlyActiveCooldownsAgainstJavaSchema_WhenEnabled",
			plan.DatabaseIntegrationTestName);
		Assert.True(plan.ShouldAddInterfaceMethod);
		Assert.False(plan.DidAddInterfaceMethod);
		Assert.True(plan.ShouldAddFakeRepositoryCapture);
		Assert.False(plan.DidAddFakeRepositoryCapture);
		Assert.True(plan.ShouldAddDatabaseIntegrationTest);
		Assert.False(plan.DidAddDatabaseIntegrationTest);
		Assert.True(plan.RequiresSeparateConnectionPerSqlOperation);
		Assert.False(plan.RequiresIntentionalConnectionDifferenceDocumentation);
		Assert.True(plan.RequiresPerOperationSqlExceptionSwallowing);
		Assert.False(plan.RequiresIntentionalErrorDifferenceDocumentation);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreateLogoutCraftCooldownRepositoryContractPlan_RequiresIntentionalDifferenceDocsWhenJavaConnectionOrErrorBehaviorIsNotPreserved()
	{
		var plan = PlayerLogoutCraftCooldownRepositoryContractPlanService.CreateDisabledPlan(
			PlayerLogoutCraftCooldownConnectionDecision.IntentionalDifferenceReuseLogoutConnectionDocumented,
			PlayerLogoutCraftCooldownErrorDecision.IntentionalDifferenceAggregateRepositoryFailureDocumented);

		Assert.Equal(PlayerLogoutCraftCooldownRepositoryContractPlanStatus.DisabledContractPlanned, plan.Status);
		Assert.False(plan.RequiresSeparateConnectionPerSqlOperation);
		Assert.True(plan.RequiresIntentionalConnectionDifferenceDocumentation);
		Assert.False(plan.RequiresPerOperationSqlExceptionSwallowing);
		Assert.True(plan.RequiresIntentionalErrorDifferenceDocumentation);
		Assert.True(plan.ShouldAddInterfaceMethod);
		Assert.False(plan.DidAddInterfaceMethod);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreateLogoutCraftCooldownRepositoryContractPlan_BlocksWhenBehaviorDecisionIsMissing()
	{
		var plan = PlayerLogoutCraftCooldownRepositoryContractPlanService.CreateDisabledPlan(
			PlayerLogoutCraftCooldownConnectionDecision.Unspecified,
			PlayerLogoutCraftCooldownErrorDecision.PreserveJavaSwallowSqlExceptionsPerOperation);

		Assert.Equal(PlayerLogoutCraftCooldownRepositoryContractPlanStatus.MissingBehaviorDecision, plan.Status);
		Assert.False(plan.ShouldAddInterfaceMethod);
		Assert.False(plan.ShouldAddFakeRepositoryCapture);
		Assert.False(plan.ShouldAddDatabaseIntegrationTest);
		Assert.False(plan.RequiresSeparateConnectionPerSqlOperation);
		Assert.False(plan.IsLive);
		Assert.Contains("blocked until connection and SQL error behavior decisions are explicit", plan.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public async Task EmptyRepository_SavePlayerCraftCooldownsAsync_CapturesDisabledFakeStateWithoutSql()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		var cooldowns = new Dictionary<int, long> { [77] = 2_000 };

		var saved = await repository.SavePlayerCraftCooldownsAsync(1001, cooldowns, nowMillis: 1_000);

		Assert.True(saved);
		Assert.Same(cooldowns, repository.SavedCraftCooldowns);
		Assert.Equal(1_000, repository.SavedCraftCooldownsNowMillis);
	}

	[Fact]
	public async Task CapturingRepository_SavePlayerCraftCooldownsAsync_CapturesFutureInterfaceCallWithoutLogoutHook()
	{
		var repository = new CapturingEnterWorldRepository();
		var cooldowns = new Dictionary<int, long> { [77] = 2_000 };

		var saved = await repository.SavePlayerCraftCooldownsAsync(1001, cooldowns, nowMillis: 1_000);

		Assert.True(saved);
		Assert.Equal(1, repository.SaveCraftCooldownsCalls);
		Assert.Same(cooldowns, repository.SavedCraftCooldowns);
		Assert.Equal(1_000, repository.SaveCraftCooldownsNowMillis);
	}

	[Fact]
	public async Task LeaveWorld_PersistsAcceptedStorageExpansionFields()
	{
		var player = CreatePlayer(lastOnline: DateTime.Now.AddMinutes(-5));
		player.WarehouseNpcExpands = 0;
		player.WarehouseBonusExpands = 0;
		player.InventoryItems =
		[
			new InventoryItem
			{
				ObjectId = 5001,
				ItemId = KinahItemId,
				Count = 10_000,
				Location = 0,
			},
		];
		var expansionService = new StorageExpansionNpcService();
		expansionService.RequestCubeExpansion(
			player,
			CreateExpansionNpc(798008),
			CreateStorageExpansionTemplate(level: 1, price: 1000),
			cubeExpansionLimit: 11,
			npcCubeExpandsSizeLimit: 5);
		expansionService.HandleResponse(
			player,
			SmQuestionWindow.WarehouseExpandWarning,
			response: 1,
			CreateItemTemplates(KinahItemId));
		expansionService.RequestWarehouseExpansion(
			player,
			CreateExpansionNpc(203199),
			CreateStorageExpansionTemplate(level: 1, price: 1200));
		expansionService.HandleResponse(
			player,
			SmQuestionWindow.WarehouseExpandWarning,
			response: 1,
			CreateItemTemplates(KinahItemId));
		var repository = new CapturingEnterWorldRepository { Player = player };
		var world = CreateWorld();
		world.TryAddObject(player.ObjectId, player);
		var service = CreateService(repository, world);

		await service.LeaveWorldAsync(player);

		Assert.Equal(1, repository.SaveLogoutCalls);
		Assert.NotNull(repository.SavedLogoutPlayer);
		Assert.Equal(1, repository.SavedLogoutPlayer!.NpcExpands);
		Assert.Equal(1, repository.SavedLogoutPlayer.WarehouseNpcExpands);
		Assert.Equal(7_800, repository.SavedLogoutPlayer.InventoryItems.Single(item => item.ItemId == KinahItemId).Count);
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

	[Fact]
	public async Task SavePowerShardUseMutation_FlattensUseResultsForRepository()
	{
		var player = CreatePlayer();
		var repository = new CapturingEnterWorldRepository { Player = player };
		var service = CreateService(repository, CreateWorld());
		var countUpdate = new InventoryItem { ObjectId = 10, ItemId = 169000005, Count = 2 };
		var equipUpdate = new InventoryItem { ObjectId = 11, ItemId = 169000005, IsEquipped = true, Slot = 1L << 13 };
		var firstUse = new PowerShardUseResult(
			Changed: true,
			InventoryItems: Array.Empty<InventoryItem>(),
			CountUpdateItems: [countUpdate],
			EquipUpdateItems: Array.Empty<InventoryItem>(),
			DeletedItemObjectIds: [12],
			PowerShardDeactivated: false,
			MarksEquipmentPersistentState: true);
		var secondUse = new PowerShardUseResult(
			Changed: true,
			InventoryItems: Array.Empty<InventoryItem>(),
			CountUpdateItems: Array.Empty<InventoryItem>(),
			EquipUpdateItems: [equipUpdate],
			DeletedItemObjectIds: [12],
			PowerShardDeactivated: false,
			MarksEquipmentPersistentState: true);

		var saved = await service.SavePowerShardUseMutationAsync(player, [firstUse, secondUse]);

		Assert.True(saved);
		Assert.Equal(1, repository.SavePowerShardUseMutationCalls);
		Assert.Equal([10], repository.PowerShardCountUpdateItems.Select(item => item.ObjectId));
		Assert.Equal([11], repository.PowerShardEquipUpdateItems.Select(item => item.ObjectId));
		Assert.Equal([12], repository.PowerShardDeletedItemObjectIds);
	}

	[Fact]
	public async Task SavePortalRequirementConsumptionMutation_PersistsUpdatedAndDeletedRows()
	{
		var player = CreatePlayer();
		var repository = new CapturingEnterWorldRepository { Player = player };
		var service = CreateService(repository, CreateWorld());
		var updatedItem = new InventoryItem { ObjectId = 10, ItemId = 185000077, Count = 1 };
		var kinahUpdate = new InventoryItem { ObjectId = 11, ItemId = 182400001, Count = 500 };
		var application = PortalRequirementConsumptionApplication.Success(
			inventoryItems: [updatedItem, kinahUpdate],
			packets: Array.Empty<GameServerPacket>(),
			updatedItems: [updatedItem, kinahUpdate],
			deletedObjectIds: [12]);

		var saved = await service.SavePortalRequirementConsumptionMutationAsync(player, application);

		Assert.True(saved);
		Assert.Equal(1, repository.SaveAssemblyItemActionMutationCalls);
		Assert.Equal([10, 11], repository.AssemblyUpdatedPartItems.Select(item => item.ObjectId));
		Assert.Equal([12], repository.AssemblyDeletedPartObjectIds);
		Assert.Empty(repository.AssemblyUpdatedRewardItems);
		Assert.Empty(repository.AssemblyAddedRewardItems);
	}

	[Fact]
	public async Task SavePortalRequirementConsumptionMutation_SkipsRepositoryWhenNoRowsChanged()
	{
		var player = CreatePlayer();
		var repository = new CapturingEnterWorldRepository { Player = player };
		var service = CreateService(repository, CreateWorld());
		var application = PortalRequirementConsumptionApplication.Success(
			inventoryItems: Array.Empty<InventoryItem>(),
			packets: Array.Empty<GameServerPacket>(),
			updatedItems: Array.Empty<InventoryItem>(),
			deletedObjectIds: Array.Empty<int>());

		var saved = await service.SavePortalRequirementConsumptionMutationAsync(player, application);

		Assert.True(saved);
		Assert.Equal(0, repository.SaveAssemblyItemActionMutationCalls);
	}

	[Fact]
	public async Task SavePortalRequirementConsumptionMutation_RejectsUnappliedApplication()
	{
		var player = CreatePlayer();
		var repository = new CapturingEnterWorldRepository { Player = player };
		var service = CreateService(repository, CreateWorld());
		var application = PortalRequirementConsumptionApplication.NotApplied(player.InventoryItems);

		var saved = await service.SavePortalRequirementConsumptionMutationAsync(player, application);

		Assert.False(saved);
		Assert.Equal(0, repository.SaveAssemblyItemActionMutationCalls);
	}

	[Fact]
	public async Task PreparePortalEntry_PersistsRequirementsAndReturnsPacketsWithoutTeleporting()
	{
		var player = CreatePlayer();
		player.Level = 25;
		player.InventoryItems =
		[
			new InventoryItem { ObjectId = 10, ItemId = 185000077, Count = 1, Location = 0 },
			new InventoryItem { ObjectId = 11, ItemId = 185000077, Count = 3, Location = 0 },
			new InventoryItem { ObjectId = 12, ItemId = KinahItemId, Count = 1_000, Location = 0 },
		];
		player.Position = new WorldPosition(PortalWorldId, 10, 20, 30, 40, InstanceId: 7);
		var repository = new CapturingEnterWorldRepository { Player = player };
		var service = CreateService(repository, CreateWorld());

		var result = await service.PreparePortalEntryAsync(
			player,
			CreatePortalPath(kinah: 500, itemRequirements: [new PortalItemRequirementSummary(185000077, 3)]),
			CreatePortalLocs(),
			CreatePortalCooltimes(maxPlayers: 0),
			CreateWorldMaps(),
			CreateItemTemplates(185000077, KinahItemId),
			DateTimeOffset.FromUnixTimeMilliseconds(100_000),
			npcObjectId: 4001);

		Assert.True(result.CanEnter);
		Assert.Equal(PortalEntryPreparationStatus.Ready, result.Status);
		Assert.Equal(PortalEntryPlanAction.SameInstanceTeleport, result.EntryPlan.Action);
		Assert.NotNull(result.RequirementApplication);
		Assert.Equal(4, result.Packets.Count);
		Assert.Equal([11, 12], player.InventoryItems.Select(item => item.ObjectId).ToArray());
		Assert.Contains(player.InventoryItems, item => item.ObjectId == 11 && item.Count == 1);
		Assert.Contains(player.InventoryItems, item => item.ObjectId == 12 && item.Count == 500);
		Assert.Equal(1, repository.SaveAssemblyItemActionMutationCalls);
		Assert.Equal([11, 12], repository.AssemblyUpdatedPartItems.Select(item => item.ObjectId));
		Assert.Equal([10], repository.AssemblyDeletedPartObjectIds);
	}

	[Fact]
	public async Task PreparePortalEntry_ReturnsValidationFailureWithoutRequirementPersistence()
	{
		var player = CreatePlayer();
		var repository = new CapturingEnterWorldRepository { Player = player };
		var service = CreateService(repository, CreateWorld());

		var result = await service.PreparePortalEntryAsync(
			player,
			CreatePortalPath(),
			new PortalLocTable([]),
			CreatePortalCooltimes(maxPlayers: 0),
			CreateWorldMaps(),
			CreateItemTemplates(),
			DateTimeOffset.FromUnixTimeMilliseconds(100_000),
			npcObjectId: 4001);

		Assert.False(result.CanEnter);
		Assert.Equal(PortalEntryPreparationStatus.ValidationRejected, result.Status);
		Assert.Equal(PortalEntryValidationStatus.MissingPortalLocation, result.EntryPlan.Status);
		Assert.Null(result.RequirementApplication);
		Assert.Empty(result.Packets);
		Assert.Equal(0, repository.SaveAssemblyItemActionMutationCalls);
	}

	[Fact]
	public async Task PreparePortalEntry_SkipsRequirementConsumptionForJavaReentry()
	{
		var player = CreatePlayer();
		player.Level = 25;
		player.Position = new WorldPosition(210010000, 10, 20, 30, 40, InstanceId: 1);
		var repository = new CapturingEnterWorldRepository { Player = player };
		var service = CreateService(repository, CreateWorld());
		var worldMaps = CreateWorldMapsWithRegisteredSoloInstance(player.ObjectId);

		var result = await service.PreparePortalEntryAsync(
			player,
			CreatePortalPath(kinah: 1, itemRequirements: [new PortalItemRequirementSummary(185000077, 1)]),
			CreatePortalLocs(),
			CreatePortalCooltimes(maxPlayers: 1),
			worldMaps,
			CreateItemTemplates(185000077, KinahItemId),
			DateTimeOffset.FromUnixTimeMilliseconds(100_000),
			npcObjectId: 4001);

		Assert.True(result.CanEnter);
		Assert.Equal(PortalEntryPreparationStatus.Ready, result.Status);
		Assert.True(result.EntryPlan.Reenter);
		Assert.Null(result.RequirementApplication);
		Assert.Empty(result.Packets);
		Assert.Equal(0, repository.SaveAssemblyItemActionMutationCalls);
	}

	[Fact]
	public async Task SaveIdianPolishBurnMutation_PersistsOnlyExhaustedBurnDeletes()
	{
		var player = CreatePlayer();
		var repository = new CapturingEnterWorldRepository { Player = player };
		var service = CreateService(repository, CreateWorld());
		var lowChargeUpdate = new InventoryItem
		{
			ObjectId = 20,
			ItemId = 100000094,
			IdianStone = new PlayerIdianStone(166050001, 1, 250_000),
		};
		var exhaustedUpdate = new InventoryItem
		{
			ObjectId = 21,
			ItemId = 100000095,
			IdianStone = null,
		};
		var plan = new IdianPolishBurnPlan(
			Changed: true,
			InventoryItems: [lowChargeUpdate, exhaustedUpdate],
			Burns:
			[
				new IdianPolishBurnResult(lowChargeUpdate, IdianPolishBurnUpdateKind.LowCharge, 100_000),
				new IdianPolishBurnResult(exhaustedUpdate, IdianPolishBurnUpdateKind.Exhausted, 250_000),
			]);

		var saved = await service.SaveIdianPolishBurnMutationAsync(player, plan);

		Assert.True(saved);
		Assert.Equal(1, repository.SaveIdianPolishBurnMutationCalls);
		Assert.Equal([21], repository.IdianPolishBurnItemUpdates.Select(item => item.ObjectId));
	}

	[Fact]
	public async Task SaveIdianPolishBurnMutation_SkipsRepositoryWhenNothingExhausted()
	{
		var player = CreatePlayer();
		var repository = new CapturingEnterWorldRepository { Player = player };
		var service = CreateService(repository, CreateWorld());
		var lowChargeUpdate = new InventoryItem
		{
			ObjectId = 20,
			ItemId = 100000094,
			IdianStone = new PlayerIdianStone(166050001, 1, 250_000),
		};
		var plan = new IdianPolishBurnPlan(
			Changed: true,
			InventoryItems: [lowChargeUpdate],
			Burns: [new IdianPolishBurnResult(lowChargeUpdate, IdianPolishBurnUpdateKind.LowCharge, 100_000)]);

		var saved = await service.SaveIdianPolishBurnMutationAsync(player, plan);

		Assert.True(saved);
		Assert.Equal(0, repository.SaveIdianPolishBurnMutationCalls);
	}

	[Fact]
	public async Task SaveItemChargeBurnMutation_PersistsAllObserverChargeUpdates()
	{
		var player = CreatePlayer();
		var repository = new CapturingEnterWorldRepository { Player = player };
		var service = CreateService(repository, CreateWorld());
		var firstUpdate = new InventoryItem { ObjectId = 20, ItemId = 100000094, Charge = 99_900 };
		var secondUpdate = new InventoryItem { ObjectId = 21, ItemId = 100000095, Charge = 119_800 };
		var plan = new ItemChargeBurnPlan(
			Changed: true,
			InventoryItems: [firstUpdate, secondUpdate],
			Burns:
			[
				new ItemChargeUpdateResult(firstUpdate, ChargeBarChanged: true, PointsDelta: -200),
				new ItemChargeUpdateResult(secondUpdate, ChargeBarChanged: false, PointsDelta: -200),
			]);

		var saved = await service.SaveItemChargeBurnMutationAsync(player, plan);

		Assert.True(saved);
		Assert.Equal(1, repository.SaveItemChargeBurnMutationCalls);
		Assert.Equal([20, 21], repository.ChargeBurnChargedItems.Select(item => item.ObjectId));
		Assert.Equal([99_900, 119_800], repository.ChargeBurnChargedItems.Select(item => item.Charge));
	}

	[Fact]
	public async Task SaveItemChargeBurnMutation_SkipsRepositoryWhenNothingChanged()
	{
		var player = CreatePlayer();
		var repository = new CapturingEnterWorldRepository { Player = player };
		var service = CreateService(repository, CreateWorld());
		var plan = ItemChargeBurnPlan.NoChange();

		var saved = await service.SaveItemChargeBurnMutationAsync(player, plan);

		Assert.True(saved);
		Assert.Equal(0, repository.SaveItemChargeBurnMutationCalls);
	}

	private static PlayerEnterWorldService CreateService(CapturingEnterWorldRepository repository, GameWorld world)
	{
		return CreateService(repository, world, out _);
	}

	private static PlayerEnterWorldService CreateService(
		CapturingEnterWorldRepository repository,
		GameWorld world,
		GameServerRuntimeContext runtimeContext)
	{
		return CreateService(repository, world, out _, runtimeContext: runtimeContext);
	}

	private static PlayerEnterWorldService CreateService(
		CapturingEnterWorldRepository repository,
		GameWorld world,
		out CapturingConnectionRegistry registry,
		CreaturePvpZoneCounterService? creaturePvpZoneCounterService = null,
		GameServerRuntimeContext? runtimeContext = null,
		Action<RepurchaseStateRemovePlan>? repurchaseStateRemovePlanObserver = null,
		FindGroupRecruitmentPlanService? findGroupService = null,
		Action<FindGroupLogoutCleanupPlan>? findGroupLogoutCleanupPlanObserver = null)
	{
		registry = new CapturingConnectionRegistry();
		var resourceStats = new WorldNpcResourceStatsService(
			new WorldNpcLifeStatsService(new WorldNpcDeathDropWorkflowService(null!, null!)),
			registry,
			new PlayerVisualStatsUpdateService(registry));
		return new PlayerEnterWorldService(
			new GameServerOptions(),
			repository,
			world,
			NullLogger<PlayerEnterWorldService>.Instance,
			resourceStats,
			creaturePvpZoneCounterService,
			registry,
			runtimeContext,
			repurchaseStateRemovePlanObserver,
			findGroupService,
			findGroupLogoutCleanupPlanObserver);
	}

	private static PlayerEnterWorldService CreateService(
		CapturingEnterWorldRepository repository,
		GameWorld world,
		CreaturePvpZoneCounterService creaturePvpZoneCounterService)
	{
		return CreateService(repository, world, out _, creaturePvpZoneCounterService);
	}

	private static GameWorld CreateWorld()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		world.Initialize();
		return world;
	}

	private static string FindRepoRoot()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory != null)
		{
			if (File.Exists(Path.Combine(directory.FullName, "game-server", "data", "static_data", "static_data.xml")))
				return directory.FullName;
			directory = directory.Parent;
		}

		throw new FileNotFoundException("Could not find repository root from test output directory.");
	}

	private static Player CreatePlayer(
		bool isOnline = false,
		DateTime? lastOnline = null,
		string playerClass = "WARRIOR",
		int dp = 0)
	{
		return new Player
		{
			ObjectId = 1001,
			AccountId = 10,
			Name = "Character",
			PlayerClass = playerClass,
			Race = "ELYOS",
			Gender = "MALE",
			WarehouseNpcExpands = 1,
			WarehouseBonusExpands = 2,
			IsOnline = isOnline,
			LastOnline = lastOnline,
			Dp = dp,
			Position = new WorldPosition(210010000, 1, 2, 3, 32),
		};
	}

	private static void SeedPendingQuestionResponses(Player player)
	{
		player.PendingFriendRequest = new PendingFriendRequest(2001, "Requester");
		player.PendingChargeAllRequest = new PendingChargeAllRequest(
			SenderObjectId: 7001,
			ChargeWay: 1,
			PaymentAmount: 0,
			Items:
			[
				new PendingChargeAllItem(9001, 100, PreviousCharge: 0, TargetCharge: 10000, Level: 2)
			]);
		player.PendingSoulBindRequest = new PendingSoulBindRequest(9002, 1, "Practice Sword");
		player.PendingRiftPortalRequest = new PendingRiftPortalRequest(9003, SmQuestionWindow.DirectPortalPassConfirm);
		player.PendingKiskBindRequest = new PendingKiskBindRequest(9004, SmQuestionWindow.RegisterBindstone);
		player.PendingLeagueInviteRequest = new PendingLeagueInviteRequest(
			QuestionId: SmQuestionWindow.UnionInviteMe,
			RequesterObjectId: 2002,
			RequestTargetObjectId: player.ObjectId,
			SelectedPlayerObjectId: player.ObjectId,
			InvitedAllianceId: 3002);

		Assert.True(player.ResponseRequester.PutRequest(
			SmQuestionWindow.BuddyListAddBuddyRequest,
			new QuestionResponseRequest(2001, QuestionResponseRequestKind.FriendInvite, player.PendingFriendRequest)));
		Assert.True(player.ResponseRequester.PutRequest(
			SmQuestionWindow.ItemChargeAllConfirm,
			new QuestionResponseRequest(player.ObjectId, QuestionResponseRequestKind.ChargeAll, player.PendingChargeAllRequest)));
		Assert.True(player.ResponseRequester.PutRequest(
			SmQuestionWindow.SoulBoundItemConfirm,
			new QuestionResponseRequest(player.ObjectId, QuestionResponseRequestKind.SoulBind, player.PendingSoulBindRequest)));
		Assert.True(player.ResponseRequester.PutRequest(
			SmQuestionWindow.DirectPortalPassConfirm,
			new QuestionResponseRequest(9003, QuestionResponseRequestKind.RiftPortal, player.PendingRiftPortalRequest)));
		Assert.True(player.ResponseRequester.PutRequest(
			SmQuestionWindow.RegisterBindstone,
			new QuestionResponseRequest(9004, QuestionResponseRequestKind.KiskBind, player.PendingKiskBindRequest)));
		Assert.True(player.ResponseRequester.PutRequest(
			SmQuestionWindow.UnionInviteMe,
			new QuestionResponseRequest(2002, QuestionResponseRequestKind.LeagueInvite, player.PendingLeagueInviteRequest)));
		Assert.Equal(6, player.ResponseRequester.Count);
	}

	private const int PortalWorldId = 300030000;
	private const int KinahItemId = 182400001;

	private static PortalPathSummary CreatePortalPath(
		int kinah = 0,
		IReadOnlyList<PortalItemRequirementSummary>? itemRequirements = null)
	{
		return new PortalPathSummary(
			PortalPathSource.Dialog,
			NpcId: 730000,
			ScrollName: string.Empty,
			Dialog: 10000,
			LocId: PortalWorldId / 100,
			SiegeId: 0,
			Race: "PC_ALL",
			MinLevel: 0,
			MinRank: 0,
			Kinah: kinah,
			TitleId: 0,
			ErrGroup: 0,
			ErrLevel: 0)
		{
			ItemRequirements = itemRequirements ?? Array.Empty<PortalItemRequirementSummary>(),
		};
	}

	private static PortalLocTable CreatePortalLocs()
	{
		return new PortalLocTable([new PortalLocSummary(PortalWorldId, PortalWorldId / 100, X: 1, Y: 2, Z: 3, Heading: 4)]);
	}

	private static InstanceCooltimeTable CreatePortalCooltimes(int maxPlayers)
	{
		return new InstanceCooltimeTable(
		[
			new InstanceCooltimeSummary(
				8,
				PortalWorldId,
				"PC_ALL",
				MaxCount: 1,
				MaxMemberLight: maxPlayers,
				MaxMemberDark: maxPlayers,
				EnterMinLevelLight: 25,
				EnterMinLevelDark: 25),
		]);
	}

	private static WorldMapRuntimeStateTable CreateWorldMaps()
	{
		return new WorldMapRuntimeStateTable([new WorldMapSummary(PortalWorldId, IsInstance: true, TwinCount: 1)]);
	}

	private static WorldMapRuntimeStateTable CreateWorldMapsWithRegisteredSoloInstance(int playerObjectId)
	{
		var worldMaps = CreateWorldMaps();
		var instance = worldMaps.AddWorldMapInstance(PortalWorldId, instanceId: 2, ownerId: 0, maxPlayers: 1)!;
		instance.Register(playerObjectId);
		return worldMaps;
	}

	private static ItemTemplateTable CreateItemTemplates(params int[] itemIds)
	{
		return new ItemTemplateTable(itemIds.Select(CreateItemTemplate).ToArray());
	}

	private static WorldNpc CreateExpansionNpc(int templateId)
	{
		return new WorldNpc(
			9001,
			templateId,
			new NpcTemplateSummary(
				templateId,
				"Expansion Master",
				123456,
				1,
				"NORMAL",
				"NORMAL",
				"PC_ALL",
				string.Empty,
				"NPC",
				FunctionDialogIds: [47],
				HasTalkInfo: true,
				IsDialogNpc: true),
			new WorldPosition(210010000, 1, 2, 3, 0));
	}

	private static StorageExpansionTemplateSummary CreateStorageExpansionTemplate(int level, int price)
	{
		return new StorageExpansionTemplateSummary([1], [new StorageExpansionPrice(level, price)]);
	}

	private static ItemTemplateSummary CreateItemTemplate(int itemId)
	{
		return new ItemTemplateSummary(
			itemId,
			itemId == KinahItemId ? "Kinah" : "Portal Item",
			DescriptionId: itemId == KinahItemId ? 12350 : 20001,
			Mask: 0,
			Level: 1,
			ItemGroup: itemId == KinahItemId ? "NONE" : "NORMAL",
			ItemType: "NORMAL",
			Quality: "COMMON",
			Race: "PC_ALL",
			MaxStackCount: 1000,
			Price: 0,
			ValidEquipmentSlots: 0);
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

		public PlayerNpcFactionsSnapshot NpcFactions { get; init; } = PlayerNpcFactionsSnapshot.Empty;

		public IReadOnlyList<PlayerTitle> Titles { get; init; } = Array.Empty<PlayerTitle>();

		public IReadOnlyList<PlayerMotion> Motions { get; init; } = Array.Empty<PlayerMotion>();

		public IReadOnlyList<PlayerEmotion> Emotions { get; init; } = Array.Empty<PlayerEmotion>();

		public IReadOnlyList<int> Recipes { get; init; } = Array.Empty<int>();

		public IReadOnlyList<PlayerMacro> Macros { get; init; } = Array.Empty<PlayerMacro>();

		public IReadOnlyList<PlayerMail> Mailbox { get; init; } = Array.Empty<PlayerMail>();

		public PlayerBrokerSettlementSummary BrokerSettlements { get; init; } = PlayerBrokerSettlementSummary.Empty;

		public IReadOnlyList<PlayerHouse> Houses { get; init; } = Array.Empty<PlayerHouse>();

		public IReadOnlyDictionary<int, long> CraftCooldowns { get; init; } = new Dictionary<int, long>();

		public IReadOnlyDictionary<int, long> HouseObjectCooldowns { get; init; } = new Dictionary<int, long>();

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

		public int LoadNpcFactionsCalls { get; private set; }

		public NpcFactionTable? NpcFactionTable { get; private set; }

		public int NpcFactionCurrentEpochSeconds { get; private set; }

		public int LoadTitlesCalls { get; private set; }

		public int LoadMotionsCalls { get; private set; }

		public int LoadEmotionsCalls { get; private set; }

		public int LoadRecipesCalls { get; private set; }

		public int LoadMacrosCalls { get; private set; }

		public int LoadMailboxCalls { get; private set; }

		public int LoadBrokerSettlementsCalls { get; private set; }

		public int LoadHousesCalls { get; private set; }

		public int LoadCraftCooldownsCalls { get; private set; }

		public int LoadHouseObjectCooldownsCalls { get; private set; }

		public int LoadPortalCooldownsCalls { get; private set; }

		public int SavePortalCooldownsCalls { get; private set; }

		public IReadOnlyDictionary<int, PlayerPortalCooldown>? SavedPortalCooldowns { get; private set; }

		public long? SavePortalCooldownsNowMillis { get; private set; }

		public int SaveCraftCooldownsCalls { get; private set; }

		public IReadOnlyDictionary<int, long>? SavedCraftCooldowns { get; private set; }

		public long? SaveCraftCooldownsNowMillis { get; private set; }

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

		public int DeleteEmotionCalls { get; private set; }

		public int DeletedEmotionId { get; private set; }

		public int DeleteTitleCalls { get; private set; }

		public int DeletedTitleId { get; private set; }

		public int DeleteMotionCalls { get; private set; }

		public int DeletedMotionId { get; private set; }

		public int SaveItemUseSourceMutationCalls { get; private set; }

		public InventoryItem? ItemUseSourceItemUpdate { get; private set; }

		public int? ItemUseDeletedSourceItemObjectId { get; private set; }

		public int SaveCraftLearnActionMutationCalls { get; private set; }

		public int CraftLearnRecipeId { get; private set; }

		public InventoryItem? CraftLearnSourceItemUpdate { get; private set; }

		public int? CraftLearnDeletedSourceItemObjectId { get; private set; }

		public int SaveEmotionLearnActionMutationCalls { get; private set; }

		public PlayerEmotion? LearnedEmotion { get; private set; }

		public InventoryItem? EmotionLearnSourceItemUpdate { get; private set; }

		public int? EmotionLearnDeletedSourceItemObjectId { get; private set; }

		public int SaveTitleAddActionMutationCalls { get; private set; }

		public PlayerTitle? AddedTitle { get; private set; }

		public InventoryItem? TitleAddSourceItemUpdate { get; private set; }

		public int? TitleAddDeletedSourceItemObjectId { get; private set; }

		public int SaveSkillLearnActionMutationCalls { get; private set; }

		public IReadOnlyList<PlayerSkill> LearnedSkills { get; private set; } = Array.Empty<PlayerSkill>();

		public InventoryItem? SkillLearnSourceItemUpdate { get; private set; }

		public int? SkillLearnDeletedSourceItemObjectId { get; private set; }

		public int SaveInventoryExpansionMutationCalls { get; private set; }

		public int InventoryExpansionItemExpands { get; private set; }

		public int InventoryExpansionWarehouseBonusExpands { get; private set; }

		public InventoryItem? InventoryExpansionSourceItemUpdate { get; private set; }

		public int? InventoryExpansionDeletedSourceItemObjectId { get; private set; }

		public int SaveDyeItemActionMutationCalls { get; private set; }

		public InventoryItem? DyeTargetItemUpdate { get; private set; }

		public InventoryItem? DyeSourceItemUpdate { get; private set; }

		public int? DyeDeletedSourceItemObjectId { get; private set; }

		public int SaveAnimationAddActionMutationCalls { get; private set; }

		public IReadOnlyList<PlayerMotion> AnimationAddedMotions { get; private set; } = Array.Empty<PlayerMotion>();

		public IReadOnlyList<int> AnimationDeactivatedMotionIds { get; private set; } = Array.Empty<int>();

		public InventoryItem? AnimationSourceItemUpdate { get; private set; }

		public int? AnimationDeletedSourceItemObjectId { get; private set; }

		public int SaveCosmeticItemActionMutationCalls { get; private set; }

		public Aion.GameServer.Model.Account.CharacterAppearance? CosmeticAppearance { get; private set; }

		public int CosmeticDeletedItemObjectId { get; private set; }

		public int SaveDecomposeActionMutationCalls { get; private set; }

		public IReadOnlyList<InventoryItem> DecomposeUpdatedItems { get; private set; } = Array.Empty<InventoryItem>();

		public IReadOnlyList<InventoryItem> DecomposeAddedItems { get; private set; } = Array.Empty<InventoryItem>();

		public InventoryItem? DecomposeSourceItemUpdate { get; private set; }

		public int? DecomposeDeletedSourceItemObjectId { get; private set; }

		public int SaveAssemblyItemActionMutationCalls { get; private set; }

		public IReadOnlyList<InventoryItem> AssemblyUpdatedPartItems { get; private set; } = Array.Empty<InventoryItem>();

		public IReadOnlyList<int> AssemblyDeletedPartObjectIds { get; private set; } = Array.Empty<int>();

		public IReadOnlyList<InventoryItem> AssemblyUpdatedRewardItems { get; private set; } = Array.Empty<InventoryItem>();

		public IReadOnlyList<InventoryItem> AssemblyAddedRewardItems { get; private set; } = Array.Empty<InventoryItem>();

		public int SaveExpExtractActionMutationCalls { get; private set; }

		public long ExpExtractNewExp { get; private set; }

		public InventoryItem? ExpExtractSourceItemUpdate { get; private set; }

		public int? ExpExtractDeletedSourceItemObjectId { get; private set; }

		public IReadOnlyList<InventoryItem> ExpExtractUpdatedRewardItems { get; private set; } = Array.Empty<InventoryItem>();

		public IReadOnlyList<InventoryItem> ExpExtractAddedRewardItems { get; private set; } = Array.Empty<InventoryItem>();

		public int SaveApExtractActionMutationCalls { get; private set; }

		public PlayerAbyssRank? ApExtractAbyssRank { get; private set; }

		public InventoryItem? ApExtractSourceItemUpdate { get; private set; }

		public int? ApExtractDeletedSourceItemObjectId { get; private set; }

		public int ApExtractDeletedTargetItemObjectId { get; private set; }

		public int SaveItemPurificationMutationCalls { get; private set; }

		public IReadOnlyList<InventoryItem> ItemPurificationMaterialItemUpdates { get; private set; } = Array.Empty<InventoryItem>();

		public IReadOnlyList<int> ItemPurificationDeletedMaterialItemObjectIds { get; private set; } = Array.Empty<int>();

		public InventoryItem? ItemPurificationBaseItemUpdate { get; private set; }

		public int? ItemPurificationDeletedBaseItemObjectId { get; private set; }

		public IReadOnlyList<InventoryItem> ItemPurificationUpdatedTargetItems { get; private set; } = Array.Empty<InventoryItem>();

		public IReadOnlyList<InventoryItem> ItemPurificationAddedTargetItems { get; private set; } = Array.Empty<InventoryItem>();

		public PlayerAbyssRank? ItemPurificationAbyssRank { get; private set; }

		public int SaveItemRemodelMutationCalls { get; private set; }

		public InventoryItem? RemodelTargetItemUpdate { get; private set; }

		public InventoryItem? RemodelKinahItemUpdate { get; private set; }

		public InventoryItem? RemodelExtractItemUpdate { get; private set; }

		public int? RemodelDeletedExtractItemObjectId { get; private set; }

		public int SaveItemChargeMutationCalls { get; private set; }

		public InventoryItem? ChargedItem { get; private set; }

		public InventoryItem? ChargePaymentKinahItem { get; private set; }

		public PlayerAbyssRank? ChargePaymentAbyssRank { get; private set; }

		public int SaveItemChargeAllMutationCalls { get; private set; }

		public IReadOnlyList<InventoryItem> ChargeAllChargedItems { get; private set; } = Array.Empty<InventoryItem>();

		public InventoryItem? ChargeAllPaymentKinahItem { get; private set; }

		public PlayerAbyssRank? ChargeAllPaymentAbyssRank { get; private set; }

		public int SaveItemChargeBurnMutationCalls { get; private set; }

		public IReadOnlyList<InventoryItem> ChargeBurnChargedItems { get; private set; } = Array.Empty<InventoryItem>();

		public int SaveIdianPolishMutationCalls { get; private set; }

		public InventoryItem? IdianPolishTargetItem { get; private set; }

		public InventoryItem? IdianPolishSourceItemUpdate { get; private set; }

		public int? IdianPolishDeletedSourceItemObjectId { get; private set; }

		public int SaveIdianPolishBurnMutationCalls { get; private set; }

		public IReadOnlyList<InventoryItem> IdianPolishBurnItemUpdates { get; private set; } = Array.Empty<InventoryItem>();

		public int SaveItemChargeActionMutationCalls { get; private set; }

		public IReadOnlyList<InventoryItem> ChargeActionChargedItems { get; private set; } = Array.Empty<InventoryItem>();

		public InventoryItem? ChargeActionSourceItemUpdate { get; private set; }

		public int? ChargeActionDeletedSourceItemObjectId { get; private set; }

		public int SaveStigmaChargeMutationCalls { get; private set; }

		public InventoryItem? StigmaChargeTargetItemUpdate { get; private set; }

		public int? StigmaChargeDeletedTargetItemObjectId { get; private set; }

		public InventoryItem? StigmaChargeSourceItemUpdate { get; private set; }

		public int? StigmaChargeDeletedSourceItemObjectId { get; private set; }

		public int SaveEquipmentMutationCalls { get; private set; }

		public IReadOnlyList<InventoryItem> EquipmentItems { get; private set; } = Array.Empty<InventoryItem>();

		public int SavePowerShardUseMutationCalls { get; private set; }

		public IReadOnlyList<InventoryItem> PowerShardCountUpdateItems { get; private set; } = Array.Empty<InventoryItem>();

		public IReadOnlyList<InventoryItem> PowerShardEquipUpdateItems { get; private set; } = Array.Empty<InventoryItem>();

		public IReadOnlyList<int> PowerShardDeletedItemObjectIds { get; private set; } = Array.Empty<int>();

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

		public Task<PlayerNpcFactionsSnapshot> LoadPlayerNpcFactionsAsync(
			int playerObjectId,
			NpcFactionTable npcFactions,
			int currentEpochSeconds = 0,
			CancellationToken cancellationToken = default)
		{
			LoadNpcFactionsCalls++;
			NpcFactionTable = npcFactions;
			NpcFactionCurrentEpochSeconds = currentEpochSeconds;
			return Task.FromResult(NpcFactions);
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

		public Task<bool> DeletePlayerEmotionAsync(int playerObjectId, int emotionId, CancellationToken cancellationToken = default)
		{
			DeleteEmotionCalls++;
			DeletedEmotionId = emotionId;
			return Task.FromResult(true);
		}

		public Task<bool> DeletePlayerTitleAsync(int playerObjectId, int titleId, CancellationToken cancellationToken = default)
		{
			DeleteTitleCalls++;
			DeletedTitleId = titleId;
			return Task.FromResult(true);
		}

		public Task<bool> DeletePlayerMotionAsync(int playerObjectId, int motionId, CancellationToken cancellationToken = default)
		{
			DeleteMotionCalls++;
			DeletedMotionId = motionId;
			return Task.FromResult(true);
		}

		public Task<bool> DeleteInventoryItemAsync(int itemOwnerId, int itemObjectId, CancellationToken cancellationToken = default)
		{
			return Task.FromResult(true);
		}

		public Task<bool> SaveItemUseSourceMutationAsync(
			int playerObjectId,
			InventoryItem? sourceItemUpdate,
			int? deletedSourceItemObjectId,
			CancellationToken cancellationToken = default)
		{
			SaveItemUseSourceMutationCalls++;
			ItemUseSourceItemUpdate = sourceItemUpdate;
			ItemUseDeletedSourceItemObjectId = deletedSourceItemObjectId;
			return Task.FromResult(true);
		}

		public Task<bool> SaveCraftLearnActionMutationAsync(
			int playerObjectId,
			int recipeId,
			InventoryItem? sourceItemUpdate,
			int? deletedSourceItemObjectId,
			CancellationToken cancellationToken = default)
		{
			SaveCraftLearnActionMutationCalls++;
			CraftLearnRecipeId = recipeId;
			CraftLearnSourceItemUpdate = sourceItemUpdate;
			CraftLearnDeletedSourceItemObjectId = deletedSourceItemObjectId;
			return Task.FromResult(true);
		}

		public Task<bool> SaveEmotionLearnActionMutationAsync(
			int playerObjectId,
			PlayerEmotion emotion,
			InventoryItem? sourceItemUpdate,
			int? deletedSourceItemObjectId,
			CancellationToken cancellationToken = default)
		{
			SaveEmotionLearnActionMutationCalls++;
			LearnedEmotion = emotion;
			EmotionLearnSourceItemUpdate = sourceItemUpdate;
			EmotionLearnDeletedSourceItemObjectId = deletedSourceItemObjectId;
			return Task.FromResult(true);
		}

		public Task<bool> SaveTitleAddActionMutationAsync(
			int playerObjectId,
			PlayerTitle title,
			InventoryItem? sourceItemUpdate,
			int? deletedSourceItemObjectId,
			CancellationToken cancellationToken = default)
		{
			SaveTitleAddActionMutationCalls++;
			AddedTitle = title;
			TitleAddSourceItemUpdate = sourceItemUpdate;
			TitleAddDeletedSourceItemObjectId = deletedSourceItemObjectId;
			return Task.FromResult(true);
		}

		public Task<bool> SaveSkillLearnActionMutationAsync(
			int playerObjectId,
			IReadOnlyList<PlayerSkill> skills,
			InventoryItem? sourceItemUpdate,
			int? deletedSourceItemObjectId,
			CancellationToken cancellationToken = default)
		{
			SaveSkillLearnActionMutationCalls++;
			LearnedSkills = skills;
			SkillLearnSourceItemUpdate = sourceItemUpdate;
			SkillLearnDeletedSourceItemObjectId = deletedSourceItemObjectId;
			return Task.FromResult(true);
		}

		public Task<bool> SaveInventoryExpansionMutationAsync(
			int playerObjectId,
			int itemExpands,
			int warehouseBonusExpands,
			InventoryItem? sourceItemUpdate,
			int? deletedSourceItemObjectId,
			CancellationToken cancellationToken = default)
		{
			SaveInventoryExpansionMutationCalls++;
			InventoryExpansionItemExpands = itemExpands;
			InventoryExpansionWarehouseBonusExpands = warehouseBonusExpands;
			InventoryExpansionSourceItemUpdate = sourceItemUpdate;
			InventoryExpansionDeletedSourceItemObjectId = deletedSourceItemObjectId;
			return Task.FromResult(true);
		}

		public Task<bool> SaveDyeItemActionMutationAsync(
			int playerObjectId,
			InventoryItem targetItemUpdate,
			InventoryItem? sourceItemUpdate,
			int? deletedSourceItemObjectId,
			CancellationToken cancellationToken = default)
		{
			SaveDyeItemActionMutationCalls++;
			DyeTargetItemUpdate = targetItemUpdate;
			DyeSourceItemUpdate = sourceItemUpdate;
			DyeDeletedSourceItemObjectId = deletedSourceItemObjectId;
			return Task.FromResult(true);
		}

		public Task<bool> SaveAnimationAddActionMutationAsync(
			int playerObjectId,
			IReadOnlyList<PlayerMotion> motions,
			IReadOnlyList<int> deactivatedMotionIds,
			InventoryItem? sourceItemUpdate,
			int? deletedSourceItemObjectId,
			CancellationToken cancellationToken = default)
		{
			SaveAnimationAddActionMutationCalls++;
			AnimationAddedMotions = motions;
			AnimationDeactivatedMotionIds = deactivatedMotionIds;
			AnimationSourceItemUpdate = sourceItemUpdate;
			AnimationDeletedSourceItemObjectId = deletedSourceItemObjectId;
			return Task.FromResult(true);
		}

		public Task<bool> SaveCosmeticItemActionMutationAsync(
			int playerObjectId,
			Aion.GameServer.Model.Account.CharacterAppearance appearance,
			int deletedItemObjectId,
			CancellationToken cancellationToken = default)
		{
			SaveCosmeticItemActionMutationCalls++;
			CosmeticAppearance = appearance;
			CosmeticDeletedItemObjectId = deletedItemObjectId;
			return Task.FromResult(true);
		}

		public Task<bool> SaveDecomposeActionMutationAsync(
			int playerObjectId,
			IReadOnlyList<InventoryItem> updatedItems,
			IReadOnlyList<InventoryItem> addedItems,
			InventoryItem? sourceItemUpdate,
			int? deletedSourceItemObjectId,
			CancellationToken cancellationToken = default)
		{
			SaveDecomposeActionMutationCalls++;
			DecomposeUpdatedItems = updatedItems;
			DecomposeAddedItems = addedItems;
			DecomposeSourceItemUpdate = sourceItemUpdate;
			DecomposeDeletedSourceItemObjectId = deletedSourceItemObjectId;
			return Task.FromResult(true);
		}

		public Task<bool> SaveAssemblyItemActionMutationAsync(
			int playerObjectId,
			IReadOnlyList<InventoryItem> updatedPartItems,
			IReadOnlyList<int> deletedPartObjectIds,
			IReadOnlyList<InventoryItem> updatedRewardItems,
			IReadOnlyList<InventoryItem> addedRewardItems,
			CancellationToken cancellationToken = default)
		{
			SaveAssemblyItemActionMutationCalls++;
			AssemblyUpdatedPartItems = updatedPartItems;
			AssemblyDeletedPartObjectIds = deletedPartObjectIds;
			AssemblyUpdatedRewardItems = updatedRewardItems;
			AssemblyAddedRewardItems = addedRewardItems;
			return Task.FromResult(true);
		}

		public Task<bool> SaveExpExtractActionMutationAsync(
			int playerObjectId,
			long newExp,
			InventoryItem? sourceItemUpdate,
			int? deletedSourceItemObjectId,
			IReadOnlyList<InventoryItem> updatedRewardItems,
			IReadOnlyList<InventoryItem> addedRewardItems,
			CancellationToken cancellationToken = default)
		{
			SaveExpExtractActionMutationCalls++;
			ExpExtractNewExp = newExp;
			ExpExtractSourceItemUpdate = sourceItemUpdate;
			ExpExtractDeletedSourceItemObjectId = deletedSourceItemObjectId;
			ExpExtractUpdatedRewardItems = updatedRewardItems;
			ExpExtractAddedRewardItems = addedRewardItems;
			return Task.FromResult(true);
		}

		public Task<bool> SaveApExtractActionMutationAsync(
			int playerObjectId,
			PlayerAbyssRank abyssRank,
			InventoryItem? sourceItemUpdate,
			int? deletedSourceItemObjectId,
			int deletedTargetItemObjectId,
			CancellationToken cancellationToken = default)
		{
			SaveApExtractActionMutationCalls++;
			ApExtractAbyssRank = abyssRank;
			ApExtractSourceItemUpdate = sourceItemUpdate;
			ApExtractDeletedSourceItemObjectId = deletedSourceItemObjectId;
			ApExtractDeletedTargetItemObjectId = deletedTargetItemObjectId;
			return Task.FromResult(true);
		}

		public Task<bool> SaveItemPurificationMutationAsync(
			int playerObjectId,
			IReadOnlyList<InventoryItem> materialItemUpdates,
			IReadOnlyList<int> deletedMaterialItemObjectIds,
			InventoryItem? baseItemUpdate,
			int? deletedBaseItemObjectId,
			IReadOnlyList<InventoryItem> updatedTargetItems,
			IReadOnlyList<InventoryItem> addedTargetItems,
			PlayerAbyssRank? abyssRank,
			CancellationToken cancellationToken = default)
		{
			SaveItemPurificationMutationCalls++;
			ItemPurificationMaterialItemUpdates = materialItemUpdates;
			ItemPurificationDeletedMaterialItemObjectIds = deletedMaterialItemObjectIds;
			ItemPurificationBaseItemUpdate = baseItemUpdate;
			ItemPurificationDeletedBaseItemObjectId = deletedBaseItemObjectId;
			ItemPurificationUpdatedTargetItems = updatedTargetItems;
			ItemPurificationAddedTargetItems = addedTargetItems;
			ItemPurificationAbyssRank = abyssRank;
			return Task.FromResult(true);
		}

		public Task<bool> SaveItemRemodelMutationAsync(
			int playerObjectId,
			InventoryItem targetItemUpdate,
			InventoryItem kinahItemUpdate,
			InventoryItem? extractItemUpdate,
			int? deletedExtractItemObjectId,
			CancellationToken cancellationToken = default)
		{
			SaveItemRemodelMutationCalls++;
			RemodelTargetItemUpdate = targetItemUpdate;
			RemodelKinahItemUpdate = kinahItemUpdate;
			RemodelExtractItemUpdate = extractItemUpdate;
			RemodelDeletedExtractItemObjectId = deletedExtractItemObjectId;
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

		public Task<IReadOnlyDictionary<int, long>> LoadPlayerHouseObjectCooldownsAsync(int playerObjectId, CancellationToken cancellationToken = default)
		{
			LoadHouseObjectCooldownsCalls++;
			return Task.FromResult(HouseObjectCooldowns);
		}

		public Task<IReadOnlyDictionary<int, PlayerPortalCooldown>> LoadPlayerPortalCooldownsAsync(int playerObjectId, CancellationToken cancellationToken = default)
		{
			LoadPortalCooldownsCalls++;
			return Task.FromResult(PortalCooldowns);
		}

		public Task<bool> SavePlayerPortalCooldownsAsync(
			int playerObjectId,
			IReadOnlyDictionary<int, PlayerPortalCooldown> cooldowns,
			long? nowMillis = null,
			CancellationToken cancellationToken = default)
		{
			SavePortalCooldownsCalls++;
			SavedPortalCooldowns = cooldowns;
			SavePortalCooldownsNowMillis = nowMillis;
			return Task.FromResult(true);
		}

		public Task<bool> SavePlayerCraftCooldownsAsync(
			int playerObjectId,
			IReadOnlyDictionary<int, long> cooldowns,
			long? nowMillis = null,
			CancellationToken cancellationToken = default)
		{
			SaveCraftCooldownsCalls++;
			SavedCraftCooldowns = cooldowns;
			SaveCraftCooldownsNowMillis = nowMillis;
			return Task.FromResult(true);
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

		public Task<bool> SaveItemChargeBurnMutationAsync(
			int playerObjectId,
			IReadOnlyList<InventoryItem> chargedItems,
			CancellationToken cancellationToken = default)
		{
			SaveItemChargeBurnMutationCalls++;
			ChargeBurnChargedItems = chargedItems;
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

		public Task<bool> SaveIdianPolishBurnMutationAsync(
			int playerObjectId,
			IReadOnlyList<InventoryItem> exhaustedItemUpdates,
			CancellationToken cancellationToken = default)
		{
			SaveIdianPolishBurnMutationCalls++;
			IdianPolishBurnItemUpdates = exhaustedItemUpdates;
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

		public Task<bool> SaveStigmaChargeMutationAsync(
			int playerObjectId,
			InventoryItem? targetItemUpdate,
			int? deletedTargetItemObjectId,
			InventoryItem? sourceItemUpdate,
			int? deletedSourceItemObjectId,
			CancellationToken cancellationToken = default)
		{
			SaveStigmaChargeMutationCalls++;
			StigmaChargeTargetItemUpdate = targetItemUpdate;
			StigmaChargeDeletedTargetItemObjectId = deletedTargetItemObjectId;
			StigmaChargeSourceItemUpdate = sourceItemUpdate;
			StigmaChargeDeletedSourceItemObjectId = deletedSourceItemObjectId;
			return Task.FromResult(true);
		}

		public Task<bool> SaveManastoneRemovalMutationAsync(
			int playerObjectId,
			int itemObjectId,
			int slot,
			int category,
			InventoryItem kinahItemUpdate,
			CancellationToken cancellationToken = default)
		{
			return Task.FromResult(true);
		}

		public Task<bool> SaveManastoneSocketMutationAsync(
			int playerObjectId,
			InventoryItem targetItemUpdate,
			ItemStoneSocket? addedStone,
			int addedCategory,
			InventoryItem? sourceItemUpdate,
			int? deletedSourceItemObjectId,
			IReadOnlyList<InventoryItem> supplementItemUpdates,
			IReadOnlyList<int> deletedSupplementItemObjectIds,
			CancellationToken cancellationToken = default)
		{
			return Task.FromResult(true);
		}

		public Task<bool> SaveEnchantItemMutationAsync(
			int playerObjectId,
			InventoryItem? targetItemUpdate,
			int? deletedTargetItemObjectId,
			InventoryItem? sourceItemUpdate,
			int? deletedSourceItemObjectId,
			IReadOnlyList<InventoryItem> supplementItemUpdates,
			IReadOnlyList<int> deletedSupplementItemObjectIds,
			CancellationToken cancellationToken = default)
		{
			return Task.FromResult(true);
		}

		public Task<bool> SaveGodstoneSocketMutationAsync(
			int playerObjectId,
			InventoryItem targetItemUpdate,
			InventoryItem? sourceItemUpdate,
			int? deletedSourceItemObjectId,
			CancellationToken cancellationToken = default)
		{
			return Task.FromResult(true);
		}

		public Task<bool> SaveItemAmplificationMutationAsync(
			int playerObjectId,
			InventoryItem targetItemUpdate,
			InventoryItem? materialItemUpdate,
			int? deletedMaterialItemObjectId,
			InventoryItem? toolItemUpdate,
			int? deletedToolItemObjectId,
			CancellationToken cancellationToken = default)
		{
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

		public Task<bool> SavePowerShardUseMutationAsync(
			int playerObjectId,
			IReadOnlyList<InventoryItem> countUpdateItems,
			IReadOnlyList<InventoryItem> equipUpdateItems,
			IReadOnlyList<int> deletedItemObjectIds,
			CancellationToken cancellationToken = default)
		{
			SavePowerShardUseMutationCalls++;
			PowerShardCountUpdateItems = countUpdateItems;
			PowerShardEquipUpdateItems = equipUpdateItems;
			PowerShardDeletedItemObjectIds = deletedItemObjectIds;
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

	private sealed class CapturingConnectionRegistry : IGameClientConnectionRegistry
	{
		public List<BroadcastRecord> Broadcasts { get; } = [];

		public List<PacketDelivery> SentPackets { get; } = [];

		public List<GameServerPacket> PacketOrder { get; } = [];

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
			SentPackets.Add(new PacketDelivery(playerObjectId, packet));
			PacketOrder.Add(packet);
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
			Broadcasts.Add(new BroadcastRecord(sourcePosition, sourceObjectId, packet, includeSourcePlayer));
			PacketOrder.Add(packet);
			return Task.FromResult(1);
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

	private sealed record PacketDelivery(int PlayerObjectId, GameServerPacket Packet);

	private sealed record BroadcastRecord(
		WorldPosition SourcePosition,
		int SourceObjectId,
		GameServerPacket Packet,
		bool IncludeSourcePlayer);
}
