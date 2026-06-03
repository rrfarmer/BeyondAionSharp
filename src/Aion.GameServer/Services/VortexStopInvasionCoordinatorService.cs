namespace Aion.GameServer.Services;

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
