using System.Collections.Concurrent;
using Aion.GameServer.Model.GameObjects;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Services;

public enum CmBuyItemKnownVisibleObjectKind
{
	Player,
	Npc,
	Pet,
	Other,
}

public enum CmBuyItemKnownVisibleObjectMembershipUpdateReason
{
	Manual,
	KnownListRefresh,
	Removed,
	Cleared,
}

public sealed record CmBuyItemKnownVisibleObjectMembershipCandidate(
	int ObjectId,
	CmBuyItemKnownVisibleObjectKind Kind,
	bool IsVisibleToOwner,
	string? JavaSource = null);

public sealed record CmBuyItemKnownVisibleObjectMembershipEntry(
	int OwnerPlayerObjectId,
	int KnownObjectId,
	CmBuyItemKnownVisibleObjectKind Kind,
	bool IsVisibleToOwner,
	CmBuyItemKnownVisibleObjectMembershipUpdateReason UpdateReason,
	string JavaSource,
	bool IsLive);

public sealed record CmBuyItemKnownVisibleObjectMembershipSnapshot(
	int OwnerPlayerObjectId,
	IReadOnlyList<CmBuyItemKnownVisibleObjectMembershipEntry> Entries,
	bool ExcludesOwnerByNormalAddPath,
	bool DeduplicatesByObjectId,
	string JavaSource,
	bool IsLive)
{
	public IReadOnlyList<int> KnownObjectIds { get; } = Entries.Select(entry => entry.KnownObjectId).ToArray();
}

public sealed class CmBuyItemKnownVisibleObjectMembershipService
{
	private const string DefaultJavaSource =
		"com.aionemu.gameserver.world.knownlist.KnownList.knownObjects / KnownList.getObject";

	private readonly ConcurrentDictionary<int, Dictionary<int, CmBuyItemKnownVisibleObjectMembershipEntry>> _knownObjectsByOwner = new();

	public CmBuyItemKnownVisibleObjectMembershipSnapshot UpsertKnownObjects(
		int ownerPlayerObjectId,
		IEnumerable<CmBuyItemKnownVisibleObjectMembershipCandidate>? candidates,
		CmBuyItemKnownVisibleObjectMembershipUpdateReason updateReason = CmBuyItemKnownVisibleObjectMembershipUpdateReason.Manual)
	{
		var entries = _knownObjectsByOwner.GetOrAdd(ownerPlayerObjectId, _ => new Dictionary<int, CmBuyItemKnownVisibleObjectMembershipEntry>());

		lock (entries)
		{
			foreach (var candidate in candidates ?? Array.Empty<CmBuyItemKnownVisibleObjectMembershipCandidate>())
			{
				// Java parity: KnownList.isAwareOf rejects the owner through the normal add path.
				if (candidate.ObjectId == ownerPlayerObjectId)
					continue;

				entries[candidate.ObjectId] = new CmBuyItemKnownVisibleObjectMembershipEntry(
					ownerPlayerObjectId,
					candidate.ObjectId,
					candidate.Kind,
					candidate.IsVisibleToOwner,
					updateReason,
					candidate.JavaSource ?? DefaultJavaSource,
					IsLive: false);
			}

			return CreateSnapshot(ownerPlayerObjectId, entries.Values);
		}
	}

	public bool RemoveKnownObject(
		int ownerPlayerObjectId,
		int knownObjectId,
		out CmBuyItemKnownVisibleObjectMembershipSnapshot snapshot)
	{
		if (!_knownObjectsByOwner.TryGetValue(ownerPlayerObjectId, out var entries))
		{
			snapshot = CreateSnapshot(ownerPlayerObjectId, Array.Empty<CmBuyItemKnownVisibleObjectMembershipEntry>());
			return false;
		}

		lock (entries)
		{
			var removed = entries.Remove(knownObjectId);
			if (entries.Count == 0)
				_knownObjectsByOwner.TryRemove(ownerPlayerObjectId, out _);

			snapshot = CreateSnapshot(ownerPlayerObjectId, entries.Values);
			return removed;
		}
	}

