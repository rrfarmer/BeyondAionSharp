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

	public VortexStopInvasionCoordinatorService(
		VortexInvasionRuntime runtime,
		VortexStopInvasionSideEffectPlanService sideEffectPlanner)
	{
		_runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
		_sideEffectPlanner = sideEffectPlanner ?? throw new ArgumentNullException(nameof(sideEffectPlanner));
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
		var sideEffectPlan = _sideEffectPlanner.CreatePlan(
			stopResult,
			invaders,
			invaderKisks,
			spawnedNpcs,
			peaceSpawns);
		return VortexStopInvasionCoordinatorReport.From(stopResult, sideEffectPlan);
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
		VortexPeaceSpawnSnapshotSelectionService? peaceSpawnSelector = null)
	{
		ArgumentNullException.ThrowIfNull(snapshotRequest);
		ArgumentNullException.ThrowIfNull(vortexSpawns);

		var selector = peaceSpawnSelector ?? new VortexPeaceSpawnSnapshotSelectionService();
		var enrichedRequest = snapshotRequest.WithPeaceSpawns(selector.SelectPeaceSpawns(vortexLocationId, vortexSpawns));
		return StopInvasion(vortexLocationId, enrichedRequest);
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
