using Aion.GameServer.Dataholders;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class WorldMapRegionZoneCapabilityServiceTests
{
	[Fact]
	public void CreatePlan_UsesWorldMapOptionsWhenZoneFlagsAreMinusOneOrZero()
	{
		var worldMap = CreateWorldMap(WorldZoneAttributes.Fly | WorldZoneAttributes.Glide | WorldZoneAttributes.Bind);

		var minusOnePlan = WorldMapRegionZoneCapabilityService.CreatePlan(new WorldMapRegionZoneCapabilityContext(
			worldMap,
			CurrentWorldFlags: WorldZoneAttributes.Fly | WorldZoneAttributes.Glide | WorldZoneAttributes.Bind,
			WorldMapRegionZoneSortClassName.Sub,
			ZoneFlags: -1));
		var zeroPlan = WorldMapRegionZoneCapabilityService.CreatePlan(new WorldMapRegionZoneCapabilityContext(
			worldMap,
			CurrentWorldFlags: WorldZoneAttributes.Fly | WorldZoneAttributes.Glide | WorldZoneAttributes.Bind,
			WorldMapRegionZoneSortClassName.Sub,
			ZoneFlags: 0));

		Assert.True(minusOnePlan.CanFly);
		Assert.True(minusOnePlan.CanGlide);
		Assert.True(minusOnePlan.CanPutKisk);
		Assert.True(zeroPlan.CanFly);
		Assert.True(zeroPlan.CanGlide);
		Assert.True(zeroPlan.CanPutKisk);
		Assert.Contains("ZoneInstance", minusOnePlan.JavaSource);
	}

	[Fact]
	public void CreatePlan_UsesZoneFlagsUnlessWorldMapOptionWasOverridden()
	{
		var worldMap = CreateWorldMap(WorldZoneAttributes.Fly);
		var currentWorldFlags = WorldZoneAttributes.Fly | WorldZoneAttributes.Glide;
		var zoneFlags = (int)(WorldZoneAttributes.Fly | WorldZoneAttributes.Recall | WorldZoneAttributes.Ride);

		var plan = WorldMapRegionZoneCapabilityService.CreatePlan(new WorldMapRegionZoneCapabilityContext(
			worldMap,
			currentWorldFlags,
			WorldMapRegionZoneSortClassName.Sub,
			zoneFlags));

		Assert.True(plan.CanFly);
		Assert.True(plan.CanGlide);
		Assert.False(plan.CanPutKisk);
		Assert.True(plan.CanRecall);
		Assert.True(plan.CanRide);
		Assert.False(plan.CanFlyRide);
	}

	[Fact]
	public void CreatePlan_ReturnToBattleAlwaysUsesWorldMapNoReturnBattleFlag()
	{
		var worldMap = CreateWorldMap(WorldZoneAttributes.None);

		var allowed = WorldMapRegionZoneCapabilityService.CreatePlan(new WorldMapRegionZoneCapabilityContext(
			worldMap,
			CurrentWorldFlags: WorldZoneAttributes.None,
			WorldMapRegionZoneSortClassName.Sub,
			ZoneFlags: (int)WorldZoneAttributes.NoReturnBattle));
		var blocked = WorldMapRegionZoneCapabilityService.CreatePlan(new WorldMapRegionZoneCapabilityContext(
			worldMap,
			CurrentWorldFlags: WorldZoneAttributes.NoReturnBattle,
			WorldMapRegionZoneSortClassName.Sub,
			ZoneFlags: -1));

		Assert.True(allowed.CanReturnToBattle);
		Assert.False(blocked.CanReturnToBattle);
	}

	[Fact]
	public void CreatePlan_PvpZoneUsesZonePvpFlagAndNonPvpUsesWorldMapFlag()
	{
		var worldMap = CreateWorldMap(WorldZoneAttributes.None);

		var pvpZoneDisabled = WorldMapRegionZoneCapabilityService.CreatePlan(new WorldMapRegionZoneCapabilityContext(
			worldMap,
			CurrentWorldFlags: WorldZoneAttributes.PvpEnabled,
			WorldMapRegionZoneSortClassName.Pvp,
			ZoneFlags: 0));
		var pvpZoneEnabled = WorldMapRegionZoneCapabilityService.CreatePlan(new WorldMapRegionZoneCapabilityContext(
			worldMap,
			CurrentWorldFlags: WorldZoneAttributes.None,
			WorldMapRegionZoneSortClassName.Pvp,
			ZoneFlags: (int)WorldZoneAttributes.PvpEnabled));
		var nonPvpZone = WorldMapRegionZoneCapabilityService.CreatePlan(new WorldMapRegionZoneCapabilityContext(
			worldMap,
			CurrentWorldFlags: WorldZoneAttributes.PvpEnabled,
			WorldMapRegionZoneSortClassName.Sub,
			ZoneFlags: 0));

		Assert.False(pvpZoneDisabled.IsPvpAllowed);
		Assert.True(pvpZoneEnabled.IsPvpAllowed);
		Assert.True(nonPvpZone.IsPvpAllowed);
	}

	[Fact]
	public void CreatePlan_DuelZonesUseFlagsUnlessZeroOrWorldMapOverride()
	{
		var worldMap = CreateWorldMap(WorldZoneAttributes.DuelSameRaceEnabled);

		var duelZoneFlags = WorldMapRegionZoneCapabilityService.CreatePlan(new WorldMapRegionZoneCapabilityContext(
			worldMap,
			CurrentWorldFlags: WorldZoneAttributes.DuelSameRaceEnabled,
			WorldMapRegionZoneSortClassName.Duel,
			ZoneFlags: (int)WorldZoneAttributes.DuelOtherRaceEnabled));
		var duelZeroFlags = WorldMapRegionZoneCapabilityService.CreatePlan(new WorldMapRegionZoneCapabilityContext(
			worldMap,
			CurrentWorldFlags: WorldZoneAttributes.DuelSameRaceEnabled,
			WorldMapRegionZoneSortClassName.Duel,
			ZoneFlags: 0));
		var nonDuelZone = WorldMapRegionZoneCapabilityService.CreatePlan(new WorldMapRegionZoneCapabilityContext(
			worldMap,
			CurrentWorldFlags: WorldZoneAttributes.DuelSameRaceEnabled,
			WorldMapRegionZoneSortClassName.Sub,
			ZoneFlags: (int)WorldZoneAttributes.DuelOtherRaceEnabled));

		Assert.False(duelZoneFlags.IsSameRaceDuelAllowed);
		Assert.True(duelZoneFlags.IsOtherRaceDuelAllowed);
		Assert.True(duelZeroFlags.IsSameRaceDuelAllowed);
		Assert.False(duelZeroFlags.IsOtherRaceDuelAllowed);
		Assert.True(nonDuelZone.IsSameRaceDuelAllowed);
		Assert.False(nonDuelZone.IsOtherRaceDuelAllowed);
	}

	private static WorldMapSummary CreateWorldMap(WorldZoneAttributes templateFlags)
	{
		return new WorldMapSummary(
			MapId: 210010000,
			IsInstance: false,
			TwinCount: 1,
			Flags: templateFlags);
	}
}
