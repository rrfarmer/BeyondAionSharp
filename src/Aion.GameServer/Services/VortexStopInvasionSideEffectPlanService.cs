using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.World;

namespace Aion.GameServer.Services;

public sealed class VortexStopInvasionSideEffectPlanService
{
	private readonly VortexKickPlayerRemovalPlanService _kickRemovalPlanner = new();

	public VortexStopInvasionSideEffectPlan CreatePlan(
		VortexStopInvasionResult stopResult,
		IReadOnlyList<VortexStopInvaderSnapshot>? invaders = null,
		IReadOnlyList<VortexStopInvaderKiskSnapshot>? invaderKisks = null,
		IReadOnlyList<VortexStopSpawnedNpcSnapshot>? spawnedNpcs = null,
		IReadOnlyList<VortexStopPeaceSpawnSnapshot>? peaceSpawns = null,
		IReadOnlyDictionary<int, VortexKickPlayerAllianceSnapshot>? invaderAlliances = null,
		IReadOnlySet<int>? passedPlayerObjectIds = null)
	{
		ArgumentNullException.ThrowIfNull(stopResult);

		if (!stopResult.Stopped)
		{
			return new VortexStopInvasionSideEffectPlan(
				stopResult.Status == VortexStopInvasionStatus.FinishedInvasion
					? VortexStopInvasionSideEffectPlanStatus.FinishedInvasion
					: VortexStopInvasionSideEffectPlanStatus.MissingInvasion,
				stopResult.LocationId,
				JavaSource: stopResult.JavaSource);
		}

		if (stopResult.PreviousSnapshot == null || stopResult.StoppedSnapshot == null)
		{
			return new VortexStopInvasionSideEffectPlan(
				VortexStopInvasionSideEffectPlanStatus.MissingStopSnapshot,
				stopResult.LocationId,
				JavaSource: "services/VortexService.stopInvasion -> services/vortex/Invasion.stopInvasion");
		}

		var steps = new List<VortexStopInvasionSideEffectStep>
		{
			VortexStopInvasionSideEffectStep.ClearActiveVortex(stopResult.LocationId),
		};

		foreach (var kisk in invaderKisks ?? [])
			steps.Add(VortexStopInvasionSideEffectStep.KillInvaderKisk(kisk));

		var kickRemovalPlans = new List<VortexKickPlayerRemovalPlan>();
		var remainingPassedPlayers = passedPlayerObjectIds == null
			? new HashSet<int>()
			: new HashSet<int>(passedPlayerObjectIds);
		foreach (var invader in invaders ?? [])
		{
			if (!invader.IsOnline)
				continue;

			var passedPlayersBeforeKick = remainingPassedPlayers.ToHashSet();
			remainingPassedPlayers.Remove(invader.PlayerObjectId);
			VortexKickPlayerAllianceSnapshot? invaderAlliance = null;
			invaderAlliances?.TryGetValue(invader.PlayerObjectId, out invaderAlliance);
			var kickRemovalPlan = _kickRemovalPlanner.CreatePlan(
				stopResult.LocationId,
				new VortexKickPlayerSnapshot(
					invader.PlayerObjectId,
					invader.IsOnline,
					invader.WorldId),
				isInvader: true,
				isParticipant: true,
				alliance: invaderAlliance,
				passedPlayerObjectIds: passedPlayersBeforeKick,
				passedPlayerCountAfterRemoval: remainingPassedPlayers.Count,
				invasionWorldId: stopResult.PreviousSnapshot.StartPoint.WorldId,
				homePoint: stopResult.PreviousSnapshot.HomePoint);
			kickRemovalPlans.Add(kickRemovalPlan);

			steps.Add(VortexStopInvasionSideEffectStep.KickOnlineInvader(
				invader,
				stopResult.PreviousSnapshot.StartPoint,
				stopResult.PreviousSnapshot.HomePoint,
				kickRemovalPlan));
		}

		foreach (var spawnedNpc in spawnedNpcs ?? [])
			steps.Add(VortexStopInvasionSideEffectStep.DespawnVortexNpc(spawnedNpc));

		foreach (var peaceSpawn in peaceSpawns ?? [])
			steps.Add(VortexStopInvasionSideEffectStep.SpawnPeaceNpc(peaceSpawn));

		return new VortexStopInvasionSideEffectPlan(
			VortexStopInvasionSideEffectPlanStatus.Planned,
			stopResult.LocationId,
			steps,
			KiskKillCount: (invaderKisks ?? []).Count,
			OnlineInvaderKickCount: (invaders ?? []).Count(invader => invader.IsOnline),
			DespawnNpcCount: (spawnedNpcs ?? []).Count,
			PeaceSpawnCount: (peaceSpawns ?? []).Count,
			KickRemovalPlans: kickRemovalPlans,
			JavaSource: "services/VortexService.stopInvasion -> services/vortex/Invasion.stopInvasion");
	}
}

