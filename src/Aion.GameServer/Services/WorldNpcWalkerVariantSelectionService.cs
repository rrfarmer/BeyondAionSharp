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

	public WorldNpcWalkerSpawnPlan CreateSpawnPlan(
		WorldNpcWalkerFormationOrganizationResult organization,
		WorldNpcWalkerSpawnPlan? previousSpawnPlan = null)
	{
		// Java parity: spawnengine/InstanceWalkerFormations.organizeAndSpawn activates one Rnd.get entry per versioned group/walker pool.
		var walkers = organization.ActiveWalkers.ToList();
		var formations = organization.ActiveFormations.ToList();
		var choices = new List<WorldNpcWalkerVariantChoice>();

		foreach (var (versionRouteId, variants) in organization.FormationVariants)
		{
			if (variants.Count == 0)
				continue;

			var selectedIndex = SelectFormationIndex(versionRouteId, variants, previousSpawnPlan);
			var formation = variants[selectedIndex];
			formations.Add(formation);
			choices.Add(new WorldNpcWalkerVariantChoice(
				WorldNpcWalkerVariantKind.Formation,
				versionRouteId,
				selectedIndex,
				variants.Count,
				formation.Members.Select(member => member.ObjectId).ToArray()));
		}

		foreach (var (versionRouteId, variants) in organization.WalkerVariants)
		{
			if (variants.Count == 0)
				continue;

			var selectedIndex = SelectWalkerIndex(versionRouteId, variants, previousSpawnPlan);
			var walker = variants[selectedIndex];
			walkers.Add(walker);
			choices.Add(new WorldNpcWalkerVariantChoice(
				WorldNpcWalkerVariantKind.Walker,
				versionRouteId,
				selectedIndex,
				variants.Count,
				[walker.ObjectId]));
		}

		return new WorldNpcWalkerSpawnPlan(walkers, formations, choices);
	}

	private int SelectFormationIndex(
		string versionRouteId,
		IReadOnlyList<WorldNpcWalkerFormationResult> variants,
		WorldNpcWalkerSpawnPlan? previousSpawnPlan)
	{
		// Java parity: InstanceWalkerFormations keeps WalkerGroup.isSpawned state instead of re-rolling variants on each refresh.
		var previousChoice = FindPreviousChoice(previousSpawnPlan, WorldNpcWalkerVariantKind.Formation, versionRouteId);
		if (previousChoice != null)
		{
			var previousObjectIds = previousChoice.ObjectIds.ToHashSet();
			for (var index = 0; index < variants.Count; index++)
			{
				if (previousObjectIds.SetEquals(variants[index].Members.Select(member => member.ObjectId)))
					return index;
			}
		}

		return SelectIndex(variants.Count);
	}

	private int SelectWalkerIndex(
		string versionRouteId,
		IReadOnlyList<WorldNpcWalkerSpawnCandidate> variants,
		WorldNpcWalkerSpawnPlan? previousSpawnPlan)
	{
		// Java parity: InstanceWalkerFormations.changeWalker toggles spawned NPCs; refreshes keep the currently spawned variant.
		var previousChoice = FindPreviousChoice(previousSpawnPlan, WorldNpcWalkerVariantKind.Walker, versionRouteId);
		var previousObjectId = previousChoice?.ObjectIds.Count == 1 ? previousChoice.ObjectIds[0] : 0;
		if (previousObjectId != 0)
		{
			for (var index = 0; index < variants.Count; index++)
			{
				if (variants[index].ObjectId == previousObjectId)
					return index;
			}
		}

		return SelectIndex(variants.Count);
	}

	private int SelectIndex(int count)
	{
		var selectedIndex = _nextIndex(count);
		if (selectedIndex < 0 || selectedIndex >= count)
			throw new InvalidOperationException($"Walker variant index {selectedIndex} is outside variant pool size {count}.");
		return selectedIndex;
	}

	private static WorldNpcWalkerVariantChoice? FindPreviousChoice(
		WorldNpcWalkerSpawnPlan? previousSpawnPlan,
		WorldNpcWalkerVariantKind kind,
		string versionRouteId)
	{
		return previousSpawnPlan?.VariantChoices.FirstOrDefault(choice =>
			choice.Kind == kind && string.Equals(choice.VersionRouteId, versionRouteId, StringComparison.Ordinal));
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
	int CandidateCount,
	IReadOnlyList<int> ObjectIds);

public enum WorldNpcWalkerVariantKind
{
	Formation,
	Walker,
}
