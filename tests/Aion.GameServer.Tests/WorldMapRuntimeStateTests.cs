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

		var instance = table.AddWorldMapInstance(300030000, 7, ownerId: 1001, maxPlayers: 2);

		Assert.NotNull(instance);
		Assert.Equal(7, instance.InstanceId);
		Assert.Equal(1001, instance.OwnerId);
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
	}

	[Fact]
	public void WorldMapRuntimeStateTable_AllocatesNextInstanceIdsLikeJavaWorldMap()
	{
		var table = new WorldMapRuntimeStateTable(
		[
			new WorldMapSummary(300030000, IsInstance: true, TwinCount: 1),
			new WorldMapSummary(210010000, IsInstance: false, TwinCount: 3),
		]);

		var instance = table.CreateNextWorldMapInstance(300030000, ownerId: 1001, maxPlayers: 6);
		var second = table.CreateNextWorldMapInstance(300030000);
		var nonInstanceNext = table.CreateNextWorldMapInstance(210010000);

		Assert.NotNull(instance);
		Assert.Equal(2, instance.InstanceId);
		Assert.Equal(1001, instance.OwnerId);
		Assert.Equal(6, instance.MaxPlayers);
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
			maxPlayers: 3);
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
