using System.Collections.Concurrent;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public interface IWorldNpcWalkerSpawnPlanCacheService
{
	int CachedWorldCount { get; }

	IReadOnlyList<WorldNpcWalkerWorldSpawnPlan> RefreshWorldPlans(
		IReadOnlyList<WorldNpc> npcs,
		WalkerTemplateTable walkerTemplates,
		WalkerVersionTable walkerVersions,
		IEnumerable<int>? worldIds = null);

	WorldNpcWalkerWorldSpawnPlan? GetWorldPlan(int worldId);

	void Clear();
}

public sealed class WorldNpcWalkerSpawnPlanCacheService : IWorldNpcWalkerSpawnPlanCacheService
{
	private readonly WorldNpcWalkerFormationOrganizerService _organizer;
	private readonly WorldNpcWalkerVariantSelectionService _variantSelection;
	private readonly WorldNpcWalkerPlacementPlanService _placementPlans;
	private readonly ConcurrentDictionary<int, WorldNpcWalkerWorldSpawnPlan> _plansByWorldId = new();

	public WorldNpcWalkerSpawnPlanCacheService()
		: this(new WorldNpcWalkerFormationOrganizerService(), new WorldNpcWalkerVariantSelectionService(), new WorldNpcWalkerPlacementPlanService())
	{
	}

	public WorldNpcWalkerSpawnPlanCacheService(
		WorldNpcWalkerFormationOrganizerService organizer,
		WorldNpcWalkerVariantSelectionService variantSelection,
		WorldNpcWalkerPlacementPlanService? placementPlans = null)
	{
		_organizer = organizer;
		_variantSelection = variantSelection;
		_placementPlans = placementPlans ?? new WorldNpcWalkerPlacementPlanService();
	}

	public int CachedWorldCount => _plansByWorldId.Count;

	public IReadOnlyList<WorldNpcWalkerWorldSpawnPlan> RefreshWorldPlans(
		IReadOnlyList<WorldNpc> npcs,
		WalkerTemplateTable walkerTemplates,
		WalkerVersionTable walkerVersions,
		IEnumerable<int>? worldIds = null)
	{
		// Java parity: spawnengine/InstanceWalkerFormations is world-map-instance scoped; this first cache is map scoped until instances exist.
		var targetWorldIds = worldIds?.Distinct().ToArray();
		if (targetWorldIds == null)
		{
			_plansByWorldId.Clear();
			targetWorldIds = npcs.Select(npc => npc.Position.WorldId).Distinct().ToArray();
		}

		var refreshedPlans = new List<WorldNpcWalkerWorldSpawnPlan>(targetWorldIds.Length);
		foreach (var worldId in targetWorldIds)
		{
			var worldNpcs = npcs
				.Where(npc => npc.Position.WorldId == worldId && !string.IsNullOrWhiteSpace(npc.WalkerId))
				.ToArray();
			if (worldNpcs.Length == 0)
			{
				_plansByWorldId.TryRemove(worldId, out _);
				continue;
			}

			var organization = _organizer.Organize(worldNpcs, walkerTemplates, walkerVersions);
			var spawnPlan = _variantSelection.CreateSpawnPlan(organization);
			var placementPlan = _placementPlans.CreatePlacementPlan(organization, spawnPlan, worldNpcs);
			var worldPlan = new WorldNpcWalkerWorldSpawnPlan(worldId, organization, spawnPlan, placementPlan);
			_plansByWorldId[worldId] = worldPlan;
			refreshedPlans.Add(worldPlan);
		}

		return refreshedPlans;
	}

	public WorldNpcWalkerWorldSpawnPlan? GetWorldPlan(int worldId)
	{
		return _plansByWorldId.GetValueOrDefault(worldId);
	}

	public void Clear()
	{
		_plansByWorldId.Clear();
	}
}

public sealed record WorldNpcWalkerWorldSpawnPlan(
	int WorldId,
	WorldNpcWalkerFormationOrganizationResult Organization,
	WorldNpcWalkerSpawnPlan SpawnPlan,
	WorldNpcWalkerPlacementPlan PlacementPlan);
