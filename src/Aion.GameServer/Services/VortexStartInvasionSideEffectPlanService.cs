using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.World;

namespace Aion.GameServer.Services;

public sealed class VortexStartInvasionSideEffectPlanService
{
	public VortexStartInvasionSideEffectPlan CreatePlan(
		VortexStartInvasionResult startResult,
		IReadOnlyList<VortexStartSpawnedNpcSnapshot>? spawnedNpcs = null,
		IReadOnlyList<VortexStartInvasionSpawnSnapshot>? invasionSpawns = null)
	{
		ArgumentNullException.ThrowIfNull(startResult);

		if (!startResult.Started)
		{
			return new VortexStartInvasionSideEffectPlan(
				VortexStartInvasionSideEffectPlanStatus.AlreadyStarted,
				startResult.LocationId,
				StartResult: startResult,
				JavaSource: startResult.JavaSource);
		}

		var existingSpawnedNpcs = spawnedNpcs ?? [];
		var selectedInvasionSpawns = invasionSpawns ?? [];
		var steps = new List<VortexStartInvasionSideEffectStep>
		{
			VortexStartInvasionSideEffectStep.SetActiveVortex(startResult.LocationId),
			VortexStartInvasionSideEffectStep.DespawnExistingVortexNpcs(startResult.LocationId),
		};

		foreach (var spawnedNpc in existingSpawnedNpcs)
			steps.Add(VortexStartInvasionSideEffectStep.DespawnExistingVortexNpc(spawnedNpc));

		foreach (var invasionSpawn in selectedInvasionSpawns)
			steps.Add(VortexStartInvasionSideEffectStep.SpawnInvasionNpc(invasionSpawn));

		steps.Add(VortexStartInvasionSideEffectStep.InitRiftGenerator(startResult.LocationId));
		steps.Add(VortexStartInvasionSideEffectStep.UpdateDefenderAlliance(startResult.LocationId));

		return new VortexStartInvasionSideEffectPlan(
			VortexStartInvasionSideEffectPlanStatus.Planned,
			startResult.LocationId,
			steps,
			DespawnNpcCount: existingSpawnedNpcs.Count,
			InvasionSpawnCount: selectedInvasionSpawns.Count,
			StartResult: startResult,
			JavaSource: "services/vortex/Invasion.startInvasion");
	}
}

public interface IVortexInvasionSpawnSnapshotSelector
{
	IReadOnlyList<VortexStartInvasionSpawnSnapshot> SelectInvasionSpawns(
		int vortexLocationId,
		NpcVortexSpawnTable vortexSpawns);
}

public sealed class VortexInvasionSpawnSnapshotSelectionService : IVortexInvasionSpawnSnapshotSelector
{
	public IReadOnlyList<VortexStartInvasionSpawnSnapshot> SelectInvasionSpawns(
		int vortexLocationId,
		NpcVortexSpawnTable vortexSpawns)
	{
		ArgumentNullException.ThrowIfNull(vortexSpawns);

		// Java parity: services/VortexService.spawn(loc, VortexStateType.INVASION)
		// scans DataManager.SPAWNS_DATA.getVortexSpawnsByLocId(loc.getId()) and
		// selects only VortexSpawnTemplate rows whose stateType is INVASION.
		return vortexSpawns
			.GetSpawnsForVortexLocation(vortexLocationId, VortexStateType.Invasion)
			.Select(VortexStartInvasionSpawnSnapshot.FromVortexSpawn)
			.ToArray();
	}
}

public enum VortexStartInvasionSideEffectPlanStatus
{
	AlreadyStarted,
	Planned,
}

public enum VortexStartInvasionSideEffectStepKind
{
	SetActiveVortex,
	DespawnExistingVortexNpcs,
	DespawnExistingVortexNpc,
	SpawnInvasionNpc,
	InitRiftGenerator,
	UpdateDefenderAlliance,
}

public sealed record VortexStartInvasionSideEffectPlan(
	VortexStartInvasionSideEffectPlanStatus Status,
	int LocationId,
	IReadOnlyList<VortexStartInvasionSideEffectStep>? Steps = null,
	int DespawnNpcCount = 0,
	int InvasionSpawnCount = 0,
	VortexStartInvasionResult? StartResult = null,
	string JavaSource = "")
{
	public IReadOnlyList<VortexStartInvasionSideEffectStep> OrderedSteps => Steps ?? [];
	public bool ShouldExecuteLiveSideEffects => false;
}

