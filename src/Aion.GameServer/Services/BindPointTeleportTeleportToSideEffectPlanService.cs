namespace Aion.GameServer.Services;

public enum BindPointTeleportTeleportToSideEffectPlanStatus
{
	BlockedFinalMovement,
	ReadySameInstance,
	ReadyMapOrInstanceChange,
}

public enum BindPointTeleportTeleportToSideEffectPlanStep
{
	EnterTeleportToOverload,
	UsePlayerHeadingAndNoneAnimation,
	ResolveTargetInstance,
	ReviveDeadAfterGateIfNeeded,
	LoseDuelIfNeeded,
	AbortPlayerActions,
	DespawnPlayer,
	CreateSpawnTask,
	RunSpawnTaskImmediately,
	CheckAlreadySpawned,
	SnapshotCurrentWorldAndInstance,
	LeaveMapAndInstanceIfChanged,
	SetPlayerPosition,
	SetPetPosition,
	SetPortAnimation,
	SendChannelInfo,
	SendPlayerInfo,
	SendStatsInfo,
	SendMotion,
	SpawnPlayerAndPet,
	StartProtection,
	UpdateEffectIcons,
	UpdateZone,
	ResetPortAnimationNone,
	SendPlayerSpawn,
	SendInstanceOpenedMessage,
	UpdateLegionMemberInfoIfWorldChanged,
}

public enum BindPointTeleportTeleportToSideEffectGap
{
	None,
	FinalMovementBlocked,
	DeadAfterGateReviveFallback,
	DuelLoss,
	PrivateStoreClosure,
	CurrentSkillCancel,
	TargetClear,
	RideModeUnset,
	WorldDespawn,
	WorldSpawn,
	PetMovement,
	SpawnedStateNoOp,
	LeaveMapCallback,
	LeaveInstanceCallback,
	ProtectionTask,
	EffectIconRefresh,
	ZoneUpdate,
	LegionRefresh,
	KnownListMembership,
	JavaRuntimeComparison,
}

public sealed record BindPointTeleportTeleportToSideEffectPlan(
	BindPointTeleportTeleportToSideEffectPlanStatus Status,
	BindPointTeleportFinalMovementPlan FinalMovementPlan,
	IReadOnlyList<BindPointTeleportTeleportToSideEffectPlanStep> Steps,
	IReadOnlyList<BindPointTeleportTeleportToSideEffectGap> Gaps,
	bool ShouldAbortPlayerActions,
	bool ShouldBroadcastDelete,
	bool ShouldSendTeleportLoc,
	bool ShouldRunSpawnTaskImmediately,
	bool ShouldMovePlayer,
	bool ShouldMovePet,
	bool ShouldUseSameInstanceSpawnPath,
	bool ShouldSendChannelInfo,
	bool ShouldSendPlayerInfo,
	bool ShouldSendStatsInfo,
	bool ShouldSendMotion,
	bool ShouldSendPlayerSpawn,
	bool ShouldSendInstanceOpenedMessage,
	bool ShouldUpdateZone,
	bool ShouldRefreshLegionMemberInfo,
	string JavaSource,
	bool IsLive);

