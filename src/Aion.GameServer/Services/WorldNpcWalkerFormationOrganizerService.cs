using System.Collections.ObjectModel;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public sealed class WorldNpcWalkerFormationOrganizerService
{
	private readonly WorldNpcWalkerRouteService _routeService;
	private readonly WorldNpcWalkerFormationService _formationService;

	public WorldNpcWalkerFormationOrganizerService()
		: this(new WorldNpcWalkerRouteService(), new WorldNpcWalkerFormationService())
	{
	}

	public WorldNpcWalkerFormationOrganizerService(
		WorldNpcWalkerRouteService routeService,
		WorldNpcWalkerFormationService formationService)
	{
		_routeService = routeService;
		_formationService = formationService;
	}

	public WorldNpcWalkerFormationOrganizationResult Organize(
		IReadOnlyList<WorldNpc> npcs,
		WalkerTemplateTable walkerTemplates,
		WalkerVersionTable walkerVersions)
	{
		// Java parity: spawnengine/InstanceWalkerFormations.cacheWalkerCandidate groups ClusteredNpc instances by WalkerTemplate.routeId.
		var candidatesByRouteId = new Dictionary<string, List<WorldNpcWalkerFormationCandidate>>(StringComparer.Ordinal);
		var activeWalkers = new List<WorldNpcWalkerSpawnCandidate>();
		var activeFormations = new List<WorldNpcWalkerFormationResult>();
		var formationVariants = new Dictionary<string, List<WorldNpcWalkerFormationResult>>(StringComparer.Ordinal);
		var walkerVariants = new Dictionary<string, List<WorldNpcWalkerSpawnCandidate>>(StringComparer.Ordinal);
		var warnings = new List<WorldNpcWalkerOrganizationWarning>();

		foreach (var npc in npcs)
		{
			var routePlan = _routeService.ResolveRoute(npc, walkerTemplates, walkerVersions);
			if (routePlan.Status == WorldNpcWalkerRouteStatus.None)
				continue;
			if (routePlan.Status == WorldNpcWalkerRouteStatus.MissingRoute)
			{
				warnings.Add(WorldNpcWalkerOrganizationWarning.MissingRoute(routePlan.RouteId, npc.ObjectId));
				continue;
			}

			if (!candidatesByRouteId.TryGetValue(routePlan.RouteId, out var candidates))
			{
				candidates = [];
				candidatesByRouteId[routePlan.RouteId] = candidates;
			}

			candidates.Add(new WorldNpcWalkerFormationCandidate(npc, routePlan));
		}

		foreach (var candidates in candidatesByRouteId.Values)
			OrganizeRouteCandidates(candidates, activeWalkers, activeFormations, formationVariants, walkerVariants, warnings);

		return new WorldNpcWalkerFormationOrganizationResult(
			activeWalkers,
			activeFormations,
			ToReadOnlyDictionary(formationVariants),
			ToReadOnlyDictionary(walkerVariants),
			warnings);
	}

	private void OrganizeRouteCandidates(
		IReadOnlyList<WorldNpcWalkerFormationCandidate> candidates,
		List<WorldNpcWalkerSpawnCandidate> activeWalkers,
		List<WorldNpcWalkerFormationResult> activeFormations,
		Dictionary<string, List<WorldNpcWalkerFormationResult>> formationVariants,
		Dictionary<string, List<WorldNpcWalkerSpawnCandidate>> walkerVariants,
		List<WorldNpcWalkerOrganizationWarning> warnings)
	{
		var routePlan = candidates[0].RoutePlan;
		var alignedCandidates = SelectLargestSamePositionGroup(candidates);
		if (alignedCandidates.Count == 0)
		{
			warnings.Add(WorldNpcWalkerOrganizationWarning.MissingWalkers(routePlan.RouteId));
			return;
		}

		if (alignedCandidates.Count == 1)
		{
			if (candidates.Count != 1)
			{
				// Java parity: unaligned single-position candidates are spawned immediately, even for versioned routes.
				warnings.Add(WorldNpcWalkerOrganizationWarning.WalkersNotAligned(
					routePlan.RouteId,
					candidates.Select(candidate => candidate.Npc.ObjectId).ToArray()));
				activeWalkers.AddRange(candidates.Select(candidate => WorldNpcWalkerSpawnCandidate.FromNpc(candidate.Npc, candidate.RoutePlan)));
				return;
			}

			var singleCandidate = alignedCandidates[0];
			var singleWalker = WorldNpcWalkerSpawnCandidate.FromNpc(singleCandidate.Npc, singleCandidate.RoutePlan);
			if (string.IsNullOrEmpty(singleCandidate.RoutePlan.VersionRouteId))
				activeWalkers.Add(singleWalker);
			else
				AddVariant(walkerVariants, singleCandidate.RoutePlan.VersionRouteId, singleWalker);
			return;
		}

		if (routePlan.Pool != candidates.Count)
			warnings.Add(WorldNpcWalkerOrganizationWarning.IncorrectPool(
				routePlan.RouteId,
				routePlan.Pool,
				candidates.Count,
				candidates.Select(candidate => candidate.Npc.ObjectId).ToArray()));

		var formation = _formationService.FormSquareGroup(
			alignedCandidates.Select(candidate => candidate.Npc).ToArray(),
			routePlan);
		if (string.IsNullOrEmpty(formation.VersionRouteId))
		{
			activeFormations.Add(formation);
			var alignedObjectIds = alignedCandidates.Select(candidate => candidate.Npc.ObjectId).ToHashSet();
			activeWalkers.AddRange(candidates
				.Where(candidate => !alignedObjectIds.Contains(candidate.Npc.ObjectId))
				.Select(candidate => WorldNpcWalkerSpawnCandidate.FromNpc(candidate.Npc, candidate.RoutePlan)));
			return;
		}

		AddVariant(formationVariants, formation.VersionRouteId, formation);
	}

	private static IReadOnlyList<WorldNpcWalkerFormationCandidate> SelectLargestSamePositionGroup(
		IReadOnlyList<WorldNpcWalkerFormationCandidate> candidates)
	{
		return candidates
			.GroupBy(candidate => GetJavaPositionHash(candidate.Npc.Position.X, candidate.Npc.Position.Y))
			.OrderByDescending(group => group.Count())
			.FirstOrDefault()
			?.ToArray()
			?? Array.Empty<WorldNpcWalkerFormationCandidate>();
	}

	private static int GetJavaPositionHash(float x, float y)
	{
		// Java parity: spawnengine/ClusteredNpc.getPositionHash groups by Float.floatToIntBits(x/y), including the same overflow behavior.
		unchecked
		{
			var result = 1;
			result = 31 * result + BitConverter.SingleToInt32Bits(x);
			result = 31 * result + BitConverter.SingleToInt32Bits(y);
			return result;
		}
	}

	private static void AddVariant<T>(Dictionary<string, List<T>> variants, string versionRouteId, T value)
	{
		if (!variants.TryGetValue(versionRouteId, out var values))
		{
			values = [];
			variants[versionRouteId] = values;
		}

		values.Add(value);
	}

	private static IReadOnlyDictionary<string, IReadOnlyList<T>> ToReadOnlyDictionary<T>(
		Dictionary<string, List<T>> source)
	{
		return new ReadOnlyDictionary<string, IReadOnlyList<T>>(
			source.ToDictionary(
				entry => entry.Key,
				entry => (IReadOnlyList<T>)entry.Value.ToArray(),
				StringComparer.Ordinal));
	}

	private sealed record WorldNpcWalkerFormationCandidate(WorldNpc Npc, WorldNpcWalkerRoutePlan RoutePlan);
}