public sealed record VortexStartInvasionSideEffectStep(
	VortexStartInvasionSideEffectStepKind Kind,
	int? ObjectId = null,
	int? NpcId = null,
	NpcSpawnSummary? Spawn = null,
	VortexStateType? VortexState = null,
	RiftPortalState? ActivePortal = null,
	string JavaSource = "")
{
	public static VortexStartInvasionSideEffectStep SetActiveVortex(int locationId)
	{
		return new VortexStartInvasionSideEffectStep(
			VortexStartInvasionSideEffectStepKind.SetActiveVortex,
			ObjectId: locationId,
			JavaSource: "services/vortex/Invasion.startInvasion -> model/vortex/VortexLocation.setActiveVortex(this)");
	}

	public static VortexStartInvasionSideEffectStep DespawnExistingVortexNpcs(int locationId)
	{
		return new VortexStartInvasionSideEffectStep(
			VortexStartInvasionSideEffectStepKind.DespawnExistingVortexNpcs,
			ObjectId: locationId,
			JavaSource: "services/vortex/Invasion.startInvasion -> services/VortexService.despawn");
	}

	public static VortexStartInvasionSideEffectStep DespawnExistingVortexNpc(VortexStartSpawnedNpcSnapshot spawnedNpc)
	{
		return new VortexStartInvasionSideEffectStep(
			VortexStartInvasionSideEffectStepKind.DespawnExistingVortexNpc,
			ObjectId: spawnedNpc.ObjectId,
			NpcId: spawnedNpc.NpcId,
			JavaSource: "services/VortexService.despawn -> VisibleObject.getController().deleteIfAliveOrCancelRespawn");
	}

	public static VortexStartInvasionSideEffectStep SpawnInvasionNpc(VortexStartInvasionSpawnSnapshot invasionSpawn)
	{
		return new VortexStartInvasionSideEffectStep(
			VortexStartInvasionSideEffectStepKind.SpawnInvasionNpc,
			NpcId: invasionSpawn.Spawn.NpcId,
			Spawn: invasionSpawn.Spawn,
			VortexState: invasionSpawn.State,
			JavaSource: "services/vortex/Invasion.startInvasion -> services/VortexService.spawn(VortexStateType.INVASION)");
	}

	public static VortexStartInvasionSideEffectStep InitRiftGenerator(int locationId)
	{
		return new VortexStartInvasionSideEffectStep(
			VortexStartInvasionSideEffectStepKind.InitRiftGenerator,
			ObjectId: locationId,
			JavaSource: "services/vortex/Invasion.startInvasion -> services/vortex/DimensionalVortex.initRiftGenerator");
	}

	public static VortexStartInvasionSideEffectStep UpdateDefenderAlliance(int locationId)
	{
		return new VortexStartInvasionSideEffectStep(
			VortexStartInvasionSideEffectStepKind.UpdateDefenderAlliance,
			ObjectId: locationId,
			JavaSource: "services/vortex/Invasion.startInvasion -> services/vortex/Invasion.updateAlliance");
	}
}

public sealed record VortexStartSpawnedNpcSnapshot(
	int ObjectId,
	int NpcId)
{
	public static VortexStartSpawnedNpcSnapshot FromWorldNpc(IWorldNpcObject npc)
	{
		ArgumentNullException.ThrowIfNull(npc);
		return new VortexStartSpawnedNpcSnapshot(npc.ObjectId, npc.TemplateId);
	}
}

public sealed record VortexStartInvasionSpawnSnapshot(
	NpcSpawnSummary Spawn,
	VortexStateType State = VortexStateType.Invasion)
{
	public static VortexStartInvasionSpawnSnapshot FromVortexSpawn(NpcVortexSpawnSummary spawn)
	{
		ArgumentNullException.ThrowIfNull(spawn);
		if (spawn.StateType != VortexStateType.Invasion)
			throw new ArgumentException("Only INVASION Vortex spawn rows can become start INVASION spawn snapshots.", nameof(spawn));

		return new VortexStartInvasionSpawnSnapshot(
			new NpcSpawnSummary(
				spawn.MapId,
				spawn.NpcId,
				spawn.X,
				spawn.Y,
				spawn.Z,
				spawn.Heading,
				spawn.RespawnSeconds,
				spawn.PoolSize,
				spawn.DifficultId,
				spawn.Handler,
				spawn.StaticId,
				spawn.RandomWalkRange,
				spawn.WalkerId,
				spawn.WalkerIndex,
				spawn.Anchor,
				spawn.State,
				spawn.AiName,
				spawn.Custom,
				spawn.GroupTemporarySchedule,
				spawn.SpotTemporarySchedule),
			VortexStateType.Invasion);
	}
}
