using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Services;

public sealed record VortexStartInvasionSnapshotRequest(
	IReadOnlyList<VortexStartSpawnedNpcSnapshot>? SpawnedNpcs = null,
	IReadOnlyList<VortexStartInvasionSpawnSnapshot>? InvasionSpawns = null,
	VortexDefenderAllianceUpdatePlan? DefenderAllianceUpdatePlan = null,
	VortexDefenderInvitationBatchPlan? DefenderInvitationBatchPlan = null)
{
	public static VortexStartInvasionSnapshotRequest Empty { get; } = new();

	public IReadOnlyList<VortexStartSpawnedNpcSnapshot> SpawnedNpcSnapshots => SpawnedNpcs ?? Array.Empty<VortexStartSpawnedNpcSnapshot>();
	public IReadOnlyList<VortexStartInvasionSpawnSnapshot> InvasionSpawnSnapshots => InvasionSpawns ?? Array.Empty<VortexStartInvasionSpawnSnapshot>();

	public bool HasAnySnapshot =>
		SpawnedNpcSnapshots.Count > 0 ||
		InvasionSpawnSnapshots.Count > 0 ||
		DefenderAllianceUpdatePlan is not null ||
		DefenderInvitationBatchPlan is not null;

	public VortexStartInvasionSnapshotRequest WithInvasionSpawns(
		IReadOnlyList<VortexStartInvasionSpawnSnapshot>? invasionSpawns)
	{
		var selectedInvasionSpawns = invasionSpawns ?? Array.Empty<VortexStartInvasionSpawnSnapshot>();
		if (selectedInvasionSpawns.Count == 0)
		{
			return this;
		}

		return new VortexStartInvasionSnapshotRequest(
			SpawnedNpcSnapshots,
			InvasionSpawnSnapshots.Concat(selectedInvasionSpawns).ToArray(),
			DefenderAllianceUpdatePlan,
			DefenderInvitationBatchPlan);
	}
}

public sealed class VortexStartInvasionCoordinatorService
{
	private readonly VortexInvasionRuntime _runtime;
	private readonly VortexStartInvasionSideEffectPlanService _sideEffectPlanner;
	private readonly IVortexInvasionSpawnSnapshotSelector _invasionSpawnSelector;

	public VortexStartInvasionCoordinatorService(
		VortexInvasionRuntime runtime,
		VortexStartInvasionSideEffectPlanService sideEffectPlanner,
		IVortexInvasionSpawnSnapshotSelector? invasionSpawnSelector = null)
	{
		_runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
		_sideEffectPlanner = sideEffectPlanner ?? throw new ArgumentNullException(nameof(sideEffectPlanner));
		_invasionSpawnSelector = invasionSpawnSelector ?? new VortexInvasionSpawnSnapshotSelectionService();
	}

	public VortexStartInvasionCoordinatorReport StartInvasion(
		VortexLocationSummary location,
		RiftPortalState? activePortal = null,
		IReadOnlyList<VortexStartSpawnedNpcSnapshot>? spawnedNpcs = null,
		IReadOnlyList<VortexStartInvasionSpawnSnapshot>? invasionSpawns = null,
		VortexDefenderAllianceUpdatePlan? defenderAllianceUpdatePlan = null,
		VortexDefenderInvitationBatchPlan? defenderInvitationBatchPlan = null)
	{
		ArgumentNullException.ThrowIfNull(location);

		// Java parity: services/VortexService.startInvasion stores a new active
		// invasion only after its service-level active map guard passes, then
		// invokes DimensionalVortex.start and Invasion.startInvasion.
		var startResult = _runtime.StartInvasionWithResult(location, activePortal);
		return CreateReport(
			startResult,
			spawnedNpcs,
			invasionSpawns,
			defenderAllianceUpdatePlan,
			defenderInvitationBatchPlan);
	}

	public VortexStartInvasionCoordinatorReport StartInvasion(
		VortexLocationSummary location,
		VortexStartInvasionSnapshotRequest snapshotRequest)
	{
		ArgumentNullException.ThrowIfNull(snapshotRequest);

		return StartInvasion(
			location,
			activePortal: null,
			snapshotRequest.SpawnedNpcSnapshots,
			snapshotRequest.InvasionSpawnSnapshots,
			snapshotRequest.DefenderAllianceUpdatePlan,
			snapshotRequest.DefenderInvitationBatchPlan);
	}