public static class BindPointTeleportTeleportToSideEffectPlanService
{
	public static BindPointTeleportTeleportToSideEffectPlan CreatePlan(
		BindPointTeleportFinalMovementPlan finalMovementPlan,
		bool destinationIsInstance = false,
		bool destinationIsPersonalWorld = false,
		bool playerBecameDeadAfterFinalGate = false,
		bool playerIsDueling = false)
	{
		// Java parity: TeleportService.teleportTo(player, worldId, x, y, z) from BindPointTeleportService final task.
		// This is non-live ordering metadata only; no player state, world state, packet, or known-list side effect runs here.
		if (!finalMovementPlan.ShouldTeleport)
		{
			return new BindPointTeleportTeleportToSideEffectPlan(
				BindPointTeleportTeleportToSideEffectPlanStatus.BlockedFinalMovement,
				finalMovementPlan,
				Array.Empty<BindPointTeleportTeleportToSideEffectPlanStep>(),
				[BindPointTeleportTeleportToSideEffectGap.FinalMovementBlocked],
				ShouldAbortPlayerActions: false,
				ShouldBroadcastDelete: false,
				ShouldSendTeleportLoc: false,
				ShouldRunSpawnTaskImmediately: false,
				ShouldMovePlayer: false,
				ShouldMovePet: false,
				ShouldUseSameInstanceSpawnPath: false,
				ShouldSendChannelInfo: false,
				ShouldSendPlayerInfo: false,
				ShouldSendStatsInfo: false,
				ShouldSendMotion: false,
				ShouldSendPlayerSpawn: false,
				ShouldSendInstanceOpenedMessage: false,
				ShouldUpdateZone: false,
				ShouldRefreshLegionMemberInfo: false,
				"BindPointTeleportService final movement gate blocked before TeleportService.teleportTo",
				IsLive: false);
		}

		var sameInstance = finalMovementPlan.Destination.CurrentWorldId == finalMovementPlan.TargetWorldId
			&& finalMovementPlan.Destination.CurrentInstanceId == finalMovementPlan.TargetInstanceId;
		var shouldSendInstanceOpened = !sameInstance && destinationIsInstance && !destinationIsPersonalWorld;
		var shouldRefreshLegion = finalMovementPlan.Destination.CurrentWorldId != finalMovementPlan.TargetWorldId;

		var gaps = CreateGapList(
			sameInstance,
			playerBecameDeadAfterFinalGate,
			playerIsDueling,
			shouldRefreshLegion);

		return new BindPointTeleportTeleportToSideEffectPlan(
			sameInstance
				? BindPointTeleportTeleportToSideEffectPlanStatus.ReadySameInstance
				: BindPointTeleportTeleportToSideEffectPlanStatus.ReadyMapOrInstanceChange,
			finalMovementPlan,
			CreateSteps(sameInstance, shouldSendInstanceOpened, shouldRefreshLegion, playerBecameDeadAfterFinalGate, playerIsDueling),
			gaps,
			ShouldAbortPlayerActions: true,
			ShouldBroadcastDelete: true,
			ShouldSendTeleportLoc: false,
			ShouldRunSpawnTaskImmediately: true,
			ShouldMovePlayer: true,
			ShouldMovePet: true,
			ShouldUseSameInstanceSpawnPath: sameInstance,
			ShouldSendChannelInfo: true,
			ShouldSendPlayerInfo: sameInstance,
			ShouldSendStatsInfo: sameInstance,
			ShouldSendMotion: sameInstance,
			ShouldSendPlayerSpawn: !sameInstance,
			ShouldSendInstanceOpenedMessage: shouldSendInstanceOpened,
			ShouldUpdateZone: sameInstance,
			ShouldRefreshLegionMemberInfo: shouldRefreshLegion,
			"TeleportService.teleportTo(player, worldId, x, y, z) -> heading + TeleportAnimation.NONE -> sendLoc -> abort actions -> despawn -> immediate SpawnTask",
			IsLive: false);
	}