	public CmBuyItemKnownVisibleObjectMembershipSnapshot ClearKnownObjects(int ownerPlayerObjectId)
	{
		_knownObjectsByOwner.TryRemove(ownerPlayerObjectId, out _);
		return CreateSnapshot(ownerPlayerObjectId, Array.Empty<CmBuyItemKnownVisibleObjectMembershipEntry>());
	}

	public CmBuyItemKnownVisibleObjectMembershipSnapshot GetSnapshot(int ownerPlayerObjectId)
	{
		if (!_knownObjectsByOwner.TryGetValue(ownerPlayerObjectId, out var entries))
			return CreateSnapshot(ownerPlayerObjectId, Array.Empty<CmBuyItemKnownVisibleObjectMembershipEntry>());

		lock (entries)
		{
			return CreateSnapshot(ownerPlayerObjectId, entries.Values);
		}
	}

	private static CmBuyItemKnownVisibleObjectMembershipSnapshot CreateSnapshot(
		int ownerPlayerObjectId,
		IEnumerable<CmBuyItemKnownVisibleObjectMembershipEntry> entries) =>
		new(
			ownerPlayerObjectId,
			entries.ToArray(),
			ExcludesOwnerByNormalAddPath: true,
			DeduplicatesByObjectId: true,
			DefaultJavaSource,
			IsLive: false);
}

public sealed record CmBuyItemKnownVisibleObjectPopulationResult(
	int OwnerPlayerObjectId,
	CmBuyItemKnownVisibleObjectMembershipSnapshot Snapshot,
	int PlayerCandidateCount,
	int NpcCandidateCount,
	int UpsertedVisibleObjectCount,
	int RemovedStaleObjectCount,
	bool UsesWorldVisibilityApproximation,
	bool IsJavaRegionKnownListParity,
	string JavaSource,
	bool IsLive);

public sealed class CmBuyItemKnownVisibleObjectPopulationAdapterService
{
	private readonly CmBuyItemKnownVisibleObjectMembershipService _membershipService;

	public CmBuyItemKnownVisibleObjectPopulationAdapterService(CmBuyItemKnownVisibleObjectMembershipService membershipService)
	{
		_membershipService = membershipService;
	}

	public CmBuyItemKnownVisibleObjectPopulationResult RefreshOwnerFromSuppliedFacts(
		Player owner,
		IEnumerable<Player>? onlinePlayers,
		IEnumerable<IWorldNpcObject>? npcs)
	{
		// Java parity breadcrumb: KnownList.findVisibleObjects scans the owner's
		// world-map region neighbours, handles two-way add, and updates visibility.
		// This disabled adapter only converts supplied player/NPC facts through the
		// current WorldVisibility approximation for CM_BUY_ITEM target membership.
		var playerCandidates = (onlinePlayers ?? Array.Empty<Player>()).ToArray();
		var npcCandidates = (npcs ?? Array.Empty<IWorldNpcObject>()).ToArray();
		var visibleCandidates = playerCandidates
			.Where(player => player.ObjectId != owner.ObjectId)
			.Where(player => WorldVisibility.IsVisibleTo(player, owner.GetPosition()))
			.Select(player => new CmBuyItemKnownVisibleObjectMembershipCandidate(
				player.ObjectId,
				CmBuyItemKnownVisibleObjectKind.Player,
				IsVisibleToOwner: true,
				"KnownList.findVisibleObjects approximated with supplied online players + WorldVisibility"))
			.Concat(npcCandidates
				.Where(npc => WorldVisibility.IsVisibleTo(owner, npc.Position))
				.Select(npc => new CmBuyItemKnownVisibleObjectMembershipCandidate(
					npc.ObjectId,
					CmBuyItemKnownVisibleObjectKind.Npc,
					IsVisibleToOwner: true,
					"KnownList.findVisibleObjects approximated with supplied NPCs + WorldVisibility")))
			.GroupBy(candidate => candidate.ObjectId)
			.Select(group => group.First())
			.ToArray();
		var visibleObjectIds = visibleCandidates.Select(candidate => candidate.ObjectId).ToHashSet();
		var removed = 0;

		foreach (var existingKnownObjectId in _membershipService.GetSnapshot(owner.ObjectId).KnownObjectIds)
		{
			if (visibleObjectIds.Contains(existingKnownObjectId))
				continue;

			if (_membershipService.RemoveKnownObject(owner.ObjectId, existingKnownObjectId, out _))
				removed++;
		}

		var snapshot = _membershipService.UpsertKnownObjects(
			owner.ObjectId,
			visibleCandidates,
			CmBuyItemKnownVisibleObjectMembershipUpdateReason.KnownListRefresh);

		return new CmBuyItemKnownVisibleObjectPopulationResult(
			owner.ObjectId,
			snapshot,
			playerCandidates.Length,
			npcCandidates.Length,
			visibleCandidates.Length,
			removed,
			UsesWorldVisibilityApproximation: true,
			IsJavaRegionKnownListParity: false,
			"KnownList.update -> forgetObjectsOrUpdateVisibility/findVisibleObjects approximated from supplied players/NPCs and WorldVisibility",
			IsLive: false);
	}
}

