using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class WorldMapRegionMaterialZoneSavePlanServiceTests
{
	[Fact]
	public void CreatePlan_FiltersZonesWithoutCollidableHandlersAndSortsTemplatesByMapId()
	{
		var context = new WorldMapRegionMaterialZoneSaveContext(
			WorldMapIds: [300, 100, 200],
			Zones:
			[
				new(300,
				[
					Zone("HANDLED_300", "TEMPLATE_300", 300),
					Zone("IGNORED_300", "IGNORED_TEMPLATE", 300),
				]),
				new(100,
				[
					Zone("HANDLED_100", "TEMPLATE_100", 100),
				]),
				new(200,
				[
					Zone("HANDLED_200", "TEMPLATE_200", 200),
				]),
			],
			CollidableHandlerZoneNames: ["HANDLED_300", "HANDLED_100", "HANDLED_200"]);

		var plan = WorldMapRegionMaterialZoneSavePlanService.CreatePlan(context);

		Assert.Equal([100, 200, 300], plan.Templates.Select(template => template.MapId));
		Assert.Equal(["TEMPLATE_100", "TEMPLATE_200", "TEMPLATE_300"], plan.Templates.Select(template => template.ZoneName));
		Assert.DoesNotContain(plan.Templates, template => template.ZoneName == "IGNORED_TEMPLATE");
		Assert.Equal([300, 100, 200], plan.VisitedMapIds);
		Assert.Empty(plan.SkippedMapIds);
		Assert.Equal(WorldMapRegionMaterialZoneSavePlanService.GeneratedZonesPath, plan.PersistencePath);
		Assert.Contains("ZoneData.saveData", plan.JavaSource);
	}

	[Fact]
	public void CreatePlan_SkipsWorldMapsWithoutZoneInfoAndPreservesSameMapOrder()
	{
		var context = new WorldMapRegionMaterialZoneSaveContext(
			WorldMapIds: [100, 200],
			Zones:
			[
				new(200,
				[
					Zone("HANDLED_A", "FIRST_200", 200),
					Zone("HANDLED_B", "SECOND_200", 200),
				]),
			],
			CollidableHandlerZoneNames: ["HANDLED_A", "HANDLED_B"]);

		var plan = WorldMapRegionMaterialZoneSavePlanService.CreatePlan(context);

		Assert.Equal([100], plan.SkippedMapIds);
		Assert.Equal(["FIRST_200", "SECOND_200"], plan.Templates.Select(template => template.ZoneName));
	}

	private static WorldMapRegionMaterialZoneInfoSnapshot Zone(
		string areaZoneName,
		string templateZoneName,
		int templateMapId)
	{
		return new WorldMapRegionMaterialZoneInfoSnapshot(
			areaZoneName,
			new WorldMapRegionMaterialZoneTemplateSnapshot(templateZoneName, templateMapId));
	}
}
