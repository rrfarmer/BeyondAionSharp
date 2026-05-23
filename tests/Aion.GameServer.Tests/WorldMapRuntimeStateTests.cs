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
}
