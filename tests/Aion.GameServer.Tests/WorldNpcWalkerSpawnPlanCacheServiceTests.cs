using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class WorldNpcWalkerSpawnPlanCacheServiceTests
{
	[Fact]
	public void RefreshWorldPlans_CachesSelectedWalkerPlansByWorld()
	{
		var cache = new WorldNpcWalkerSpawnPlanCacheService(
			new WorldNpcWalkerFormationOrganizerService(),
			new WorldNpcWalkerVariantSelectionService(count => count - 1));
		var templates = new WalkerTemplateTable(
		[
			new WalkerTemplateSummary("route-a", 2, "SQUARE", "NORMAL", [2], CreateRouteSteps()),
			new WalkerTemplateSummary("route-v1", 1, "POINT", "NORMAL", [], CreateRouteSteps()),
			new WalkerTemplateSummary("route-v2", 1, "POINT", "NORMAL", [], CreateRouteSteps()),
		]);
		var versions = new WalkerVersionTable(
			new Dictionary<string, string>
			{
				["route-v1"] = "route-parent",
				["route-v2"] = "route-parent",
			});
		var npcs = new[]
		{
			CreateNpc(1, 210010000, "route-a", walkerIndex: 1),
			CreateNpc(2, 210010000, "route-a", walkerIndex: 2),
			CreateNpc(3, 220010000, "route-v1", walkerIndex: 0),
			CreateNpc(4, 220010000, "route-v2", walkerIndex: 0),
		};

		var refreshed = cache.RefreshWorldPlans(npcs, templates, versions);

		Assert.Equal(2, cache.CachedWorldCount);
		Assert.Equal([210010000, 220010000], refreshed.Select(plan => plan.WorldId).OrderBy(id => id).ToArray());
		var formationPlan = cache.GetWorldPlan(210010000);
		Assert.NotNull(formationPlan);
		var formation = Assert.Single(formationPlan.SpawnPlan.Formations);
		Assert.Equal([2, 1], formation.Members.Select(member => member.ObjectId).ToArray());
		var variantPlan = cache.GetWorldPlan(220010000);
		Assert.NotNull(variantPlan);
		var selectedWalker = Assert.Single(variantPlan.SpawnPlan.Walkers);
		Assert.Equal(4, selectedWalker.ObjectId);
		var choice = Assert.Single(variantPlan.SpawnPlan.VariantChoices);
		Assert.Equal("route-parent", choice.VersionRouteId);
		Assert.Equal(1, choice.SelectedIndex);
	}

	[Fact]
	public void RefreshWorldPlans_RemovesStaleWorldPlans()
	{
		var cache = new WorldNpcWalkerSpawnPlanCacheService();
		var templates = new WalkerTemplateTable(
		[
			new WalkerTemplateSummary("route-a", 1, "POINT", "NORMAL", [], CreateRouteSteps()),
		]);
		var versions = new WalkerVersionTable(new Dictionary<string, string>());

		cache.RefreshWorldPlans([CreateNpc(1, 210010000, "route-a", walkerIndex: 0)], templates, versions);
		cache.RefreshWorldPlans([], templates, versions, [210010000]);

		Assert.Equal(0, cache.CachedWorldCount);
		Assert.Null(cache.GetWorldPlan(210010000));
	}

	private static IReadOnlyList<WalkerRouteStepSummary> CreateRouteSteps()
	{
		return
		[
			new WalkerRouteStepSummary(0, 0, 0, 0, 0, false),
			new WalkerRouteStepSummary(10, 0, 0, 0, 1, true),
		];
	}

	private static WorldNpc CreateNpc(int objectId, int worldId, string walkerId, int walkerIndex)
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
			Position: new WorldPosition(worldId, 0, 0, 0, 0),
			WalkerId: walkerId,
			WalkerIndex: walkerIndex);
	}
}