	public VortexStartInvasionCoordinatorReport StartInvasion(
		VortexLocationSummary location,
		RiftPortalState? activePortal,
		VortexStartInvasionSnapshotRequest snapshotRequest,
		NpcVortexSpawnTable vortexSpawns,
		IVortexInvasionSpawnSnapshotSelector? invasionSpawnSelector = null)
	{
		ArgumentNullException.ThrowIfNull(location);
		ArgumentNullException.ThrowIfNull(snapshotRequest);
		ArgumentNullException.ThrowIfNull(vortexSpawns);

		var startResult = _runtime.StartInvasionWithResult(location, activePortal);
		var enrichedRequest = snapshotRequest;
		if (startResult.Started)
		{
			// Java parity: VortexService.startInvasion returns before constructing
			// or starting an invasion when the active map guard fails, so static
			// INVASION spawn enrichment is only relevant after the start guard succeeds.
			var selector = invasionSpawnSelector ?? _invasionSpawnSelector;
			enrichedRequest = snapshotRequest.WithInvasionSpawns(selector.SelectInvasionSpawns(location.Id, vortexSpawns));
		}

		return CreateReport(
			startResult,
			enrichedRequest.SpawnedNpcSnapshots,
			enrichedRequest.InvasionSpawnSnapshots,
			enrichedRequest.DefenderAllianceUpdatePlan,
			enrichedRequest.DefenderInvitationBatchPlan);
	}

	public VortexStartInvasionCoordinatorReport StartInvasion(
		VortexLocationSummary location,
		NpcVortexSpawnTable vortexSpawns,
		IVortexInvasionSpawnSnapshotSelector? invasionSpawnSelector = null)
	{
		return StartInvasion(
			location,
			activePortal: null,
			VortexStartInvasionSnapshotRequest.Empty,
			vortexSpawns,
			invasionSpawnSelector);
	}

	private VortexStartInvasionCoordinatorReport CreateReport(
		VortexStartInvasionResult startResult,
		IReadOnlyList<VortexStartSpawnedNpcSnapshot>? spawnedNpcs = null,
		IReadOnlyList<VortexStartInvasionSpawnSnapshot>? invasionSpawns = null,
		VortexDefenderAllianceUpdatePlan? defenderAllianceUpdatePlan = null,
		VortexDefenderInvitationBatchPlan? defenderInvitationBatchPlan = null)
	{
		var sideEffectPlan = _sideEffectPlanner.CreatePlan(
			startResult,
			spawnedNpcs,
			invasionSpawns,
			defenderAllianceUpdatePlan,
			defenderInvitationBatchPlan);
		return VortexStartInvasionCoordinatorReport.From(startResult, sideEffectPlan);
	}
}

public enum VortexStartInvasionCoordinatorStatus
{
	AlreadyStarted,
	Planned,
}

public sealed record VortexStartInvasionCoordinatorReport(
	VortexStartInvasionCoordinatorStatus Status,
	int LocationId,
	VortexStartInvasionResult StartResult,
	VortexStartInvasionSideEffectPlan SideEffectPlan,
	string JavaSource)
{
	public bool Started => StartResult.Started;
	public bool HasSideEffectPlan => SideEffectPlan.Status == VortexStartInvasionSideEffectPlanStatus.Planned;
	public bool ShouldExecuteLiveSideEffects => false;

	public static VortexStartInvasionCoordinatorReport From(
		VortexStartInvasionResult startResult,
		VortexStartInvasionSideEffectPlan sideEffectPlan)
	{
		ArgumentNullException.ThrowIfNull(startResult);
		ArgumentNullException.ThrowIfNull(sideEffectPlan);

		var status = sideEffectPlan.Status == VortexStartInvasionSideEffectPlanStatus.Planned
			? VortexStartInvasionCoordinatorStatus.Planned
			: VortexStartInvasionCoordinatorStatus.AlreadyStarted;

		return new VortexStartInvasionCoordinatorReport(
			status,
			startResult.LocationId,
			startResult,
			sideEffectPlan,
			status == VortexStartInvasionCoordinatorStatus.Planned
				? "services/VortexService.startInvasion -> services/vortex/DimensionalVortex.start -> services/vortex/Invasion.startInvasion"
				: "services/VortexService.startInvasion");
	}
}
