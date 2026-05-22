using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public sealed class WorldNpcWalkerPlacementPlanService
{
	public WorldNpcWalkerPlacementPlan CreatePlacementPlan(
		WorldNpcWalkerFormationOrganizationResult organization,
		WorldNpcWalkerSpawnPlan spawnPlan,
		IReadOnlyList<WorldNpc> npcs)
	{
		// Java parity: spawnengine/InstanceWalkerFormations spawns only the active unversioned candidates and selected version variants.
		var npcsByObjectId = npcs.ToDictionary(npc => npc.ObjectId);
		var placements = new List<WorldNpcWalkerPlacement>();
		foreach (var walker in spawnPlan.Walkers)
		{
			placements.Add(new WorldNpcWalkerPlacement(
				walker.ObjectId,
				walker.TemplateId,
				walker.RouteId,
				IsFormationMember: false,
				walker.X,
				walker.Y,
				walker.Z,
				walker.Heading));
		}

		foreach (var formation in spawnPlan.Formations)
		{
			foreach (var member in formation.Members)
			{
				var sourceNpc = npcsByObjectId.GetValueOrDefault(member.ObjectId);
				var spawnLocation = sourceNpc?.SpawnLocation;
				placements.Add(new WorldNpcWalkerPlacement(
					member.ObjectId,
					member.TemplateId,
					formation.RouteId,
					IsFormationMember: true,
					member.X,
					member.Y,
					spawnLocation?.Z ?? 0,
					spawnLocation?.Heading ?? 0));
			}
		}

		return new WorldNpcWalkerPlacementPlan(
			placements,
			GetInactiveVariantObjectIds(organization, spawnPlan));
	}

	private static IReadOnlyList<int> GetInactiveVariantObjectIds(
		WorldNpcWalkerFormationOrganizationResult organization,
		WorldNpcWalkerSpawnPlan spawnPlan)
	{
		var activeObjectIds = spawnPlan.Walkers
			.Select(walker => walker.ObjectId)
			.Concat(spawnPlan.Formations.SelectMany(formation => formation.Members.Select(member => member.ObjectId)))
			.ToHashSet();

		return organization.WalkerVariants.Values
			.SelectMany(variants => variants.Select(walker => walker.ObjectId))
			.Concat(organization.FormationVariants.Values.SelectMany(variants => variants.SelectMany(formation => formation.Members.Select(member => member.ObjectId))))
			.Where(objectId => !activeObjectIds.Contains(objectId))
			.Distinct()
			.OrderBy(objectId => objectId)
			.ToArray();
	}
}

public sealed record WorldNpcWalkerPlacementPlan(
	IReadOnlyList<WorldNpcWalkerPlacement> ActivePlacements,
	IReadOnlyList<int> InactiveVariantObjectIds);

public sealed record WorldNpcWalkerPlacement(
	int ObjectId,
	int TemplateId,
	string RouteId,
	bool IsFormationMember,
	float X,
	float Y,
	float Z,
	byte Heading);