public enum CmBuyItemKnownVisibleObjectResolverAdapterStatus
{
	MissingPlayer,
	MissingMembershipService,
	KnownObjectTarget,
	UnknownObjectTarget,
}

public sealed record CmBuyItemKnownVisibleObjectPopulationResolverAdapterPlan(
	CmBuyItemKnownVisibleObjectPopulationResult? PopulationResult,
	CmBuyItemKnownVisibleObjectResolverAdapterPlan ResolverPlan,
	bool RefreshesSuppliedFactsBeforeResolve,
	bool IsJavaRegionKnownListParity,
	string JavaSource,
	bool IsLive);

public sealed record CmBuyItemWorldKnownVisibleObjectSnapshot(
	int OwnerPlayerObjectId,
	IReadOnlyList<Player> PlayerCandidates,
	IReadOnlyList<IWorldNpcObject> NpcCandidates,
	int WorldObjectCount,
	bool UsesWorldContainerSnapshot,
	bool IsJavaRegionKnownListParity,
	string JavaSource,
	bool IsLive);

public sealed record CmBuyItemWorldKnownVisibleObjectResolverFactoryPlan(
	CmBuyItemWorldKnownVisibleObjectSnapshot? WorldSnapshot,
	CmBuyItemKnownVisibleObjectPopulationResolverAdapterPlan PopulationResolverPlan,
	bool UsesWorldSnapshotCollector,
	bool IsDefaultConnectionWiring,
	bool IsJavaRegionKnownListParity,
	string JavaSource,
	bool IsLive);

public sealed class CmBuyItemWorldKnownVisibleObjectSnapshotCollectorService
{
	public CmBuyItemWorldKnownVisibleObjectSnapshot Collect(Player owner, GameWorld world)
	{
		// Java parity breadcrumb: KnownList.findVisibleObjects scans the owner's
		// current map-region neighbours. This disabled collector only snapshots
		// same-world objects already stored in the C# World container.
		var playerCandidates = world.GetPlayers(owner.GetPosition().WorldId);
		var npcCandidates = world.GetNpcs(owner.GetPosition().WorldId);

		return new CmBuyItemWorldKnownVisibleObjectSnapshot(
			owner.ObjectId,
			playerCandidates,
			npcCandidates,
			world.ObjectCount,
			UsesWorldContainerSnapshot: true,
			IsJavaRegionKnownListParity: false,
			"KnownList.findVisibleObjects approximated from C# World same-world player/NPC snapshots",
			IsLive: false);
	}
}

public sealed class CmBuyItemWorldKnownVisibleObjectResolverFactoryService
{
	private readonly CmBuyItemKnownVisibleObjectPopulationResolverAdapterService _populationResolver;
	private readonly CmBuyItemWorldKnownVisibleObjectSnapshotCollectorService _snapshotCollector;

