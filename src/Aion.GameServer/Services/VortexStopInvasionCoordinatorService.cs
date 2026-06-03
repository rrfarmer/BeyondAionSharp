using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Services;

public sealed record VortexStopInvasionSnapshotRequest(
	IReadOnlyList<VortexStopInvaderSnapshot>? Invaders = null,
	IReadOnlyList<VortexStopInvaderKiskSnapshot>? InvaderKisks = null,
	IReadOnlyList<VortexStopSpawnedNpcSnapshot>? SpawnedNpcs = null,
	IReadOnlyList<VortexStopPeaceSpawnSnapshot>? PeaceSpawns = null)
{
	public static VortexStopInvasionSnapshotRequest Empty { get; } = new();

	public IReadOnlyList<VortexStopInvaderSnapshot> InvaderSnapshots => Invaders ?? Array.Empty<VortexStopInvaderSnapshot>();
	public IReadOnlyList<VortexStopInvaderKiskSnapshot> InvaderKiskSnapshots => InvaderKisks ?? Array.Empty<VortexStopInvaderKiskSnapshot>();
	public IReadOnlyList<VortexStopSpawnedNpcSnapshot> SpawnedNpcSnapshots => SpawnedNpcs ?? Array.Empty<VortexStopSpawnedNpcSnapshot>();
	public IReadOnlyList<VortexStopPeaceSpawnSnapshot> PeaceSpawnSnapshots => PeaceSpawns ?? Array.Empty<VortexStopPeaceSpawnSnapshot>();

	public bool HasAnySnapshot =>
		InvaderSnapshots.Count > 0 ||
		InvaderKiskSnapshots.Count > 0 ||
		SpawnedNpcSnapshots.Count > 0 ||
		PeaceSpawnSnapshots.Count > 0;

	public VortexStopInvasionSnapshotRequest WithPeaceSpawns(
		IReadOnlyList<VortexStopPeaceSpawnSnapshot>? peaceSpawns)
	{
		var selectedPeaceSpawns = peaceSpawns ?? Array.Empty<VortexStopPeaceSpawnSnapshot>();
		if (selectedPeaceSpawns.Count == 0)
		{
			return this;
		}

		return new VortexStopInvasionSnapshotRequest(
			InvaderSnapshots,
			InvaderKiskSnapshots,
			SpawnedNpcSnapshots,
			PeaceSpawnSnapshots.Concat(selectedPeaceSpawns).ToArray());
	}
}

public sealed class VortexStopInvasionCoordinatorService
{
	private readonly VortexInvasionRuntime _runtime;
	private readonly VortexStopInvasionSideEffectPlanService _sideEffectPlanner;
	private readonly IVortexPeaceSpawnSnapshotSelector _peaceSpawnSelector;

	public VortexStopInvasionCoordinatorService(
		VortexInvasionRuntime runtime,
		VortexStopInvasionSideEffectPlanService sideEffectPlanner,
		IVortexPeaceSpawnSnapshotSelector? peaceSpawnSelector = null)
	{
		_runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
		_sideEffectPlanner = sideEffectPlanner ?? throw new ArgumentNullException(nameof(sideEffectPlanner));
		_peaceSpawnSelector = peaceSpawnSelector ?? new VortexPeaceSpawnSnapshotSelectionService();
	}

	public VortexStopInvasionCoordinatorReport StopInvasion(
		int vortexLocationId,
		IReadOnlyList<VortexStopInvaderSnapshot>? invaders = null,
		IReadOnlyList<VortexStopInvaderKiskSnapshot>? invaderKisks = null,
		IReadOnlyList<VortexStopSpawnedNpcSnapshot>? spawnedNpcs = null,
		IReadOnlyList<VortexStopPeaceSpawnSnapshot>? peaceSpawns = null)
	{
		// Java parity: services/VortexService.stopInvasion removes the active invasion
		// from its service map, then invokes DimensionalVortex.stop, whose Invasion
		// implementation clears active metadata and performs live side effects.
		var stopResult = _runtime.StopInvasion(vortexLocationId);
		return CreateReport(
			stopResult,
			invaders,
			invaderKisks,
			spawnedNpcs,
			peaceSpawns);
	}

	public VortexStopInvasionCoordinatorReport StopInvasion(
		int vortexLocationId,
		VortexStopInvasionSnapshotRequest snapshotRequest)
	{
		ArgumentNullException.ThrowIfNull(snapshotRequest);

		return StopInvasion(
			vortexLocationId,
			snapshotRequest.InvaderSnapshots,
			snapshotRequest.InvaderKiskSnapshots,
			snapshotRequest.SpawnedNpcSnapshots,
			snapshotRequest.PeaceSpawnSnapshots);
	}