	private static IReadOnlyList<BindPointTeleportTeleportToSideEffectPlanStep> CreateSteps(
		bool sameInstance,
		bool shouldSendInstanceOpened,
		bool shouldRefreshLegion,
		bool playerBecameDeadAfterFinalGate,
		bool playerIsDueling)
	{
		var steps = new List<BindPointTeleportTeleportToSideEffectPlanStep>
		{
			BindPointTeleportTeleportToSideEffectPlanStep.EnterTeleportToOverload,
			BindPointTeleportTeleportToSideEffectPlanStep.UsePlayerHeadingAndNoneAnimation,
			BindPointTeleportTeleportToSideEffectPlanStep.ResolveTargetInstance,
		};

		if (playerBecameDeadAfterFinalGate)
			steps.Add(BindPointTeleportTeleportToSideEffectPlanStep.ReviveDeadAfterGateIfNeeded);
		if (playerIsDueling)
			steps.Add(BindPointTeleportTeleportToSideEffectPlanStep.LoseDuelIfNeeded);

		steps.AddRange(
		[
			BindPointTeleportTeleportToSideEffectPlanStep.AbortPlayerActions,
			BindPointTeleportTeleportToSideEffectPlanStep.DespawnPlayer,
			BindPointTeleportTeleportToSideEffectPlanStep.CreateSpawnTask,
			BindPointTeleportTeleportToSideEffectPlanStep.RunSpawnTaskImmediately,
			BindPointTeleportTeleportToSideEffectPlanStep.CheckAlreadySpawned,
			BindPointTeleportTeleportToSideEffectPlanStep.SnapshotCurrentWorldAndInstance,
		]);

		if (!sameInstance)
			steps.Add(BindPointTeleportTeleportToSideEffectPlanStep.LeaveMapAndInstanceIfChanged);

		steps.AddRange(
		[
			BindPointTeleportTeleportToSideEffectPlanStep.SetPlayerPosition,
			BindPointTeleportTeleportToSideEffectPlanStep.SetPetPosition,
			BindPointTeleportTeleportToSideEffectPlanStep.SetPortAnimation,
			BindPointTeleportTeleportToSideEffectPlanStep.SendChannelInfo,
		]);

		if (sameInstance)
		{
			steps.AddRange(
			[
				BindPointTeleportTeleportToSideEffectPlanStep.SendPlayerInfo,
				BindPointTeleportTeleportToSideEffectPlanStep.SendStatsInfo,
				BindPointTeleportTeleportToSideEffectPlanStep.SendMotion,
				BindPointTeleportTeleportToSideEffectPlanStep.SpawnPlayerAndPet,
				BindPointTeleportTeleportToSideEffectPlanStep.StartProtection,
				BindPointTeleportTeleportToSideEffectPlanStep.UpdateEffectIcons,
				BindPointTeleportTeleportToSideEffectPlanStep.UpdateZone,
				BindPointTeleportTeleportToSideEffectPlanStep.ResetPortAnimationNone,
			]);
		}
		else
		{
			steps.Add(BindPointTeleportTeleportToSideEffectPlanStep.SendPlayerSpawn);
			if (shouldSendInstanceOpened)
				steps.Add(BindPointTeleportTeleportToSideEffectPlanStep.SendInstanceOpenedMessage);
		}

		if (shouldRefreshLegion)
			steps.Add(BindPointTeleportTeleportToSideEffectPlanStep.UpdateLegionMemberInfoIfWorldChanged);

		return steps;
	}

	private static IReadOnlyList<BindPointTeleportTeleportToSideEffectGap> CreateGapList(
		bool sameInstance,
		bool playerBecameDeadAfterFinalGate,
		bool playerIsDueling,
		bool shouldRefreshLegion)
	{
		var gaps = new List<BindPointTeleportTeleportToSideEffectGap>
		{
			BindPointTeleportTeleportToSideEffectGap.PrivateStoreClosure,
			BindPointTeleportTeleportToSideEffectGap.CurrentSkillCancel,
			BindPointTeleportTeleportToSideEffectGap.TargetClear,
			BindPointTeleportTeleportToSideEffectGap.RideModeUnset,
			BindPointTeleportTeleportToSideEffectGap.WorldDespawn,
			BindPointTeleportTeleportToSideEffectGap.WorldSpawn,
			BindPointTeleportTeleportToSideEffectGap.PetMovement,
			BindPointTeleportTeleportToSideEffectGap.SpawnedStateNoOp,
			BindPointTeleportTeleportToSideEffectGap.KnownListMembership,
			BindPointTeleportTeleportToSideEffectGap.JavaRuntimeComparison,
		};

		if (playerBecameDeadAfterFinalGate)
			gaps.Add(BindPointTeleportTeleportToSideEffectGap.DeadAfterGateReviveFallback);
		if (playerIsDueling)
			gaps.Add(BindPointTeleportTeleportToSideEffectGap.DuelLoss);
		if (!sameInstance)
		{
			gaps.Add(BindPointTeleportTeleportToSideEffectGap.LeaveMapCallback);
			gaps.Add(BindPointTeleportTeleportToSideEffectGap.LeaveInstanceCallback);
		}
		if (sameInstance)
		{
			gaps.Add(BindPointTeleportTeleportToSideEffectGap.ProtectionTask);
			gaps.Add(BindPointTeleportTeleportToSideEffectGap.EffectIconRefresh);
			gaps.Add(BindPointTeleportTeleportToSideEffectGap.ZoneUpdate);
		}
		if (shouldRefreshLegion)
			gaps.Add(BindPointTeleportTeleportToSideEffectGap.LegionRefresh);

		return gaps;
	}
}