	public CmBuyItemWorldKnownVisibleObjectResolverFactoryService(
		CmBuyItemKnownVisibleObjectPopulationResolverAdapterService populationResolver,
		CmBuyItemWorldKnownVisibleObjectSnapshotCollectorService snapshotCollector)
	{
		_populationResolver = populationResolver;
		_snapshotCollector = snapshotCollector;
	}

	public Func<Player, int, object?, bool?> CreateResolver(GameWorld world) =>
		_populationResolver.CreateResolver(
			player => _snapshotCollector.Collect(player, world).PlayerCandidates,
			player => _snapshotCollector.Collect(player, world).NpcCandidates);

	public CmBuyItemWorldKnownVisibleObjectResolverFactoryPlan CreatePlan(Player? player, int sellerObjectId, GameWorld world)
	{
		if (player == null)
			return CreatePlan(
				worldSnapshot: null,
				_populationResolver.CreatePlan(player, sellerObjectId, _ => Array.Empty<Player>(), _ => Array.Empty<IWorldNpcObject>()),
				"CM_BUY_ITEM world snapshot resolver factory cannot collect without active player");

		var worldSnapshot = _snapshotCollector.Collect(player, world);
		var populationResolverPlan = _populationResolver.CreatePlan(
			player,
			sellerObjectId,
			_ => worldSnapshot.PlayerCandidates,
			_ => worldSnapshot.NpcCandidates);

		return CreatePlan(
			worldSnapshot,
			populationResolverPlan,
			"CM_BUY_ITEM opt-in diagnostic resolver factory feeds World snapshots into supplied-facts resolver");
	}

	private static CmBuyItemWorldKnownVisibleObjectResolverFactoryPlan CreatePlan(
		CmBuyItemWorldKnownVisibleObjectSnapshot? worldSnapshot,
		CmBuyItemKnownVisibleObjectPopulationResolverAdapterPlan populationResolverPlan,
		string javaSource) =>
		new(
			worldSnapshot,
			populationResolverPlan,
			UsesWorldSnapshotCollector: worldSnapshot != null,
			IsDefaultConnectionWiring: false,
			IsJavaRegionKnownListParity: false,
			javaSource,
			IsLive: false);
}

public sealed class CmBuyItemKnownVisibleObjectPopulationResolverAdapterService
{
	private readonly CmBuyItemKnownVisibleObjectMembershipService _membershipService;
	private readonly CmBuyItemKnownVisibleObjectPopulationAdapterService _populationAdapter;

	public CmBuyItemKnownVisibleObjectPopulationResolverAdapterService(
		CmBuyItemKnownVisibleObjectMembershipService membershipService,
		CmBuyItemKnownVisibleObjectPopulationAdapterService populationAdapter)
	{
		_membershipService = membershipService;
		_populationAdapter = populationAdapter;
	}

	public Func<Player, int, object?, bool?> CreateResolver(
		Func<Player, IEnumerable<Player>?> onlinePlayersProvider,
		Func<Player, IEnumerable<IWorldNpcObject>?> npcProvider) =>
		(player, sellerObjectId, _) => CreatePlan(player, sellerObjectId, onlinePlayersProvider, npcProvider).ResolverPlan.IsKnownByPlayer;

	public CmBuyItemKnownVisibleObjectPopulationResolverAdapterPlan CreatePlan(
		Player? player,
		int sellerObjectId,
		Func<Player, IEnumerable<Player>?> onlinePlayersProvider,
		Func<Player, IEnumerable<IWorldNpcObject>?> npcProvider)
	{
		if (player == null)
			return CreatePlan(
				populationResult: null,
				CmBuyItemKnownVisibleObjectResolverAdapterService.CreatePlan(player, sellerObjectId, _membershipService),
				"CM_BUY_ITEM population resolver adapter cannot refresh supplied facts without active player");

		var populationResult = _populationAdapter.RefreshOwnerFromSuppliedFacts(
			player,
			onlinePlayersProvider(player),
			npcProvider(player));
		var resolverPlan = CmBuyItemKnownVisibleObjectResolverAdapterService.CreatePlan(player, sellerObjectId, _membershipService);

		return CreatePlan(
			populationResult,
			resolverPlan,
			"KnownList.update/findVisibleObjects supplied-facts approximation refreshed before KnownList.getObject resolver plan");
	}

