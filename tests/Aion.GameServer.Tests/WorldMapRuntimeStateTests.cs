using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class WorldMapRuntimeStateTests
{
	[Fact]
	public void WorldMapRuntimeState_MatchesJavaWorldOptionsMutationSlice()
	{
		var summary = new WorldMapSummary(
			400010000,
			IsInstance: false,
			TwinCount: 1,
			Flags: WorldZoneAttributes.Fly | WorldZoneAttributes.Glide | WorldZoneAttributes.Ride | WorldZoneAttributes.NoReturnBattle);
		var state = new WorldMapRuntimeState(summary);

		Assert.Equal(summary.Flags, state.CurrentFlags);
		Assert.True(state.IsFlightAllowed);
		Assert.True(state.CanGlide);
		Assert.True(state.CanRide);
		Assert.False(state.CanReturnToBattle);
		Assert.False(state.HasOverriddenOption(WorldZoneAttributes.Fly));

		state.RemoveWorldOption(WorldZoneAttributes.Fly | WorldZoneAttributes.NoReturnBattle);
		Assert.False(state.IsFlightAllowed);
		Assert.True(state.CanReturnToBattle);
		Assert.True(state.HasOverriddenOption(WorldZoneAttributes.Fly));

		state.SetWorldOption(WorldZoneAttributes.FlyRide);
		Assert.True(state.CanFlyRide);
		Assert.True(state.HasOverriddenOption(WorldZoneAttributes.FlyRide));

		state.SetWorldOption(WorldZoneAttributes.Fly | WorldZoneAttributes.NoReturnBattle);
		Assert.True(state.IsFlightAllowed);
		Assert.False(state.CanReturnToBattle);
		Assert.False(state.HasOverriddenOption(WorldZoneAttributes.Fly));
	}

	[Fact]
	public void WorldMapRuntimeStateTable_MatchesJavaWorldMapLookupSlice()
	{
		var table = new WorldMapRuntimeStateTable(
		[
			new WorldMapSummary(210010000, IsInstance: false, TwinCount: 5, Flags: WorldZoneAttributes.Glide),
			new WorldMapSummary(210010000, IsInstance: false, TwinCount: 7, Flags: WorldZoneAttributes.Fly),
			new WorldMapSummary(400010000, IsInstance: false, TwinCount: 1, Flags: WorldZoneAttributes.Fly | WorldZoneAttributes.Glide),
		]);

		Assert.Equal(2, table.Count);
		var elyosMap = table.GetMap(210010000);
		Assert.NotNull(elyosMap);
		Assert.Equal(7, elyosMap.Summary.TwinCount);
		Assert.True(elyosMap.IsFlightAllowed);
		Assert.False(elyosMap.CanGlide);
		Assert.True(table.TryGetMap(400010000, out var flyMap));
		Assert.NotNull(flyMap);
		Assert.True(flyMap.IsFlightAllowed);
		Assert.True(table.RemoveWorldOption(400010000, WorldZoneAttributes.Fly));
		Assert.False(flyMap.IsFlightAllowed);
		Assert.True(flyMap.HasOverriddenOption(WorldZoneAttributes.Fly));

		Assert.Same(flyMap, table.GetMap(400010000));
		Assert.Null(table.GetMap(123));
		Assert.False(table.SetWorldOption(123, WorldZoneAttributes.Fly));
	}

	[Fact]
	public void WorldMapRuntimeStateTable_TracksExplicitInstanceRemovalLikeJavaWorldMap()
	{
		var table = new WorldMapRuntimeStateTable(
		[
			new WorldMapSummary(300030000, IsInstance: true, TwinCount: 1),
		]);

		Assert.True(table.InstanceExists(300030000, 2));
		Assert.True(table.RemoveWorldMapInstance(300030000, 2));
		Assert.False(table.InstanceExists(300030000, 2));
		Assert.NotNull(table.AddWorldMapInstance(300030000, 2));
		Assert.True(table.InstanceExists(300030000, 2));

		Assert.True(table.RemoveWorldMapInstance(300030000, 0));
		Assert.False(table.InstanceExists(300030000, 1));
		Assert.False(table.InstanceExists(123, 1));
		Assert.False(table.RemoveWorldMapInstance(123, 1));
		Assert.Null(table.AddWorldMapInstance(123, 1));
	}

	[Fact]
	public void WorldMapRuntimeStateTable_TracksInstanceRegistrationAndCapacitySlice()
	{
		var table = new WorldMapRuntimeStateTable(
		[
			new WorldMapSummary(300030000, IsInstance: true, TwinCount: 1),
		]);

		var instance = table.AddWorldMapInstance(300030000, 7, ownerId: 1001, maxPlayers: 2, difficultyId: 2);

		Assert.NotNull(instance);
		Assert.Equal(7, instance.InstanceId);
		Assert.Equal(1001, instance.OwnerId);
		Assert.Equal(2, instance.DifficultyId);
		Assert.True(instance.IsPersonal);
		Assert.False(instance.IsFull);
		instance.Register(1001);
		instance.Register(2002);
		Assert.Equal(2, instance.RegisteredCount);
		Assert.True(instance.IsRegistered(1001));
		Assert.Same(instance, table.GetRegisteredInstance(300030000, 2002));
		Assert.Null(table.GetRegisteredInstance(300030000, 3003));

		instance.AddPlayer(1001);
		Assert.False(instance.IsFull);
		instance.AddPlayer(2002);
		Assert.True(instance.IsFull);
		instance.RemovePlayer(1001);
		Assert.False(instance.IsFull);
		Assert.True(table.TryGetWorldMapInstance(300030000, 7, out var stored));
		Assert.Same(instance, stored);
		Assert.False(table.TryGetWorldMapInstance(123, 7, out _));

		var firstStart = new WorldPosition(300030000, 10, 20, 30, 40, InstanceId: 7);
		var secondStart = new WorldPosition(300030000, 50, 60, 70, 80, InstanceId: 7);
		Assert.Equal(firstStart, instance.SetStartPositionIfMissing(firstStart));
		Assert.Equal(firstStart, instance.SetStartPositionIfMissing(secondStart));
		Assert.Equal(firstStart, instance.StartPosition);
	}

	[Fact]
	public void WorldMapInstanceRuntimeState_RegisterTeamIdMirrorsJavaRegisterTeamStorage()
	{
		var instance = new WorldMapInstanceRuntimeState(instanceId: 7, maxPlayers: 6);

		instance.RegisterTeamId(88001);

		Assert.Equal(88001, instance.RegisteredTeamId);
		Assert.True(instance.IsRegistered(88001));
		Assert.Equal(1, instance.RegisteredCount);
		var error = Assert.Throws<InvalidOperationException>(() => instance.RegisterTeamId(88002));
		Assert.Contains("already registered", error.Message);
		Assert.Equal(88001, instance.RegisteredTeamId);
		Assert.False(instance.IsRegistered(88002));
	}

	[Fact]
	public void WorldMapInstanceRuntimeState_TracksQuestStartIdsLikeJavaWorldMapInstance()
	{
		var instance = new WorldMapInstanceRuntimeState(instanceId: 7, maxPlayers: 6);

		Assert.True(instance.RegisterQuestStartIds([1001, 1002, 1001]));
		Assert.Equal([1001, 1002], instance.QuestIds.Order());
		Assert.False(instance.RegisterQuestStartIds([1002]));
		Assert.True(instance.RegisterQuestStartIds([1003]));
		Assert.Equal([1001, 1002, 1003], instance.QuestIds.Order());
	}

	[Fact]
	public void WorldMapInstanceRuntimeState_PlansDelayedNearbyRefreshLikeJavaWorldMapInstance()
	{
		var instance = new WorldMapInstanceRuntimeState(instanceId: 7, maxPlayers: 6);

		var first = instance.RegisterQuestStartIdsAndPlanNearbyRefresh([1001, 1002, 1001]);
		var duplicate = instance.RegisterQuestStartIdsAndPlanNearbyRefresh([1002]);
		var whilePending = instance.RegisterQuestStartIdsAndPlanNearbyRefresh([1003]);

		Assert.Equal(WorldMapNearbyQuestRefreshScheduleStatus.Scheduled, first.Status);
		Assert.True(first.WouldScheduleTask);
		Assert.Equal(TimeSpan.FromMilliseconds(1500), first.Delay);
		Assert.Equal([1001, 1002], first.NewlyRegisteredQuestIds.Order());
		Assert.Equal([1001, 1002], first.WorldQuestIds.Order());
		Assert.True(instance.HasPendingNearbyQuestRefresh);
		Assert.Equal(WorldMapNearbyQuestRefreshScheduleStatus.NoNewQuestIds, duplicate.Status);
		Assert.False(duplicate.WouldScheduleTask);
		Assert.Equal([1001, 1002], duplicate.WorldQuestIds.Order());
		Assert.Equal(WorldMapNearbyQuestRefreshScheduleStatus.AlreadyPending, whilePending.Status);
		Assert.False(whilePending.WouldScheduleTask);
		Assert.Equal([1003], whilePending.NewlyRegisteredQuestIds.Order());
		Assert.Equal([1001, 1002, 1003], whilePending.WorldQuestIds.Order());
		Assert.Equal([1001, 1002, 1003], instance.QuestIds.Order());
	}

	[Fact]
	public void WorldMapInstanceRuntimeState_CompletesPendingNearbyRefreshBeforeSchedulingAgain()
	{
		var instance = new WorldMapInstanceRuntimeState(instanceId: 7, maxPlayers: 6);
		var first = instance.RegisterQuestStartIdsAndPlanNearbyRefresh([2001]);

		var completed = instance.CompletePendingNearbyQuestRefresh();
		var second = instance.RegisterQuestStartIdsAndPlanNearbyRefresh([2002]);
		var completedAgain = instance.CompletePendingNearbyQuestRefresh();
		var noPending = instance.CompletePendingNearbyQuestRefresh();

		Assert.Equal(WorldMapNearbyQuestRefreshScheduleStatus.Scheduled, first.Status);
		Assert.True(completed);
		Assert.False(instance.HasPendingNearbyQuestRefresh);
		Assert.Equal(WorldMapNearbyQuestRefreshScheduleStatus.Scheduled, second.Status);
		Assert.Equal([2002], second.NewlyRegisteredQuestIds.Order());
		Assert.Equal([2001, 2002], second.WorldQuestIds.Order());
		Assert.True(completedAgain);
		Assert.False(noPending);
	}

	[Fact]
	public void WorldMapInstanceRuntimeState_NotifiesInstanceCreateOnceLikeJavaInstanceService()
	{
		var handler = new RecordingInstanceLifecycleHandler();
		var instance = new WorldMapInstanceRuntimeState(instanceId: 7, maxPlayers: 6, instanceHandler: handler);

		Assert.False(instance.InstanceCreateNotified);
		Assert.True(instance.NotifyInstanceCreated());
		Assert.False(instance.NotifyInstanceCreated());

		Assert.True(instance.InstanceCreateNotified);
		var notified = Assert.Single(handler.CreatedInstances);
		Assert.Same(instance, notified);
	}

	private sealed class RecordingInstanceLifecycleHandler : IInstanceLifecycleHandler
	{
		public List<WorldMapInstanceRuntimeState> CreatedInstances { get; } = new();

		public void OnInstanceCreate(WorldMapInstanceRuntimeState instance)
		{
			CreatedInstances.Add(instance);
		}
	}

	[Fact]
	public void NearbyQuestCandidateProjectionService_RegistersNpcStartQuestIdsLikeJavaWorldMapInstance()
	{
		var table = new QuestNpcStartTable();
		table.RegisterOnQuestStart(new QuestNpcStartRegistrationSource(203098, 1192, QuestNpcStartRegistrationSourceKind.Manual, "manual"));
		table.RegisterOnQuestStart(new QuestNpcStartRegistrationSource(203098, 1194, QuestNpcStartRegistrationSourceKind.Manual, "manual"));
		table.RegisterOnQuestStart(new QuestNpcStartRegistrationSource(203099, 1194, QuestNpcStartRegistrationSourceKind.Manual, "manual"));
		table.RegisterOnQuestStart(new QuestNpcStartRegistrationSource(203099, 1195, QuestNpcStartRegistrationSourceKind.Manual, "manual"));
		var instance = new WorldMapInstanceRuntimeState(instanceId: 7, maxPlayers: 6);
		instance.RegisterQuestStartIds([1192]);

		var result = NearbyQuestCandidateProjectionService.ProjectNpcStartQuestIds(
			instance,
			table,
			[203098, 999999, 203099, 203098]);

		Assert.Equal([203098, 203099, 999999], result.InspectedNpcIds.Order());
		Assert.Equal([203098, 203099], result.MatchedNpcIds.Order());
		Assert.Equal([1192, 1194, 1195], result.ProjectedQuestIds.Order());
		Assert.Equal([1194, 1195], result.NewlyRegisteredQuestIds.Order());
		Assert.Equal([1192, 1194, 1195], result.WorldQuestIds.Order());
		Assert.Equal([1192, 1194, 1195], instance.QuestIds.Order());
	}

	[Fact]
	public void WorldMapRuntimeStateTable_AllocatesNextInstanceIdsLikeJavaWorldMap()
	{
		var table = new WorldMapRuntimeStateTable(
		[
			new WorldMapSummary(300030000, IsInstance: true, TwinCount: 1),
			new WorldMapSummary(210010000, IsInstance: false, TwinCount: 3),
		]);

		var instance = table.CreateNextWorldMapInstance(300030000, ownerId: 1001, maxPlayers: 6, difficultyId: 2);
		var second = table.CreateNextWorldMapInstance(300030000);
		var nonInstanceNext = table.CreateNextWorldMapInstance(210010000);

		Assert.NotNull(instance);
		Assert.Equal(2, instance.InstanceId);
		Assert.Equal(1001, instance.OwnerId);
		Assert.Equal(6, instance.MaxPlayers);
		Assert.Equal(2, instance.DifficultyId);
		Assert.NotNull(second);
		Assert.Equal(3, second.InstanceId);
		Assert.NotNull(nonInstanceNext);
		Assert.Equal(4, nonInstanceNext.InstanceId);
		Assert.Null(table.CreateNextWorldMapInstance(123));
	}

	[Fact]
	public void InstanceRuntimeService_CreatesAndReusesRegisteredInstances()
	{
		var table = new WorldMapRuntimeStateTable(
		[
			new WorldMapSummary(300030000, IsInstance: true, TwinCount: 1),
			new WorldMapSummary(210010000, IsInstance: false, TwinCount: 1),
		]);

		var created = InstanceRuntimeService.GetNextAvailableInstanceForPlayer(
			table,
			300030000,
			playerObjectId: 1001,
			maxPlayers: 3,
			difficultyId: 2);
		var reused = InstanceRuntimeService.GetOrRegisterInstance(
			table,
			300030000,
			playerObjectId: 1001,
			maxPlayers: 3);
		var other = InstanceRuntimeService.GetOrRegisterInstance(
			table,
			300030000,
			playerObjectId: 1002,
			maxPlayers: 3);

		Assert.Equal(2, created.InstanceId);
		Assert.Equal(2, created.DifficultyId);
		Assert.True(created.IsRegistered(1001));
		Assert.Same(created, reused);
		Assert.NotSame(created, other);
		Assert.Equal(3, other.InstanceId);
		Assert.True(other.IsRegistered(1002));
		var error = Assert.Throws<UnsupportedOperationException>(() =>
			InstanceRuntimeService.GetNextAvailableInstance(table, 210010000));
		Assert.Contains("210010000", error.Message);
		Assert.Throws<InvalidOperationException>(() =>
			InstanceRuntimeService.GetNextAvailableInstance(table, 123));
	}

	[Fact]
	public void InstanceCooltimeTable_MatchesJavaRaceSpecificMaxMemberLookup()
	{
		var cooltimes = new InstanceCooltimeTable(
		[
			new InstanceCooltimeSummary(8, 300030000, "PC_ALL", 5, MaxMemberLight: 6, MaxMemberDark: 12),
		]);

		Assert.Equal(6, cooltimes.GetMaxMemberCount(300030000, "ELYOS"));
		Assert.Equal(12, cooltimes.GetMaxMemberCount(300030000, "ASMODIANS"));
		Assert.Equal(12, cooltimes.GetMaxMemberCount(300030000, "UNKNOWN"));
		Assert.Equal(0, cooltimes.GetMaxMemberCount(123, "ELYOS"));
	}

	[Fact]
	public void InstanceCooltimeTable_MatchesJavaRaceSpecificEnterLevelLookup()
	{
		var cooltimes = new InstanceCooltimeTable(
		[
			new InstanceCooltimeSummary(
				8,
				300030000,
				"PC_ALL",
				5,
				EnterMinLevelLight: 41,
				EnterMaxLevelLight: 50,
				EnterMinLevelDark: 42,
				EnterMaxLevelDark: 51),
		]);

		Assert.Equal(41, cooltimes.GetEnterMinLevel(300030000, "ELYOS"));
		Assert.Equal(50, cooltimes.GetEnterMaxLevel(300030000, "ELYOS"));
		Assert.Equal(42, cooltimes.GetEnterMinLevel(300030000, "ASMODIANS"));
		Assert.Equal(51, cooltimes.GetEnterMaxLevel(300030000, "ASMODIANS"));
		Assert.Equal(42, cooltimes.GetEnterMinLevel(300030000, "UNKNOWN"));
		Assert.Equal(51, cooltimes.GetEnterMaxLevel(300030000, "UNKNOWN"));
		Assert.Equal(0, cooltimes.GetEnterMinLevel(123, "ELYOS"));
		Assert.Equal(0, cooltimes.GetEnterMaxLevel(123, "ELYOS"));
	}

	[Fact]
	public void InstanceCooltimeTable_MatchesJavaCanEnterMentorLookup()
	{
		var cooltimes = new InstanceCooltimeTable(
		[
			new InstanceCooltimeSummary(8, 300030000, "PC_ALL", 5, CanEnterMentor: true),
			new InstanceCooltimeSummary(9, 300040000, "PC_ALL", 5, CanEnterMentor: false),
		]);

		Assert.True(cooltimes.CanEnterMentor(300030000));
		Assert.False(cooltimes.CanEnterMentor(300040000));
		Assert.False(cooltimes.CanEnterMentor(123));
	}

	[Fact]
	public void InstanceRuntimeService_PlayerOverloadUsesInstanceCooltimeMaxMembers()
	{
		var table = new WorldMapRuntimeStateTable(
		[
			new WorldMapSummary(300030000, IsInstance: true, TwinCount: 1),
		]);
		var cooltimes = new InstanceCooltimeTable(
		[
			new InstanceCooltimeSummary(8, 300030000, "PC_ALL", 5, MaxMemberLight: 6, MaxMemberDark: 12),
		]);
		var elyos = new Player { ObjectId = 1001, Race = "ELYOS" };
		var asmodian = new Player { ObjectId = 2002, Race = "ASMODIANS" };

		var elyosInstance = InstanceRuntimeService.GetNextAvailableInstanceForPlayer(table, 300030000, elyos, cooltimes);
		var reused = InstanceRuntimeService.GetOrRegisterInstance(table, 300030000, elyos, cooltimes);
		var asmodianInstance = InstanceRuntimeService.GetOrRegisterInstance(table, 300030000, asmodian, cooltimes);

		Assert.Equal(6, elyosInstance.MaxPlayers);
		Assert.True(elyosInstance.IsRegistered(1001));
		Assert.Same(elyosInstance, reused);
		Assert.Equal(12, asmodianInstance.MaxPlayers);
		Assert.True(asmodianInstance.IsRegistered(2002));
		Assert.NotSame(elyosInstance, asmodianInstance);
	}

	[Fact]
	public void InstanceRuntimeService_CreatesPortalTransferInstanceWithStartPosition()
	{
		var table = new WorldMapRuntimeStateTable(
		[
			new WorldMapSummary(300030000, IsInstance: true, TwinCount: 1),
			new WorldMapSummary(210010000, IsInstance: false, TwinCount: 1),
		]);
		var player = new Player { ObjectId = 1001 };
		var portalLocation = new WorldPosition(300030000, 10, 20, 30, 40, InstanceId: 1);

		var plan = InstanceRuntimeService.CreatePortalTransferInstance(
			table,
			player,
			portalLocation,
			ownerId: player.ObjectId,
			maxPlayers: 6);

		Assert.Equal(2, plan.Instance.InstanceId);
		Assert.Equal(player.ObjectId, plan.Instance.OwnerId);
		Assert.Equal(6, plan.Instance.MaxPlayers);
		Assert.True(plan.Instance.IsRegistered(player.ObjectId));
		Assert.Equal(portalLocation with { InstanceId = 2 }, plan.Destination);
		Assert.Equal(plan.Destination, plan.Instance.StartPosition);
		Assert.Same(plan.Instance, table.GetRegisteredInstance(300030000, player.ObjectId));
		Assert.Throws<UnsupportedOperationException>(() =>
			InstanceRuntimeService.CreatePortalTransferInstance(table, player, new WorldPosition(210010000, 1, 2, 3, 4)));
	}

	[Fact]
	public void InstanceCooltimeTable_CalculatesRelativeEntranceCooldownLikeJava()
	{
		var now = new DateTimeOffset(2026, 5, 23, 8, 30, 0, TimeSpan.FromHours(-4));
		var cooltimes = new InstanceCooltimeTable(
		[
			new InstanceCooltimeSummary(1, 300030000, "PC_ALL", MaxCount: 5, CoolTimeType: "RELATIVE", EntCoolTime: 30),
			new InstanceCooltimeSummary(2, 300040000, "PC_ALL", MaxCount: 5, CoolTimeType: "RELATIVE", EntCoolTime: 0),
		]);

		Assert.Equal(now.AddMinutes(30).ToUnixTimeMilliseconds(), cooltimes.CalculateInstanceEntranceCooltime(300030000, now));
		Assert.Equal(now.AddMinutes(15).ToUnixTimeMilliseconds(), cooltimes.CalculateInstanceEntranceCooltime(300030000, now, instanceCooldownRate: 2));
		Assert.Equal(0, cooltimes.CalculateInstanceEntranceCooltime(300040000, now));
		Assert.Equal(0, cooltimes.CalculateInstanceEntranceCooltime(123, now));
	}

	[Fact]
	public void InstanceCooltimeTable_CalculatesDailyEntranceCooldownLikeJava()
	{
		var beforeReset = new DateTimeOffset(2026, 5, 23, 8, 30, 0, TimeSpan.FromHours(-4));
		var afterReset = new DateTimeOffset(2026, 5, 23, 10, 0, 0, TimeSpan.FromHours(-4));
		var cooltimes = new InstanceCooltimeTable(
		[
			new InstanceCooltimeSummary(1, 300030000, "PC_ALL", MaxCount: 5, CoolTimeType: "DAILY", EntCoolTime: 900),
		]);

		Assert.Equal(
			new DateTimeOffset(2026, 5, 23, 9, 0, 0, TimeSpan.FromHours(-4)).ToUnixTimeMilliseconds(),
			cooltimes.CalculateInstanceEntranceCooltime(300030000, beforeReset));
		Assert.Equal(
			new DateTimeOffset(2026, 5, 24, 9, 0, 0, TimeSpan.FromHours(-4)).ToUnixTimeMilliseconds(),
			cooltimes.CalculateInstanceEntranceCooltime(300030000, afterReset));
	}

	[Fact]
	public void InstanceCooltimeTable_CalculatesWeeklyEntranceCooldownLikeJava()
	{
		var tuesdayAfterReset = new DateTimeOffset(2026, 5, 19, 10, 0, 0, TimeSpan.FromHours(-4));
		var wednesdayAfterReset = new DateTimeOffset(2026, 5, 20, 10, 0, 0, TimeSpan.FromHours(-4));
		var cooltimes = new InstanceCooltimeTable(
		[
			new InstanceCooltimeSummary(
				1,
				300030000,
				"PC_ALL",
				MaxCount: 5,
				CoolTimeType: "WEEKLY",
				TypeValue: "Mon,Wed",
				EntCoolTime: 900),
		]);

		Assert.Equal(
			new DateTimeOffset(2026, 5, 20, 9, 0, 0, TimeSpan.FromHours(-4)).ToUnixTimeMilliseconds(),
			cooltimes.CalculateInstanceEntranceCooltime(300030000, tuesdayAfterReset));
		Assert.Equal(
			new DateTimeOffset(2026, 5, 25, 9, 0, 0, TimeSpan.FromHours(-4)).ToUnixTimeMilliseconds(),
			cooltimes.CalculateInstanceEntranceCooltime(300030000, wednesdayAfterReset));
	}
}