public interface IVortexPeaceSpawnSnapshotSelector
{
	IReadOnlyList<VortexStopPeaceSpawnSnapshot> SelectPeaceSpawns(
		int vortexLocationId,
		NpcVortexSpawnTable vortexSpawns);
}

public sealed class VortexPeaceSpawnSnapshotSelectionService : IVortexPeaceSpawnSnapshotSelector
{
	public IReadOnlyList<VortexStopPeaceSpawnSnapshot> SelectPeaceSpawns(
		int vortexLocationId,
		NpcVortexSpawnTable vortexSpawns)
	{
		ArgumentNullException.ThrowIfNull(vortexSpawns);

		// Java parity: services/VortexService.spawn(loc, VortexStateType.PEACE)
		// scans DataManager.SPAWNS_DATA.getVortexSpawnsByLocId(loc.getId()) and
		// selects only VortexSpawnTemplate rows whose stateType is PEACE.
		return vortexSpawns
			.GetSpawnsForVortexLocation(vortexLocationId, VortexStateType.Peace)
			.Select(VortexStopPeaceSpawnSnapshot.FromVortexSpawn)
			.ToArray();
	}
}

public enum VortexStopInvasionSideEffectPlanStatus
{
	MissingInvasion,
	FinishedInvasion,
	MissingStopSnapshot,
	Planned,
}

public enum VortexStopInvasionSideEffectStepKind
{
	ClearActiveVortex,
	KillInvaderKisk,
	KickOnlineInvader,
	DespawnVortexNpc,
	SpawnPeaceNpc,
}

public enum VortexStateType
{
	Invasion,
	Peace,
}

public sealed record VortexStopInvasionSideEffectPlan(
	VortexStopInvasionSideEffectPlanStatus Status,
	int LocationId,
	IReadOnlyList<VortexStopInvasionSideEffectStep>? Steps = null,
	int KiskKillCount = 0,
	int OnlineInvaderKickCount = 0,
	int DespawnNpcCount = 0,
	int PeaceSpawnCount = 0,
	IReadOnlyList<VortexKickPlayerRemovalPlan>? KickRemovalPlans = null,
	string JavaSource = "")
{
	public IReadOnlyList<VortexStopInvasionSideEffectStep> OrderedSteps => Steps ?? [];
	public IReadOnlyList<VortexKickPlayerRemovalPlan> OrderedKickRemovalPlans => KickRemovalPlans ?? [];
	public bool HasKickRemovalPlans => OrderedKickRemovalPlans.Count > 0;
	public bool ShouldExecuteLiveSideEffects => false;
}

