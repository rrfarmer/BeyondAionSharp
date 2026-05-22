using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.World;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Services;

public sealed class WorldNpcWalkerPlacementApplicationService
{
	public WorldNpcWalkerPlacementApplicationResult ApplyActivePlacements(
		GameWorld world,
		WorldNpcWalkerPlacementPlan placementPlan)
	{
		// Java parity: spawnengine/InstanceWalkerFormations spawns selected variants and leaves unselected variants unspawned.
		var updatedObjectIds = new List<int>();
		var missingObjectIds = new List<int>();
		var removedInactiveNpcs = new List<WorldNpc>();
		foreach (var placement in placementPlan.ActivePlacements)
		{
			if (!world.TryGetObject(placement.ObjectId, out var gameObject) || gameObject is not WorldNpc npc)
			{
				missingObjectIds.Add(placement.ObjectId);
				continue;
			}

			var updatedNpc = npc with
			{
				Position = new WorldPosition(
					npc.Position.WorldId,
					placement.X,
					placement.Y,
					placement.Z,
					placement.Heading),
			};
			if (world.TryUpdateObject(placement.ObjectId, updatedNpc))
				updatedObjectIds.Add(placement.ObjectId);
			else
				missingObjectIds.Add(placement.ObjectId);
		}

		foreach (var objectId in placementPlan.InactiveVariantObjectIds)
		{
			if (world.TryRemoveObject(objectId, out var gameObject) && gameObject is WorldNpc npc)
				removedInactiveNpcs.Add(npc);
		}

		return new WorldNpcWalkerPlacementApplicationResult(updatedObjectIds, missingObjectIds, removedInactiveNpcs);
	}
}

public sealed record WorldNpcWalkerPlacementApplicationResult(
	IReadOnlyList<int> UpdatedObjectIds,
	IReadOnlyList<int> MissingObjectIds,
	IReadOnlyList<WorldNpc> RemovedInactiveNpcs);