	public VortexStopInvasionCoordinatorReport StopInvasion(
		int vortexLocationId,
		VortexStopInvasionSnapshotRequest snapshotRequest,
		NpcVortexSpawnTable vortexSpawns,
		IVortexPeaceSpawnSnapshotSelector? peaceSpawnSelector = null)
	{
		ArgumentNullException.ThrowIfNull(snapshotRequest);
		ArgumentNullException.ThrowIfNull(vortexSpawns);

		var stopResult = _runtime.StopInvasion(vortexLocationId);
		var enrichedRequest = snapshotRequest;
		if (stopResult.Stopped)
		{
			// Java parity: VortexService.stopInvasion returns before invoking
			// Invasion.stopInvasion when no active invasion exists, so static PEACE
			// spawn enrichment is only relevant after the stop guard succeeds.
			var selector = peaceSpawnSelector ?? _peaceSpawnSelector;
			enrichedRequest = snapshotRequest.WithPeaceSpawns(selector.SelectPeaceSpawns(vortexLocationId, vortexSpawns));
		}

		return CreateReport(
			stopResult,
			enrichedRequest.InvaderSnapshots,
			enrichedRequest.InvaderKiskSnapshots,
			enrichedRequest.SpawnedNpcSnapshots,
			enrichedRequest.PeaceSpawnSnapshots);
	}

	public VortexStopInvasionCoordinatorReport StopInvasion(
		int vortexLocationId,
		NpcVortexSpawnTable vortexSpawns,
		IVortexPeaceSpawnSnapshotSelector? peaceSpawnSelector = null)
	{
		return StopInvasion(
			vortexLocationId,
			VortexStopInvasionSnapshotRequest.Empty,
			vortexSpawns,
			peaceSpawnSelector);
	}

	private VortexStopInvasionCoordinatorReport CreateReport(
		VortexStopInvasionResult stopResult,
		IReadOnlyList<VortexStopInvaderSnapshot>? invaders = null,
		IReadOnlyList<VortexStopInvaderKiskSnapshot>? invaderKisks = null,
		IReadOnlyList<VortexStopSpawnedNpcSnapshot>? spawnedNpcs = null,
		IReadOnlyList<VortexStopPeaceSpawnSnapshot>? peaceSpawns = null)
	{
		var sideEffectPlan = _sideEffectPlanner.CreatePlan(
			stopResult,
			invaders,
			invaderKisks,
			spawnedNpcs,
			peaceSpawns);
		return VortexStopInvasionCoordinatorReport.From(stopResult, sideEffectPlan);
	}
}

public enum VortexStopInvasionCoordinatorStatus
{
	MissingInvasion,
	MissingStopSnapshot,
	Planned,
}

public sealed record VortexStopInvasionCoordinatorReport(
	VortexStopInvasionCoordinatorStatus Status,
	int LocationId,
	VortexStopInvasionResult StopResult,
	VortexStopInvasionSideEffectPlan SideEffectPlan,
	string JavaSource)
{
	public bool Stopped => StopResult.Stopped;
	public bool HasSideEffectPlan => SideEffectPlan.Status == VortexStopInvasionSideEffectPlanStatus.Planned;
	public bool ShouldExecuteLiveSideEffects => false;

	public static VortexStopInvasionCoordinatorReport From(
		VortexStopInvasionResult stopResult,
		VortexStopInvasionSideEffectPlan sideEffectPlan)
	{
		ArgumentNullException.ThrowIfNull(stopResult);
		ArgumentNullException.ThrowIfNull(sideEffectPlan);

		var status = sideEffectPlan.Status switch
		{
			VortexStopInvasionSideEffectPlanStatus.Planned => VortexStopInvasionCoordinatorStatus.Planned,
			VortexStopInvasionSideEffectPlanStatus.MissingStopSnapshot => VortexStopInvasionCoordinatorStatus.MissingStopSnapshot,
			_ => VortexStopInvasionCoordinatorStatus.MissingInvasion,
		};

		return new VortexStopInvasionCoordinatorReport(
			status,
			stopResult.LocationId,
			stopResult,
			sideEffectPlan,
			status == VortexStopInvasionCoordinatorStatus.Planned
				? "services/VortexService.stopInvasion -> services/vortex/DimensionalVortex.stop -> services/vortex/Invasion.stopInvasion"
				: "services/VortexService.stopInvasion");
	}
}
