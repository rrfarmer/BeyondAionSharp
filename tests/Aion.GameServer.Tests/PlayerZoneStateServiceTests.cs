using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class PlayerZoneStateServiceTests
{
	[Fact]
	public void RevalidateFlightZonesMatchesJavaWorldMapFlightSlice()
	{
		var worldMaps = new[]
		{
			new WorldMapSummary(400010000, IsInstance: false, TwinCount: 1, Flags: WorldZoneAttributes.Fly | WorldZoneAttributes.Glide),
			new WorldMapSummary(210010000, IsInstance: false, TwinCount: 5, Flags: WorldZoneAttributes.Glide),
		};
		var flyMapPlayer = new Player
		{
			Position = new WorldPosition(400010000, 0, 0, 0, 0),
			IsInsideNoFlyZone = true,
		};
		var nonFlyMapPlayer = new Player
		{
			Position = new WorldPosition(210010000, 0, 0, 0, 0),
			IsInsideFlyZone = true,
			IsInsideNoFlyZone = true,
		};
		var unknownMapPlayer = new Player
		{
			Position = new WorldPosition(123, 0, 0, 0, 0),
			IsInsideFlyZone = true,
			IsInsideNoFlyZone = true,
		};

		var flyMapResult = PlayerZoneStateService.RevalidateFlightZones(flyMapPlayer, worldMaps);
		Assert.True(flyMapResult.FoundWorldMap);
		Assert.True(flyMapResult.EnteredFlyZone);
		Assert.True(flyMapPlayer.IsInsideFlyZone);
		Assert.False(flyMapPlayer.IsInsideNoFlyZone);

		var nonFlyMapResult = PlayerZoneStateService.RevalidateFlightZones(nonFlyMapPlayer, worldMaps);
		Assert.True(nonFlyMapResult.FoundWorldMap);
		Assert.True(nonFlyMapResult.LeftFlyZone);
		Assert.True(nonFlyMapResult.LeftNoFlyZone);
		Assert.False(nonFlyMapPlayer.IsInsideFlyZone);
		Assert.False(nonFlyMapPlayer.IsInsideNoFlyZone);

		var unknownMapResult = PlayerZoneStateService.RevalidateFlightZones(unknownMapPlayer, worldMaps);
		Assert.False(unknownMapResult.FoundWorldMap);
		Assert.True(unknownMapResult.LeftFlyZone);
		Assert.True(unknownMapResult.LeftNoFlyZone);
		Assert.False(unknownMapPlayer.IsInsideFlyZone);
		Assert.False(unknownMapPlayer.IsInsideNoFlyZone);
	}

	[Fact]
	public void RevalidateFlightZonesMatchesJavaPolygonFlyAndNoFlySlice()
	{
		var worldMaps = new[]
		{
			new WorldMapSummary(210020000, IsInstance: false, TwinCount: 1, Flags: WorldZoneAttributes.Glide),
			new WorldMapSummary(400020000, IsInstance: false, TwinCount: 1, Flags: WorldZoneAttributes.Fly | WorldZoneAttributes.Glide),
		};
		var flightZones = new FlightZoneTable(
		[
			new FlightZoneSummary(
				210020000,
				"FLYINGZONESHAPE1_4_210020000",
				FlightZoneType.Fly,
				Flags: -1,
				Bottom: 0,
				Top: 100,
				Points: [new ZonePoint2D(0, 0), new ZonePoint2D(10, 0), new ZonePoint2D(10, 10), new ZonePoint2D(0, 10)]),
			new FlightZoneSummary(
				400020000,
				"GAB1_01_FLYING_ZONE01_400020000",
				FlightZoneType.NoFly,
				Flags: 48,
				Bottom: 0,
				Top: 100,
				Points: [new ZonePoint2D(20, 20), new ZonePoint2D(30, 20), new ZonePoint2D(30, 30), new ZonePoint2D(20, 30)]),
		]);
		var localFlyPlayer = new Player { Position = new WorldPosition(210020000, 5, 5, 50, 0) };
		var abyssNoFlyPlayer = new Player { Position = new WorldPosition(400020000, 25, 25, 50, 0) };
		var aboveZonePlayer = new Player { Position = new WorldPosition(210020000, 5, 5, 150, 0) };

		var localFlyResult = PlayerZoneStateService.RevalidateFlightZones(localFlyPlayer, worldMaps, flightZones);
		Assert.True(localFlyResult.FoundWorldMap);
		Assert.True(localFlyResult.EnteredFlyZone);
		Assert.True(localFlyPlayer.IsInsideFlyZone);
		Assert.False(localFlyPlayer.IsInsideNoFlyZone);

		var abyssNoFlyResult = PlayerZoneStateService.RevalidateFlightZones(abyssNoFlyPlayer, worldMaps, flightZones);
		Assert.True(abyssNoFlyResult.FoundWorldMap);
		Assert.True(abyssNoFlyResult.EnteredFlyZone);
		Assert.True(abyssNoFlyResult.EnteredNoFlyZone);
		Assert.True(abyssNoFlyPlayer.IsInsideFlyZone);
		Assert.True(abyssNoFlyPlayer.IsInsideNoFlyZone);

		var aboveZoneResult = PlayerZoneStateService.RevalidateFlightZones(aboveZonePlayer, worldMaps, flightZones);
		Assert.True(aboveZoneResult.FoundWorldMap);
		Assert.False(aboveZoneResult.EnteredFlyZone);
		Assert.False(aboveZonePlayer.IsInsideFlyZone);
		Assert.False(aboveZonePlayer.IsInsideNoFlyZone);
	}

	[Fact]
	public void ApplyFlightZoneTransitionIntentMatchesJavaFlyAreaFpTaskSlice()
	{
		var worldMaps = new[]
		{
			new WorldMapSummary(400020000, IsInstance: false, TwinCount: 1, Flags: WorldZoneAttributes.Fly | WorldZoneAttributes.Glide),
		};
		var noFlyZones = new FlightZoneTable(
		[
			new FlightZoneSummary(
				400020000,
				"GAB1_01_FLYING_ZONE01_400020000",
				FlightZoneType.NoFly,
				Flags: 48,
				Bottom: 0,
				Top: 100,
				Points: [new ZonePoint2D(20, 20), new ZonePoint2D(30, 20), new ZonePoint2D(30, 30), new ZonePoint2D(20, 30)]),
		]);
		var flyingIntoNoFly = new Player
		{
			Position = new WorldPosition(400020000, 25, 25, 50, 0),
			IsInsideFlyZone = true,
			FlyState = PlayerFlyState.Flying,
			CreatureState = PlayerCreatureState.Flying,
			IsFpReduceActive = true,
		};
		var glidingIntoNoFly = new Player
		{
			Position = new WorldPosition(400020000, 25, 25, 50, 0),
			IsInsideFlyZone = true,
			FlyState = PlayerFlyState.Flying | PlayerFlyState.Gliding,
			CreatureState = PlayerCreatureState.Flying | PlayerCreatureState.Gliding,
			IsFpRestoreActive = true,
		};
		var leavingNoFly = new Player
		{
			Position = new WorldPosition(400020000, 10, 10, 50, 0),
			IsInsideFlyZone = true,
			IsInsideNoFlyZone = true,
			FlyState = PlayerFlyState.Gliding,
			CreatureState = PlayerCreatureState.Gliding,
			IsFpRestoreActive = true,
		};
		var walkingIntoFlyArea = new Player { Position = new WorldPosition(400020000, 10, 10, 50, 0) };
		var freeFlightAdmin = new Player
		{
			AccessLevel = 1,
			Position = new WorldPosition(400020000, 25, 25, 50, 0),
			IsInsideFlyZone = true,
			FlyState = PlayerFlyState.Flying,
			CreatureState = PlayerCreatureState.Flying,
			IsFpReduceActive = true,
		};

		var flyingNoFlyResult = PlayerZoneStateService.RevalidateFlightZones(flyingIntoNoFly, worldMaps, noFlyZones);
		var flyingNoFlyIntent = PlayerZoneStateService.ApplyFlightZoneTransitionIntent(flyingIntoNoFly, flyingNoFlyResult);
		Assert.True(flyingNoFlyIntent.LeftValidFlyArea);
		Assert.Equal(PlayerLeaveFlyAreaStatus.EndedFlying, flyingNoFlyIntent.LeaveStatus);
		Assert.False(flyingIntoNoFly.IsFlying());
		Assert.False(flyingIntoNoFly.IsInState(PlayerCreatureState.Flying));
		Assert.False(flyingIntoNoFly.IsFpReduceActive);
		Assert.True(flyingIntoNoFly.IsFpRestoreActive);

		var glidingNoFlyResult = PlayerZoneStateService.RevalidateFlightZones(glidingIntoNoFly, worldMaps, noFlyZones);
		var glidingNoFlyIntent = PlayerZoneStateService.ApplyFlightZoneTransitionIntent(glidingIntoNoFly, glidingNoFlyResult);
		Assert.True(glidingNoFlyIntent.LeftValidFlyArea);
		Assert.Equal(PlayerLeaveFlyAreaStatus.ContinueGliding, glidingNoFlyIntent.LeaveStatus);
		Assert.False(glidingIntoNoFly.IsInFlyingState());
		Assert.True(glidingIntoNoFly.IsInGlidingState());
		Assert.False(glidingIntoNoFly.IsInState(PlayerCreatureState.Flying));
		Assert.True(glidingIntoNoFly.IsInState(PlayerCreatureState.Gliding));
		Assert.True(glidingNoFlyIntent.FpReduceTriggered);
		Assert.True(glidingIntoNoFly.IsFpReduceActive);
		Assert.False(glidingIntoNoFly.IsFpRestoreActive);

		var leavingNoFlyResult = PlayerZoneStateService.RevalidateFlightZones(leavingNoFly, worldMaps, noFlyZones);
		var leavingNoFlyIntent = PlayerZoneStateService.ApplyFlightZoneTransitionIntent(leavingNoFly, leavingNoFlyResult);
		Assert.True(leavingNoFlyIntent.EnteredValidFlyArea);
		Assert.True(leavingNoFlyIntent.FpReduceTriggered);
		Assert.True(leavingNoFly.IsFpReduceActive);
		Assert.False(leavingNoFly.IsFpRestoreActive);

		var walkingFlyResult = PlayerZoneStateService.RevalidateFlightZones(walkingIntoFlyArea, worldMaps, noFlyZones);
		var walkingFlyIntent = PlayerZoneStateService.ApplyFlightZoneTransitionIntent(walkingIntoFlyArea, walkingFlyResult);
		Assert.True(walkingFlyIntent.EnteredValidFlyArea);
		Assert.False(walkingFlyIntent.FpReduceTriggered);
		Assert.False(walkingIntoFlyArea.IsFpReduceActive);
		Assert.False(walkingIntoFlyArea.IsFpRestoreActive);

		var freeFlightResult = PlayerZoneStateService.RevalidateFlightZones(freeFlightAdmin, worldMaps, noFlyZones);
		var freeFlightIntent = PlayerZoneStateService.ApplyFlightZoneTransitionIntent(freeFlightAdmin, freeFlightResult);
		Assert.Equal(PlayerLeaveFlyAreaStatus.FreeFlightAccess, freeFlightIntent.LeaveStatus);
		Assert.True(freeFlightAdmin.IsInFlyingState());
		Assert.True(freeFlightAdmin.IsFpReduceActive);
		Assert.False(freeFlightAdmin.IsFpRestoreActive);
	}
}
