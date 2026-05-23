using Aion.GameServer.Dataholders;
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
}
