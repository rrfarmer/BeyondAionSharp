using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class WorldMapRegionLifecyclePlanServiceTests
{
	[Fact]
	public void CreateAddPlan_FirstPlayerActivatesInactiveSelfAndNeighbours()
	{
		var context = CreateContext(
			self: new WorldMapRegionLifecycleRegionState(1001, IsActive: false, PlayerCount: 0),
			neighbours:
			[
				new WorldMapRegionLifecycleRegionState(1000, IsActive: false, PlayerCount: 0),
				new WorldMapRegionLifecycleRegionState(1002, IsActive: true, PlayerCount: 0),
			]);

		var plan = WorldMapRegionLifecyclePlanService.CreateAddPlan(context, objectAlreadyPresent: false, isPlayer: true);

		Assert.Equal(WorldMapRegionLifecycleAction.ActivateSelfAndNeighbours, plan.Action);
		Assert.Equal(1, plan.ResultingSelfPlayerCount);
		Assert.Equal([1001, 1000], plan.ActivatedRegionIds);
		Assert.False(plan.ShouldScheduleDeactivation);
		Assert.Contains("MapRegion.add", plan.JavaSource);
	}

	[Fact]
	public void CreateAddPlan_DuplicateOrNonPlayerDoesNotChangeLifecycle()
	{
		var context = CreateContext(new WorldMapRegionLifecycleRegionState(1001, IsActive: false, PlayerCount: 0));

		var duplicate = WorldMapRegionLifecyclePlanService.CreateAddPlan(context, objectAlreadyPresent: true, isPlayer: true);
		var nonPlayer = WorldMapRegionLifecyclePlanService.CreateAddPlan(context, objectAlreadyPresent: false, isPlayer: false);

		Assert.Equal(WorldMapRegionLifecycleAction.IgnoreExistingObject, duplicate.Action);
		Assert.Equal(WorldMapRegionLifecycleAction.AddNonPlayerObject, nonPlayer.Action);
		Assert.Empty(duplicate.ActivatedRegionIds);
		Assert.Empty(nonPlayer.ActivatedRegionIds);
	}

	[Fact]
	public void CreateRemovePlan_LastPlayerSchedulesOneJavaDelayedDeactivation()
	{
		var context = CreateContext(new WorldMapRegionLifecycleRegionState(1001, IsActive: true, PlayerCount: 1));

		var plan = WorldMapRegionLifecyclePlanService.CreateRemovePlan(context, objectWasPresent: true, wasPlayer: true);

		Assert.Equal(WorldMapRegionLifecycleAction.ScheduleDeactivation, plan.Action);
		Assert.Equal(0, plan.ResultingSelfPlayerCount);
		Assert.True(plan.ShouldScheduleDeactivation);
		Assert.Equal(TimeSpan.FromSeconds(60), plan.DeactivationDelay);
		Assert.Contains("scheduleDeactivation", plan.JavaSource);
	}

	[Fact]
	public void CreateRemovePlan_PendingDeactivationSuppressesDuplicateSchedule()
	{
		var context = CreateContext(
			new WorldMapRegionLifecycleRegionState(1001, IsActive: true, PlayerCount: 1, DeactivationPending: true));

		var plan = WorldMapRegionLifecyclePlanService.CreateRemovePlan(context, objectWasPresent: true, wasPlayer: true);

		Assert.Equal(WorldMapRegionLifecycleAction.DeactivationAlreadyPending, plan.Action);
		Assert.Equal(0, plan.ResultingSelfPlayerCount);
		Assert.False(plan.ShouldScheduleDeactivation);
	}

	[Fact]
	public void CreateRemovePlan_NonLastPlayerOnlyDecrementsCount()
	{
		var context = CreateContext(new WorldMapRegionLifecycleRegionState(1001, IsActive: true, PlayerCount: 2));

		var plan = WorldMapRegionLifecyclePlanService.CreateRemovePlan(context, objectWasPresent: true, wasPlayer: true);

		Assert.Equal(WorldMapRegionLifecycleAction.RemovePlayerObject, plan.Action);
		Assert.Equal(1, plan.ResultingSelfPlayerCount);
		Assert.False(plan.ShouldScheduleDeactivation);
	}

	[Fact]
	public void CreateScheduledDeactivationPlan_DeactivatesActiveSelfAndNeighboursWhenNoPlayersRemain()
	{
		var context = CreateContext(
			self: new WorldMapRegionLifecycleRegionState(1001, IsActive: true, PlayerCount: 0, DeactivationPending: true),
			neighbours:
			[
				new WorldMapRegionLifecycleRegionState(1000, IsActive: true, PlayerCount: 0),
				new WorldMapRegionLifecycleRegionState(1002, IsActive: false, PlayerCount: 0),
			]);

		var plan = WorldMapRegionLifecyclePlanService.CreateScheduledDeactivationPlan(context);

		Assert.Equal(WorldMapRegionLifecycleAction.DeactivateSelfAndNeighbours, plan.Action);
		Assert.True(plan.ShouldClearDeactivationPending);
		Assert.Equal([1001, 1000], plan.DeactivatedRegionIds);
		Assert.Null(plan.BlockedReason);
	}

	[Theory]
	[InlineData(true, 210010000, WorldMapRegionLifecycleBlockedReason.InstanceOrTransidiumAnnex)]
	[InlineData(false, WorldMapRegionLifecyclePlanService.TransidiumAnnexWorldId, WorldMapRegionLifecycleBlockedReason.InstanceOrTransidiumAnnex)]
	public void CreateScheduledDeactivationPlan_BlocksInstanceAndTransidiumAnnexMaps(
		bool isInstanceType,
		int mapId,
		WorldMapRegionLifecycleBlockedReason expectedReason)
	{
		var context = CreateContext(
			self: new WorldMapRegionLifecycleRegionState(1001, IsActive: true, PlayerCount: 0, DeactivationPending: true),
			neighbours: [],
			mapId: mapId,
			isInstanceType: isInstanceType);

		var plan = WorldMapRegionLifecyclePlanService.CreateScheduledDeactivationPlan(context);

		Assert.Equal(WorldMapRegionLifecycleAction.DeactivationBlocked, plan.Action);
		Assert.True(plan.ShouldClearDeactivationPending);
		Assert.Equal(expectedReason, plan.BlockedReason);
		Assert.Empty(plan.DeactivatedRegionIds);
	}

	[Fact]
	public void CreateScheduledDeactivationPlan_BlocksWhenAnyNeighbourStillHasPlayers()
	{
		var context = CreateContext(
			self: new WorldMapRegionLifecycleRegionState(1001, IsActive: true, PlayerCount: 0, DeactivationPending: true),
			neighbours: [new WorldMapRegionLifecycleRegionState(1000, IsActive: true, PlayerCount: 1)]);

		var plan = WorldMapRegionLifecyclePlanService.CreateScheduledDeactivationPlan(context);

		Assert.Equal(WorldMapRegionLifecycleAction.DeactivationBlocked, plan.Action);
		Assert.Equal(WorldMapRegionLifecycleBlockedReason.NeighbourHasPlayers, plan.BlockedReason);
		Assert.True(plan.ShouldClearDeactivationPending);
		Assert.Empty(plan.DeactivatedRegionIds);
	}

	private static WorldMapRegionLifecycleContext CreateContext(
		WorldMapRegionLifecycleRegionState self,
		IReadOnlyList<WorldMapRegionLifecycleRegionState>? neighbours = null,
		int mapId = 210010000,
		bool isInstanceType = false)
	{
		return new WorldMapRegionLifecycleContext(mapId, isInstanceType, self, neighbours ?? []);
	}
}