public sealed record WorldNpcWalkerFormationOrganizationResult(
	IReadOnlyList<WorldNpcWalkerSpawnCandidate> ActiveWalkers,
	IReadOnlyList<WorldNpcWalkerFormationResult> ActiveFormations,
	IReadOnlyDictionary<string, IReadOnlyList<WorldNpcWalkerFormationResult>> FormationVariants,
	IReadOnlyDictionary<string, IReadOnlyList<WorldNpcWalkerSpawnCandidate>> WalkerVariants,
	IReadOnlyList<WorldNpcWalkerOrganizationWarning> Warnings);

public sealed record WorldNpcWalkerSpawnCandidate(
	int ObjectId,
	int TemplateId,
	string RouteId,
	string VersionRouteId,
	float X,
	float Y,
	float Z,
	int WalkerIndex,
	byte Heading = 0)
{
	public static WorldNpcWalkerSpawnCandidate FromNpc(WorldNpc npc, WorldNpcWalkerRoutePlan routePlan)
	{
		return new WorldNpcWalkerSpawnCandidate(
			npc.ObjectId,
			npc.TemplateId,
			routePlan.RouteId,
			routePlan.VersionRouteId,
			npc.Position.X,
			npc.Position.Y,
			npc.Position.Z,
			npc.WalkerIndex,
			npc.Position.Heading);
	}
}

public sealed record WorldNpcWalkerOrganizationWarning(
	WorldNpcWalkerOrganizationWarningKind Kind,
	string RouteId,
	int ExpectedPool,
	int ActualPool,
	IReadOnlyList<int> ObjectIds)
{
	public static WorldNpcWalkerOrganizationWarning MissingRoute(string routeId, int objectId)
	{
		return new WorldNpcWalkerOrganizationWarning(
			WorldNpcWalkerOrganizationWarningKind.MissingRoute,
			routeId,
			0,
			1,
			[objectId]);
	}

	public static WorldNpcWalkerOrganizationWarning MissingWalkers(string routeId)
	{
		return new WorldNpcWalkerOrganizationWarning(
			WorldNpcWalkerOrganizationWarningKind.MissingWalkers,
			routeId,
			0,
			0,
			Array.Empty<int>());
	}

	public static WorldNpcWalkerOrganizationWarning WalkersNotAligned(string routeId, IReadOnlyList<int> objectIds)
	{
		return new WorldNpcWalkerOrganizationWarning(
			WorldNpcWalkerOrganizationWarningKind.WalkersNotAligned,
			routeId,
			objectIds.Count,
			objectIds.Count,
			objectIds);
	}

	public static WorldNpcWalkerOrganizationWarning IncorrectPool(
		string routeId,
		int expectedPool,
		int actualPool,
		IReadOnlyList<int> objectIds)
	{
		return new WorldNpcWalkerOrganizationWarning(
			WorldNpcWalkerOrganizationWarningKind.IncorrectPool,
			routeId,
			expectedPool,
			actualPool,
			objectIds);
	}
}

public enum WorldNpcWalkerOrganizationWarningKind
{
	MissingRoute,
	MissingWalkers,
	WalkersNotAligned,
	IncorrectPool,
}
