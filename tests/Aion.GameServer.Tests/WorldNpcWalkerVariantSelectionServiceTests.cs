using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class WorldNpcWalkerVariantSelectionServiceTests
{
	[Fact]
	public void CreateSpawnPlan_PreservesActiveWalkersAndChoosesOnePerVariantPool()
	{
		var service = new WorldNpcWalkerVariantSelectionService(count => count - 1);
		var activeWalker = Walker(1, "active-route");
		var activeFormation = Formation("active-formation", "");
		var variantFormationA = Formation("formation-a", "formation-parent");
		var variantFormationB = Formation("formation-b", "formation-parent");
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
			&& choice.CandidateCount == 2);
		Assert.Contains(plan.VariantChoices, choice =>
			choice.Kind == WorldNpcWalkerVariantKind.Walker
			&& choice.VersionRouteId == "walker-parent"
			&& choice.SelectedIndex == 1
			&& choice.CandidateCount == 2);
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
				["formation-parent"] = [Formation("formation-a", "formation-parent")],
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

	private static WorldNpcWalkerFormationResult Formation(string routeId, string versionRouteId)
	{
		return new WorldNpcWalkerFormationResult(
			WorldNpcWalkerFormationStatus.Ready,
			routeId,
			versionRouteId,
			[
				new WorldNpcWalkerFormationMember(1, 203000, 0, 0, 0, 0, 0),
			]);
	}
}