	private static CmBuyItemKnownVisibleObjectPopulationResolverAdapterPlan CreatePlan(
		CmBuyItemKnownVisibleObjectPopulationResult? populationResult,
		CmBuyItemKnownVisibleObjectResolverAdapterPlan resolverPlan,
		string javaSource) =>
		new(
			populationResult,
			resolverPlan,
			RefreshesSuppliedFactsBeforeResolve: populationResult != null,
			IsJavaRegionKnownListParity: false,
			javaSource,
			IsLive: false);
}

public sealed record CmBuyItemKnownVisibleObjectResolverAdapterPlan(
	CmBuyItemKnownVisibleObjectResolverAdapterStatus Status,
	int SellerObjectId,
	bool? IsKnownByPlayer,
	CmBuyItemKnownVisibleObjectKind? SnapshotObjectKind,
	int SnapshotEntryCount,
	bool UsesKnownVisibleObjectSnapshot,
	bool IsJavaKnownListParity,
	string JavaSource,
	bool IsLive);

public static class CmBuyItemKnownVisibleObjectResolverAdapterService
{
	public static Func<Player, int, object?, bool?> CreateResolver(CmBuyItemKnownVisibleObjectMembershipService membershipService) =>
		(player, sellerObjectId, _) => CreatePlan(player, sellerObjectId, membershipService).IsKnownByPlayer;

	public static CmBuyItemKnownVisibleObjectResolverAdapterPlan CreatePlan(
		Player? player,
		int sellerObjectId,
		CmBuyItemKnownVisibleObjectMembershipService? membershipService)
	{
		if (player == null)
			return CreatePlan(
				CmBuyItemKnownVisibleObjectResolverAdapterStatus.MissingPlayer,
				sellerObjectId,
				isKnownByPlayer: null,
				snapshotObjectKind: null,
				snapshotEntryCount: 0,
				usesKnownVisibleObjectSnapshot: false,
				"CM_BUY_ITEM known-visible-object resolver adapter cannot read membership without active player");

		if (membershipService == null)
			return CreatePlan(
				CmBuyItemKnownVisibleObjectResolverAdapterStatus.MissingMembershipService,
				sellerObjectId,
				isKnownByPlayer: null,
				snapshotObjectKind: null,
				snapshotEntryCount: 0,
				usesKnownVisibleObjectSnapshot: false,
				"CM_BUY_ITEM known-visible-object resolver adapter has no membership service");

		var snapshot = membershipService.GetSnapshot(player.ObjectId);
		var entry = snapshot.Entries.FirstOrDefault(entry => entry.KnownObjectId == sellerObjectId);
		var isKnown = entry != null;

		return CreatePlan(
			isKnown
				? CmBuyItemKnownVisibleObjectResolverAdapterStatus.KnownObjectTarget
				: CmBuyItemKnownVisibleObjectResolverAdapterStatus.UnknownObjectTarget,
			sellerObjectId,
			isKnown,
			entry?.Kind,
			snapshot.Entries.Count,
			usesKnownVisibleObjectSnapshot: true,
			"KnownList.getObject membership approximated from supplied generic known-visible-object snapshot");
	}

	private static CmBuyItemKnownVisibleObjectResolverAdapterPlan CreatePlan(
		CmBuyItemKnownVisibleObjectResolverAdapterStatus status,
		int sellerObjectId,
		bool? isKnownByPlayer,
		CmBuyItemKnownVisibleObjectKind? snapshotObjectKind,
		int snapshotEntryCount,
		bool usesKnownVisibleObjectSnapshot,
		string javaSource)
	{
		return new CmBuyItemKnownVisibleObjectResolverAdapterPlan(
			status,
			sellerObjectId,
			isKnownByPlayer,
			snapshotObjectKind,
			snapshotEntryCount,
			usesKnownVisibleObjectSnapshot,
			IsJavaKnownListParity: false,
			javaSource,
			IsLive: false);
	}
}
