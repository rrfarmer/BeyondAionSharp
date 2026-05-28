using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class WorldMapRegionZoneSortServiceTests
{
	[Fact]
	public void SortByJavaMapRegionOrder_OrdersByZoneClassThenPriorityThenZoneNameId()
	{
		var zones = new[]
		{
			Create("pvp-low-name", WorldMapRegionZoneSortClassName.Pvp, priority: 0, zoneName: "SANCTUM"),
			Create("sub-high-priority", WorldMapRegionZoneSortClassName.Sub, priority: 10, zoneName: "Z_SUB"),
			Create("dummy-high-priority", WorldMapRegionZoneSortClassName.Dummy, priority: 99, zoneName: "Z_DUMMY"),
			Create("sub-higher-zone-name-id", WorldMapRegionZoneSortClassName.Sub, priority: 1, zoneName: "LF1_ITEMUSEAREA_Q10020"),
			Create("sub-lower-zone-name-id", WorldMapRegionZoneSortClassName.Sub, priority: 1, zoneName: "NONE"),
			Create("fort", WorldMapRegionZoneSortClassName.Fort, priority: 0, zoneName: "FORTRESS"),
		};

		var sorted = WorldMapRegionZoneSortService.SortByJavaMapRegionOrder(zones);

		Assert.Equal(
		[
			"dummy-high-priority",
			"sub-lower-zone-name-id",
			"sub-higher-zone-name-id",
			"sub-high-priority",
			"fort",
			"pvp-low-name",
		], sorted.Select(zone => zone.ZoneId));
	}

	[Fact]
	public void SortByJavaMapRegionOrder_PreservesInputOrderForEquivalentComparatorKeys()
	{
		var zones = new[]
		{
			Create("first", WorldMapRegionZoneSortClassName.Fly, priority: 3, zoneName: "SAME"),
			Create("second", WorldMapRegionZoneSortClassName.Fly, priority: 3, zoneName: "SAME"),
			Create("third", WorldMapRegionZoneSortClassName.Fly, priority: 3, zoneName: "SAME"),
		};

		var sorted = WorldMapRegionZoneSortService.SortByJavaMapRegionOrder(zones);

		Assert.Equal(["first", "second", "third"], sorted.Select(zone => zone.ZoneId));
	}

	[Theory]
	[InlineData("NONE", 2402104)]
	[InlineData("none", 2402104)]
	[InlineData("LF1_ITEMUSEAREA_Q10020", 1527320452)]
	[InlineData("SANCTUM", -1711596919)]
	public void GetJavaZoneNameId_UsesUppercaseJavaStringHashCode(string zoneName, int expectedId)
	{
		var zoneNameId = WorldMapRegionZoneSortService.GetJavaZoneNameId(zoneName);

		Assert.Equal(expectedId, zoneNameId);
	}

	private static WorldMapRegionZoneSortCandidate Create(
		string zoneId,
		WorldMapRegionZoneSortClassName zoneClassName,
		int priority,
		string zoneName)
	{
		return new WorldMapRegionZoneSortCandidate(
			zoneId,
			zoneClassName,
			priority,
			WorldMapRegionZoneSortService.GetJavaZoneNameId(zoneName));
	}
}
