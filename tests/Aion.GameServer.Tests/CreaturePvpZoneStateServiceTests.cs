using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class CreaturePvpZoneStateServiceTests
{
	[Theory]
	[InlineData(0, 0, true)]
	[InlineData(0, 1, false)]
	[InlineData(0, 2, true)]
	[InlineData(1, 1, true)]
	public void IsInsidePvpZoneMatchesJavaCreatureZoneCounterRule(
		int siegeZoneCount,
		int pvpZoneCount,
		bool expected)
	{
		Assert.Equal(expected, CreaturePvpZoneStateService.IsInsidePvpZone(siegeZoneCount, pvpZoneCount));
	}
}
