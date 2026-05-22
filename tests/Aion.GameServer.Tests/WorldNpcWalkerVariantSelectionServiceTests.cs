using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class WorldNpcWalkerVariantSelectionServiceTests
{
	[Fact]
	public void CreateSpawnPlan_PreservesActiveWalkersAndChoosesOnePerVariantPool()
	{
		var service = new WorldNpcWalkerVariantSelectionService(count => count - 1);
		var activeWalker = Walker(1, "active-route");
		var activeFormation = Formation("active-formation", "", 10);
		var variantFormationA = Formation("formation-a", "formation-parent", 11);
		var variantFormationB = Formation("formation-b", "formation-parent", 12);
		var variantWalkerA = Walker(2, "walker-a", "walker-parent");
		var variantWalkerB = Walker(3, "walker-b", "walker-parent");
		var organization = new WorldNpcWalkerFormationOrganizationResult(
			[activeWalker],
			[activeFormation],
			new Dictionary<string, IReadOnlyList<WorldNpcWalkerFormationResult>>
			{
				["formation-parent"] = [variantFormationA, variantFormationB],
			},
			new Dictionary<string, IReadOnlyList<WorldNpcWalkerSpawnCandidate>>
			{
				["walker-parent"] = [variantWalkerA, variantWalkerB],
			},
			[]);

		var plan = service.CreateSpawnPlan(organization);

		Assert.Equal(["active-route", "walker-b"], plan.Walkers.Select(walker => walker.RouteId).ToArray());
		Assert.Equal(["active-formation", "formation-b"], plan.Formations.Select(formation => formation.RouteId).ToArray());
		Assert.Equal(2, plan.VariantChoices.Count);
		Assert.Contains(plan.VariantChoices, choice =>
			choice.Kind == WorldNpcWalkerVariantKind.Formation
			&& choice.VersionRouteId == "formation-parent"
			&& choice.SelectedIndex == 1
			&& choice.CandidateCount == 2
			&& choice.ObjectIds.SequenceEqual([12]));
		Assert.Contains(plan.VariantChoices, choice =>
			choice.Kind == WorldNpcWalkerVariantKind.Walker
			&& choice.VersionRouteId == "walker-parent"
			&& choice.SelectedIndex == 1
			&& choice.CandidateCount == 2
			&& choice.ObjectIds.SequenceEqual([3]));
	}

	[Fact]
	public void CreateSpawnPlan_PreservesPreviousSpawnedVariantChoice()
	{
		var chooseLast = true;
		var service = new WorldNpcWalkerVariantSelectionService(count => chooseLast ? count - 1 : 0);
		var organization = CreateVersionedOrganization(
			Formation("formation-a", "formation-parent", 10),
			Formation("formation-b", "formation-parent", 20),
			Walker(30, "walker-a", "walker-parent"),
			Walker(40, "walker-b", "walker-parent"));

		var initialPlan = service.CreateSpawnPlan(organization);
		chooseLast = false;
		var refreshedPlan = service.CreateSpawnPlan(organization, initialPlan);

		Assert.Equal(["walker-b"], refreshedPlan.Walkers.Select(walker => walker.RouteId).ToArray());
		Assert.Equal(["formation-b"], refreshedPlan.Formations.Select(formation => formation.RouteId).ToArray());
		Assert.Contains(refreshedPlan.VariantChoices, choice =>
			choice.Kind == WorldNpcWalkerVariantKind.Formation
			&& choice.SelectedIndex == 1
			&& choice.ObjectIds.SequenceEqual([20]));
		Assert.Contains(refreshedPlan.VariantChoices, choice =>
			choice.Kind == WorldNpcWalkerVariantKind.Walker
			&& choice.SelectedIndex == 1
			&& choice.ObjectIds.SequenceEqual([40]));
	}

	[Fact]
	public void CreateSpawnPlan_RejectsInvalidVariantIndex()
	{
		var service = new WorldNpcWalkerVariantSelectionService(count => count);
		var organization = new WorldNpcWalkerFormationOrganizationResult(
			[],
			[],
			new Dictionary<string, IReadOnlyList<WorldNpcWalkerFormationResult>>
			{
				["formation-parent"] = [Formation("formation-a", "formation-parent", 1)],
			},
			new Dictionary<string, IReadOnlyList<WorldNpcWalkerSpawnCandidate>>(),
			[]);

		Assert.Throws<InvalidOperationException>(() => service.CreateSpawnPlan(organization));
	}

	private static WorldNpcWalkerSpawnCandidate Walker(
		int objectId,
		string routeId,
		string versionRouteId = "")
	{
		return new WorldNpcWalkerSpawnCandidate(
			objectId,
			203000,
			routeId,
			versionRouteId,
			0,
			0,
			0,
			0);
	}

	private static WorldNpcWalkerFormationOrganizationResult CreateVersionedOrganization(
		WorldNpcWalkerFormationResult formationA,
		WorldNpcWalkerFormationResult formationB,
		WorldNpcWalkerSpawnCandidate walkerA,
		WorldNpcWalkerSpawnCandidate walkerB)
	{
		return new WorldNpcWalkerFormationOrganizationResult(
			[],
			[],
			new Dictionary<string, IReadOnlyList<WorldNpcWalkerFormationResult>>
			{
				["formation-parent"] = [formationA, formationB],
			},
			new Dictionary<string, IReadOnlyList<WorldNpcWalkerSpawnCandidate>>
			{
				["walker-parent"] = [walkerA, walkerB],
			},
			[]);
	}

	private static WorldNpcWalkerFormationResult Formation(string routeId, string versionRouteId, int objectId)
	{
		return new WorldNpcWalkerFormationResult(
			WorldNpcWalkerFormationStatus.Ready,
			routeId,
			versionRouteId,
			[
				new WorldNpcWalkerFormationMember(objectId, 203000, 0, 0, 0, 0, 0),
			]);
	}
}
