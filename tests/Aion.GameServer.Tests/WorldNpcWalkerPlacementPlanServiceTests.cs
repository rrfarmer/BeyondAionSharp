using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class WorldNpcWalkerPlacementPlanServiceTests
{
	[Fact]
	public void CreatePlacementPlan_SeparatesActivePlacementsFromInactiveVariants()
	{
		var service = new WorldNpcWalkerPlacementPlanService();
		var activeWalker = Walker(1, "route-active", heading: 9);
		var inactiveWalker = Walker(2, "route-walker-a");
		var selectedWalker = Walker(3, "route-walker-b", "walker-parent", heading: 11);
		var inactiveFormation = Formation("route-formation-a", "formation-parent", Member(4), Member(5));
		var selectedFormation = Formation(
			"route-formation-b",
			"formation-parent",
			Member(6, x: 10, y: 20),
			Member(7, x: 11, y: 21));
		var organization = new WorldNpcWalkerFormationOrganizationResult(
			[activeWalker],
			[],
			new Dictionary<string, IReadOnlyList<WorldNpcWalkerFormationResult>>
			{
				["formation-parent"] = [inactiveFormation, selectedFormation],
			},
			new Dictionary<string, IReadOnlyList<WorldNpcWalkerSpawnCandidate>>
			{
				["walker-parent"] = [inactiveWalker, selectedWalker],
			},
			[]);
		var spawnPlan = new WorldNpcWalkerSpawnPlan(
			[activeWalker, selectedWalker],
			[selectedFormation],
			[]);
		var npcs = new[]
		{
			Npc(4, z: 4, heading: 4),
			Npc(5, z: 5, heading: 5),
			Npc(6, z: 6, heading: 6),
			Npc(7, z: 7, heading: 7),
		};

		var placementPlan = service.CreatePlacementPlan(organization, spawnPlan, npcs);

		Assert.Equal([2, 4, 5], placementPlan.InactiveVariantObjectIds);
		Assert.Equal([1, 3, 6, 7], placementPlan.ActivePlacements.Select(placement => placement.ObjectId).ToArray());
		Assert.Contains(placementPlan.ActivePlacements, placement =>
			placement.ObjectId == 1
			&& !placement.IsFormationMember
			&& placement.Heading == 9);
		Assert.Contains(placementPlan.ActivePlacements, placement =>
			placement.ObjectId == 3
			&& !placement.IsFormationMember
			&& placement.Heading == 11);
		Assert.Contains(placementPlan.ActivePlacements, placement =>
			placement.ObjectId == 6
			&& placement.IsFormationMember
			&& placement.X == 10
			&& placement.Y == 20
			&& placement.Z == 6
			&& placement.Heading == 6);
		Assert.Contains(placementPlan.ActivePlacements, placement =>
			placement.ObjectId == 7
			&& placement.IsFormationMember
			&& placement.X == 11
			&& placement.Y == 21
			&& placement.Z == 7
			&& placement.Heading == 7);
	}

	private static WorldNpcWalkerSpawnCandidate Walker(
		int objectId,
		string routeId,
		string versionRouteId = "",
		byte heading = 0)
	{
		return new WorldNpcWalkerSpawnCandidate(
			objectId,
			203000,
			routeId,
			versionRouteId,
			0,
			0,
			0,
			0,
			heading);
	}

	private static WorldNpcWalkerFormationResult Formation(
		string routeId,
		string versionRouteId,
		params WorldNpcWalkerFormationMember[] members)
	{
		return new WorldNpcWalkerFormationResult(
			WorldNpcWalkerFormationStatus.Ready,
			routeId,
			versionRouteId,
			members);
	}

	private static WorldNpcWalkerFormationMember Member(int objectId, float x = 0, float y = 0)
	{
		return new WorldNpcWalkerFormationMember(
			objectId,
			203000,
			0,
			x,
			y,
			0,
			0);
	}

	private static WorldNpc Npc(int objectId, float z, byte heading)
	{
		return new WorldNpc(
			ObjectId: objectId,
			TemplateId: 203000,
			Template: new NpcTemplateSummary(
				203000,
				$"walker-{objectId}",
				NameId: 203000,
				Level: 1,
				Rank: "NORMAL",
				Rating: "NORMAL",
				Race: "ELYOS",
				Tribe: "GENERAL",
				Type: "GENERAL"),
			Position: new WorldPosition(210010000, 0, 0, z, heading));
	}
}