public sealed record VortexStopInvasionSideEffectStep(
	VortexStopInvasionSideEffectStepKind Kind,
	int? ObjectId = null,
	int? PlayerObjectId = null,
	int? NpcId = null,
	bool IsOnline = false,
	bool WasInInvasionWorld = false,
	bool ShouldTeleportHome = false,
	WorldPosition? TeleportDestination = null,
	VortexKickPlayerRemovalPlan? KickRemovalPlan = null,
	NpcSpawnSummary? Spawn = null,
	VortexStateType? VortexState = null,
	string JavaSource = "")
{
	public static VortexStopInvasionSideEffectStep ClearActiveVortex(int locationId)
	{
		return new VortexStopInvasionSideEffectStep(
			VortexStopInvasionSideEffectStepKind.ClearActiveVortex,
			ObjectId: locationId,
			JavaSource: "services/vortex/Invasion.stopInvasion -> model/vortex/VortexLocation.setActiveVortex(null)");
	}

	public static VortexStopInvasionSideEffectStep KillInvaderKisk(VortexStopInvaderKiskSnapshot kisk)
	{
		return new VortexStopInvasionSideEffectStep(
			VortexStopInvasionSideEffectStepKind.KillInvaderKisk,
			ObjectId: kisk.KiskObjectId,
			PlayerObjectId: kisk.OwnerObjectId,
			NpcId: kisk.NpcId,
			JavaSource: "services/vortex/Invasion.stopInvasion -> model/gameobjects/Kisk.getController().die");
	}

	public static VortexStopInvasionSideEffectStep KickOnlineInvader(
		VortexStopInvaderSnapshot invader,
		WorldPosition invasionStartPoint,
		WorldPosition homePoint,
		VortexKickPlayerRemovalPlan? kickRemovalPlan = null)
	{
		var wasInInvasionWorld = invader.WorldId == invasionStartPoint.WorldId;
		return new VortexStopInvasionSideEffectStep(
			VortexStopInvasionSideEffectStepKind.KickOnlineInvader,
			PlayerObjectId: invader.PlayerObjectId,
			IsOnline: invader.IsOnline,
			WasInInvasionWorld: wasInInvasionWorld,
			ShouldTeleportHome: wasInInvasionWorld,
			TeleportDestination: wasInInvasionWorld ? homePoint : null,
			KickRemovalPlan: kickRemovalPlan,
			JavaSource: "services/vortex/Invasion.stopInvasion -> services/vortex/Invasion.kickPlayer(player, true)");
	}

	public static VortexStopInvasionSideEffectStep DespawnVortexNpc(VortexStopSpawnedNpcSnapshot spawnedNpc)
	{
		return new VortexStopInvasionSideEffectStep(
			VortexStopInvasionSideEffectStepKind.DespawnVortexNpc,
			ObjectId: spawnedNpc.ObjectId,
			NpcId: spawnedNpc.NpcId,
			JavaSource: "services/vortex/Invasion.stopInvasion -> services/VortexService.despawn");
	}

	public static VortexStopInvasionSideEffectStep SpawnPeaceNpc(VortexStopPeaceSpawnSnapshot peaceSpawn)
	{
		return new VortexStopInvasionSideEffectStep(
			VortexStopInvasionSideEffectStepKind.SpawnPeaceNpc,
			NpcId: peaceSpawn.Spawn.NpcId,
			Spawn: peaceSpawn.Spawn,
			VortexState: peaceSpawn.State,
			JavaSource: "services/vortex/Invasion.stopInvasion -> services/VortexService.spawn(VortexStateType.PEACE)");
	}
}

public sealed record VortexStopInvaderSnapshot(
	int PlayerObjectId,
	bool IsOnline,
	int WorldId)
{
	public static VortexStopInvaderSnapshot FromPlayer(Player player)
	{
		ArgumentNullException.ThrowIfNull(player);
		return new VortexStopInvaderSnapshot(player.ObjectId, player.IsOnline, player.Position.WorldId);
	}
}

public sealed record VortexStopInvaderKiskSnapshot(
	int KiskObjectId,
	int OwnerObjectId,
	int NpcId)
{
	public static VortexStopInvaderKiskSnapshot FromRuntimeState(PlayerKiskRuntimeState kisk)
	{
		ArgumentNullException.ThrowIfNull(kisk);
		return new VortexStopInvaderKiskSnapshot(kisk.ObjectId, kisk.OwnerObjectId, kisk.NpcId);
	}
}

public sealed record VortexStopSpawnedNpcSnapshot(
	int ObjectId,
	int NpcId)
{
	public static VortexStopSpawnedNpcSnapshot FromWorldNpc(IWorldNpcObject npc)
	{
		ArgumentNullException.ThrowIfNull(npc);
		return new VortexStopSpawnedNpcSnapshot(npc.ObjectId, npc.TemplateId);
	}
}

public sealed record VortexStopPeaceSpawnSnapshot(
	NpcSpawnSummary Spawn,
	VortexStateType State = VortexStateType.Peace)
{
	public static VortexStopPeaceSpawnSnapshot FromSpawn(NpcSpawnSummary spawn)
	{
		ArgumentNullException.ThrowIfNull(spawn);
		return new VortexStopPeaceSpawnSnapshot(spawn, VortexStateType.Peace);
	}

	public static VortexStopPeaceSpawnSnapshot FromVortexSpawn(NpcVortexSpawnSummary spawn)
	{
		ArgumentNullException.ThrowIfNull(spawn);
		if (spawn.StateType != VortexStateType.Peace)
			throw new ArgumentException("Only PEACE Vortex spawn rows can become stop PEACE spawn snapshots.", nameof(spawn));

		return new VortexStopPeaceSpawnSnapshot(
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
			VortexStateType.Peace);
	}
}
