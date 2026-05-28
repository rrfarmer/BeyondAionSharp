using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class WorldMapRegionZoneScanPlanServiceTests
{
	[Fact]
	public void CreateRevalidationPlan_LeavesLaterPriorityZonesWithinSameTypeUntilTypeChanges()
	{
		var zones = new[]
		{
			Create("sub-priority-a", WorldMapRegionZoneSortClassName.Sub, priority: 5, revalidateSucceeds: true),
			Create("sub-priority-b", WorldMapRegionZoneSortClassName.Sub, priority: 1, revalidateSucceeds: true),
			Create("pvp-priority", WorldMapRegionZoneSortClassName.Pvp, priority: 2, revalidateSucceeds: true),
			Create("pvp-normal-failed", WorldMapRegionZoneSortClassName.Pvp, priority: 0, revalidateSucceeds: false),
		};

		var plan = WorldMapRegionZoneScanPlanService.CreateRevalidationPlan(creatureIsSpawned: true, zones);

		Assert.Equal(
		[
			("sub-priority-a", WorldMapRegionZoneRevalidationActionType.Enter),
			("sub-priority-b", WorldMapRegionZoneRevalidationActionType.Leave),
			("pvp-priority", WorldMapRegionZoneRevalidationActionType.Enter),
			("pvp-normal-failed", WorldMapRegionZoneRevalidationActionType.Leave),
		], plan.Actions.Select(action => (action.ZoneId, action.ActionType)));
		Assert.Contains("MapRegion.revalidateZones", plan.JavaSource);
	}

	[Fact]
	public void CreateRevalidationPlan_UnspawnedCreatureLeavesAllZones()
	{
		var zones = new[]
		{
			Create("sub", WorldMapRegionZoneSortClassName.Sub, priority: 0, revalidateSucceeds: true),
			Create("pvp", WorldMapRegionZoneSortClassName.Pvp, priority: 0, revalidateSucceeds: true),
		};

		var plan = WorldMapRegionZoneScanPlanService.CreateRevalidationPlan(creatureIsSpawned: false, zones);

		Assert.Equal(
		[
			("sub", WorldMapRegionZoneRevalidationActionType.Leave),
			("pvp", WorldMapRegionZoneRevalidationActionType.Leave),
		], plan.Actions.Select(action => (action.ZoneId, action.ActionType)));
	}

	[Fact]
	public void FindInsideZones_ReturnsEveryInsideCreatureZoneInConstructorOrder()
	{
		var zones = new[]
		{
			Create("outside", WorldMapRegionZoneSortClassName.Sub, priority: 0, isInsideCreature: false),
			Create("inside-a", WorldMapRegionZoneSortClassName.Sub, priority: 0, isInsideCreature: true),
			Create("inside-b", WorldMapRegionZoneSortClassName.Fort, priority: 0, isInsideCreature: true),
		};

		var insideZoneIds = WorldMapRegionZoneScanPlanService.FindInsideZones(zones);

		Assert.Equal(["inside-a", "inside-b"], insideZoneIds);
	}

	[Fact]
	public void IsInsideZoneByName_UsesFirstMatchingZoneNameForCreatureAndCoordinateChecks()
	{
		var sharedNameId = WorldMapRegionZoneSortService.GetJavaZoneNameId("SHARED");
		var zones = new[]
		{
			Create("first-shared", WorldMapRegionZoneSortClassName.Sub, priority: 0, zoneNameId: sharedNameId, isInsideCreature: false, isInsideCoordinate: true),
			Create("second-shared", WorldMapRegionZoneSortClassName.Sub, priority: 0, zoneNameId: sharedNameId, isInsideCreature: true, isInsideCoordinate: true),
		};

		Assert.False(WorldMapRegionZoneScanPlanService.IsInsideZoneByName(
			zones,
			sharedNameId,
			WorldMapRegionZoneInsideMode.Creature));
		Assert.True(WorldMapRegionZoneScanPlanService.IsInsideZoneByName(
			zones,
			sharedNameId,
			WorldMapRegionZoneInsideMode.Coordinate));
	}

	[Fact]
	public void IsInsideItemUseZone_UsesFortressSpecialCaseOrXmlNamePrefix()
	{
		var zones = new[]
		{
			Create("sub-prefix", WorldMapRegionZoneSortClassName.Sub, priority: 0, xmlName: "ITEM_LIMIT_001", isInsideCreature: true),
			Create("fort-outside", WorldMapRegionZoneSortClassName.Fort, priority: 0, xmlName: "FORT_A", isInsideCreature: false),
			Create("fort-inside", WorldMapRegionZoneSortClassName.Fort, priority: 0, xmlName: "FORT_B", isInsideCreature: true),
		};

		Assert.True(WorldMapRegionZoneScanPlanService.IsInsideItemUseZone(
			zones,
			"ITEM_LIMIT",
			WorldMapRegionZoneInsideMode.Creature));
		Assert.True(WorldMapRegionZoneScanPlanService.IsInsideItemUseZone(
			zones,
			WorldMapRegionZoneScanPlanService.AbyssCastleAreaZoneName,
			WorldMapRegionZoneInsideMode.Creature));
		Assert.False(WorldMapRegionZoneScanPlanService.IsInsideItemUseZone(
			zones,
			"MISSING_PREFIX",
			WorldMapRegionZoneInsideMode.Creature));
	}

	[Fact]
	public void CreateDeathPlan_ScansOnlyInsideZonesAndStopsOnFirstHandledDeath()
	{
		var zones = new[]
		{
			Create("outside", WorldMapRegionZoneSortClassName.Sub, priority: 0, isInsideCreature: false, deathHandlerHandles: true),
			Create("inside-unhandled", WorldMapRegionZoneSortClassName.Sub, priority: 0, isInsideCreature: true, deathHandlerHandles: false),
			Create("inside-handled", WorldMapRegionZoneSortClassName.Pvp, priority: 0, isInsideCreature: true, deathHandlerHandles: true),
			Create("inside-after-handled", WorldMapRegionZoneSortClassName.Fort, priority: 0, isInsideCreature: true, deathHandlerHandles: true),
		};

		var plan = WorldMapRegionZoneScanPlanService.CreateDeathPlan(zones);

		Assert.True(plan.WasHandled);
		Assert.Equal("inside-handled", plan.HandledZoneId);
		Assert.Equal(
		[
			("inside-unhandled", WorldMapRegionZoneDeathActionType.NotHandled),
			("inside-handled", WorldMapRegionZoneDeathActionType.Handled),
		], plan.Actions.Select(action => (action.ZoneId, action.ActionType)));
		Assert.Contains("MapRegion.onDie", plan.JavaSource);
	}

	[Fact]
	public void CreateDeathPlan_ReturnsUnhandledWhenInsideZonesDoNotHandleDeath()
	{
		var zones = new[]
		{
			Create("inside-a", WorldMapRegionZoneSortClassName.Sub, priority: 0, isInsideCreature: true, deathHandlerHandles: false),
			Create("inside-b", WorldMapRegionZoneSortClassName.Pvp, priority: 0, isInsideCreature: true, deathHandlerHandles: false),
		};

		var plan = WorldMapRegionZoneScanPlanService.CreateDeathPlan(zones);

		Assert.False(plan.WasHandled);
		Assert.Null(plan.HandledZoneId);
		Assert.Equal(
		[
			("inside-a", WorldMapRegionZoneDeathActionType.NotHandled),
			("inside-b", WorldMapRegionZoneDeathActionType.NotHandled),
		], plan.Actions.Select(action => (action.ZoneId, action.ActionType)));
	}

	private static WorldMapRegionZoneScanCandidate Create(
		string zoneId,
		WorldMapRegionZoneSortClassName zoneClassName,
		int priority,
		int? zoneNameId = null,
		string? xmlName = null,
		bool revalidateSucceeds = true,
		bool isInsideCreature = true,
		bool isInsideCoordinate = true,
		bool deathHandlerHandles = false)
	{
		return new WorldMapRegionZoneScanCandidate(
			zoneId,
			zoneClassName,
			priority,
			zoneNameId ?? WorldMapRegionZoneSortService.GetJavaZoneNameId(zoneId),
			xmlName ?? zoneId,
			revalidateSucceeds,
			isInsideCreature,
			isInsideCoordinate,
			deathHandlerHandles);
	}
}
