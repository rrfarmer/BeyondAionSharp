namespace Aion.GameServer.Services;

public sealed class WorldNpcWalkerVariantSelectionService
{
	private readonly Func<int, int> _nextIndex;

	public WorldNpcWalkerVariantSelectionService()
		: this(count => Random.Shared.Next(count))
	{
	}

	public WorldNpcWalkerVariantSelectionService(Func<int, int> nextIndex)
	{
		_nextIndex = nextIndex;
	}

	public WorldNpcWalkerSpawnPlan CreateSpawnPlan(WorldNpcWalkerFormationOrganizationResult organization)
	{
		// Java parity: spawnengine/InstanceWalkerFormations.organizeAndSpawn activates one Rnd.get entry per versioned group/walker pool.
		var walkers = organization.ActiveWalkers.ToList();
		var formations = organization.ActiveFormations.ToList();
		var choices = new List<WorldNpcWalkerVariantChoice>();

		foreach (var (versionRouteId, variants) in organization.FormationVariants)
		{
			if (variants.Count == 0)
				continue;

			var selectedIndex = SelectIndex(variants.Count);
			formations.Add(variants[selectedIndex]);
			choices.Add(new WorldNpcWalkerVariantChoice(
				WorldNpcWalkerVariantKind.Formation,
				versionRouteId,
				selectedIndex,
				variants.Count));
		}

		foreach (var (versionRouteId, variants) in organization.WalkerVariants)
		{
			if (variants.Count == 0)
				continue;

			var selectedIndex = SelectIndex(variants.Count);
			walkers.Add(variants[selectedIndex]);
			choices.Add(new WorldNpcWalkerVariantChoice(
				WorldNpcWalkerVariantKind.Walker,
				versionRouteId,
				selectedIndex,
				variants.Count));
		}

		return new WorldNpcWalkerSpawnPlan(walkers, formations, choices);
	}

	private int SelectIndex(int count)
	{
		var selectedIndex = _nextIndex(count);
		if (selectedIndex < 0 || selectedIndex >= count)
			throw new InvalidOperationException($"Walker variant index {selectedIndex} is outside variant pool size {count}.");
		return selectedIndex;
	}
}

public sealed record WorldNpcWalkerSpawnPlan(
	IReadOnlyList<WorldNpcWalkerSpawnCandidate> Walkers,
	IReadOnlyList<WorldNpcWalkerFormationResult> Formations,
	IReadOnlyList<WorldNpcWalkerVariantChoice> VariantChoices);

public sealed record WorldNpcWalkerVariantChoice(
	WorldNpcWalkerVariantKind Kind,
	string VersionRouteId,
	int SelectedIndex,
	int CandidateCount);

public enum WorldNpcWalkerVariantKind
{
	Formation,
	Walker,
}
