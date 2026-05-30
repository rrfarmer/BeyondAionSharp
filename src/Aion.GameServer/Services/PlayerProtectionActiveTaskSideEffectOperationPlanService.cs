using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public enum PlayerProtectionActiveTaskSideEffectOperation
{
	CheckProtectionActive,
	SetBlinkingVisualState,
	CancelCastOnKnownCreatures,
	RemoveTargetFromKnownPlayers,
	BroadcastPlayerState,
	ScheduleDelayedStopTask,
	StoreProtectionTask,
	CancelProtectionTask,
	CheckSpawned,
	UnsetBlinkingVisualState,
	NotifyAiOnMove,
	SkipNotifyAiOnMoveForFlightPath,
}

public enum PlayerProtectionActiveTaskSideEffectOperationStatus
{
	ObservedCondition,
	SkippedBranch,
	PlannedNotLive,
	LiveVisualMutation,
}

public sealed record PlayerProtectionActiveTaskSideEffectOperationRow(
	int Order,
	PlayerProtectionActiveTaskSideEffectOperation Operation,
	PlayerProtectionActiveTaskSideEffectOperationStatus Status,
	bool JavaCallReached,
	bool IsLive,
	string JavaOperation,
	string JavaSource,
	string Notes
);

public sealed record PlayerProtectionActiveTaskSideEffectOperationPlan(
	PlayerProtectionActiveTaskAdapterAction Action,
	PlayerProtectionActiveTaskPlanStatus PlanStatus,
	IReadOnlyList<PlayerProtectionActiveTaskSideEffectOperationRow> Rows,
	bool SchedulesDelayedStop,
	bool CancelsExistingStopTask,
	bool CancelsKnownCreatureCasts,
	bool ClearsKnownPlayerTargets,
	bool NotifiesAiOnMove,
	bool SkipsAiMoveNotificationForFlightPath,
	bool HasLiveVisualMutationOnly,
	string JavaSource,
	bool IsLive
);

public static class PlayerProtectionActiveTaskSideEffectOperationPlanService
{
	public static PlayerProtectionActiveTaskSideEffectOperationPlan Create(
		PlayerProtectionActiveTaskAdapterRequest request,
		PlayerProtectionActiveTaskAdapterResult adapterResult
	)
	{
		// Java parity: PlayerController protection-task start/stop executes a fixed side-effect order:
		// visual state, AttackUtil helpers, player-state broadcast, scheduler/task storage, and stop-path
		// AI notification. This planner keeps that order explicit while only the visual mutation may be live.
		var rows =
			request.Action == PlayerProtectionActiveTaskAdapterAction.Start
				? CreateStartRows(adapterResult)
				: CreateStopRows(request.Player, adapterResult);
		var rowList = rows.ToArray();

		return new PlayerProtectionActiveTaskSideEffectOperationPlan(
			request.Action,
			adapterResult.Plan.Status,
			rowList,
			SchedulesDelayedStop: rowList.Any(row =>
				row.Operation == PlayerProtectionActiveTaskSideEffectOperation.ScheduleDelayedStopTask && row.JavaCallReached
			),
			CancelsExistingStopTask: rowList.Any(row =>
				row.Operation == PlayerProtectionActiveTaskSideEffectOperation.CancelProtectionTask && row.JavaCallReached
			),
			CancelsKnownCreatureCasts: rowList.Any(row =>
				row.Operation == PlayerProtectionActiveTaskSideEffectOperation.CancelCastOnKnownCreatures && row.JavaCallReached
			),
			ClearsKnownPlayerTargets: rowList.Any(row =>
				row.Operation == PlayerProtectionActiveTaskSideEffectOperation.RemoveTargetFromKnownPlayers && row.JavaCallReached
			),
			NotifiesAiOnMove: rowList.Any(row => row.Operation == PlayerProtectionActiveTaskSideEffectOperation.NotifyAiOnMove && row.JavaCallReached),
			SkipsAiMoveNotificationForFlightPath: rowList.Any(row =>
				row.Operation == PlayerProtectionActiveTaskSideEffectOperation.SkipNotifyAiOnMoveForFlightPath && row.JavaCallReached
			),
			HasLiveVisualMutationOnly: adapterResult.MutatedVisualState
				&& rowList.All(row =>
					!row.IsLive
					|| row.Operation
						is PlayerProtectionActiveTaskSideEffectOperation.SetBlinkingVisualState
							or PlayerProtectionActiveTaskSideEffectOperation.UnsetBlinkingVisualState
				),
			"PlayerController.startProtectionActiveTask / stopProtectionActiveTask ordered side-effect plan",
			IsLive: adapterResult.IsLive
		);
	}

	private static IEnumerable<PlayerProtectionActiveTaskSideEffectOperationRow> CreateStartRows(PlayerProtectionActiveTaskAdapterResult adapterResult)
	{
		yield return Row(
			0,
			PlayerProtectionActiveTaskSideEffectOperation.CheckProtectionActive,
			PlayerProtectionActiveTaskSideEffectOperationStatus.ObservedCondition,
			javaCallReached: true,
			isLive: false,
			"if (!getOwner().isProtectionActive())",
			"PlayerController.startProtectionActiveTask",
			adapterResult.Plan.WasProtectionActive
				? "Already protected; Java exits without side effects."
				: "Protection inactive; Java enters side-effect branch."
		);

		if (adapterResult.Plan.Status == PlayerProtectionActiveTaskPlanStatus.AlreadyProtected)
			yield break;

		yield return Row(
			1,
			PlayerProtectionActiveTaskSideEffectOperation.SetBlinkingVisualState,
			adapterResult.MutatedVisualState
				? PlayerProtectionActiveTaskSideEffectOperationStatus.LiveVisualMutation
				: PlayerProtectionActiveTaskSideEffectOperationStatus.PlannedNotLive,
			javaCallReached: true,
			adapterResult.MutatedVisualState,
			"getOwner().setVisualState(CreatureVisualState.BLINKING)",
			"PlayerController.startProtectionActiveTask",
			"Visual-state mutation is the only live start-side effect currently allowed."
		);
		yield return Row(
			2,
			PlayerProtectionActiveTaskSideEffectOperation.CancelCastOnKnownCreatures,
			PlayerProtectionActiveTaskSideEffectOperationStatus.PlannedNotLive,
			javaCallReached: true,
			isLive: false,
			"AttackUtil.cancelCastOn(getOwner())",
			"AttackUtil.cancelCastOn -> knownList.forEachObject creature casting at owner -> cancelCurrentSkill(null)",
			"Requires live known-list creature traversal and casting-skill state."
		);
		yield return Row(
			3,
			PlayerProtectionActiveTaskSideEffectOperation.RemoveTargetFromKnownPlayers,
			PlayerProtectionActiveTaskSideEffectOperationStatus.PlannedNotLive,
			javaCallReached: true,
			isLive: false,
			"AttackUtil.removeTargetFrom(getOwner())",
			"AttackUtil.removeTargetFrom -> knownList.forEachPlayer player.target == owner -> player.setTarget(null)",
			"Requires live known-list player target mutation."
		);
		yield return Row(
			4,
			PlayerProtectionActiveTaskSideEffectOperation.BroadcastPlayerState,
			PlayerProtectionActiveTaskSideEffectOperationStatus.PlannedNotLive,
			javaCallReached: true,
			isLive: false,
			"PacketSendUtility.broadcastToSightedPlayers(getOwner(), new SM_PLAYER_STATE(getOwner()), true)",
			"PlayerController.startProtectionActiveTask",
			"Packet construction and disabled executor metadata exist; production sends remain disabled."
		);
		yield return Row(
			5,
			PlayerProtectionActiveTaskSideEffectOperation.ScheduleDelayedStopTask,
			PlayerProtectionActiveTaskSideEffectOperationStatus.PlannedNotLive,
			javaCallReached: true,
			isLive: false,
			"ThreadPoolManager.getInstance().schedule(this::stopProtectionActiveTask, 60000)",
			"PlayerController.startProtectionActiveTask",
			"Requires scheduler and task-owner integration before live execution."
		);
		yield return Row(
			6,
			PlayerProtectionActiveTaskSideEffectOperation.StoreProtectionTask,
			PlayerProtectionActiveTaskSideEffectOperationStatus.PlannedNotLive,
			javaCallReached: true,
			isLive: false,
			"addTask(TaskId.PROTECTION_ACTIVE, scheduledFuture)",
			"PlayerController.startProtectionActiveTask",
			"TaskId ordinal and delay are modeled in the base plan; no live task is stored here."
		);
	}

	private static IEnumerable<PlayerProtectionActiveTaskSideEffectOperationRow> CreateStopRows(
		Player player,
		PlayerProtectionActiveTaskAdapterResult adapterResult
	)
	{
		yield return Row(
			0,
			PlayerProtectionActiveTaskSideEffectOperation.CancelProtectionTask,
			PlayerProtectionActiveTaskSideEffectOperationStatus.PlannedNotLive,
			javaCallReached: true,
			isLive: false,
			"cancelTask(TaskId.PROTECTION_ACTIVE)",
			"PlayerController.stopProtectionActiveTask",
			"Java calls cancelTask before checking player.isSpawned(); C# records this even when no task fact is supplied."
		);
		yield return Row(
			1,
			PlayerProtectionActiveTaskSideEffectOperation.CheckSpawned,
			PlayerProtectionActiveTaskSideEffectOperationStatus.ObservedCondition,
			javaCallReached: true,
			isLive: false,
			"if (player.isSpawned())",
			"PlayerController.stopProtectionActiveTask",
			adapterResult.Plan.IsSpawned ? "Spawned; Java continues with visual/fanout/AI side effects." : "Unspawned; Java stops after cancelTask."
		);

		if (!adapterResult.Plan.IsSpawned)
			yield break;

		yield return Row(
			2,
			PlayerProtectionActiveTaskSideEffectOperation.UnsetBlinkingVisualState,
			adapterResult.MutatedVisualState
				? PlayerProtectionActiveTaskSideEffectOperationStatus.LiveVisualMutation
				: PlayerProtectionActiveTaskSideEffectOperationStatus.PlannedNotLive,
			javaCallReached: true,
			adapterResult.MutatedVisualState,
			"player.unsetVisualState(CreatureVisualState.BLINKING)",
			"PlayerController.stopProtectionActiveTask",
			"Visual-state mutation is the only live stop-side effect currently allowed."
		);
		yield return Row(
			3,
			PlayerProtectionActiveTaskSideEffectOperation.BroadcastPlayerState,
			PlayerProtectionActiveTaskSideEffectOperationStatus.PlannedNotLive,
			javaCallReached: true,
			isLive: false,
			"PacketSendUtility.broadcastToSightedPlayers(player, new SM_PLAYER_STATE(player), true)",
			"PlayerController.stopProtectionActiveTask",
			"Packet construction and disabled executor metadata exist; production sends remain disabled."
		);

		var usingFlightPath =
			player.IsUsingFlightPath(PlayerFlightPathType.FlightTransporter) || player.IsUsingFlightPath(PlayerFlightPathType.Windstream);
		yield return Row(
			4,
			usingFlightPath
				? PlayerProtectionActiveTaskSideEffectOperation.SkipNotifyAiOnMoveForFlightPath
				: PlayerProtectionActiveTaskSideEffectOperation.NotifyAiOnMove,
			usingFlightPath
				? PlayerProtectionActiveTaskSideEffectOperationStatus.SkippedBranch
				: PlayerProtectionActiveTaskSideEffectOperationStatus.PlannedNotLive,
			javaCallReached: true,
			isLive: false,
			"notifyAIOnMove()",
			usingFlightPath
				? "PlayerController.notifyAIOnMove -> if using flight transporter or windstream, return"
				: "PlayerController.notifyAIOnMove -> CreatureController.notifyAIOnMove -> MovementNotifyTask.add(owner)",
			usingFlightPath
				? "Movement notification is skipped by Java's player flight-path guard."
				: "Requires live MovementNotifyTask integration before execution."
		);
	}

	private static PlayerProtectionActiveTaskSideEffectOperationRow Row(
		int order,
		PlayerProtectionActiveTaskSideEffectOperation operation,
		PlayerProtectionActiveTaskSideEffectOperationStatus status,
		bool javaCallReached,
		bool isLive,
		string javaOperation,
		string javaSource,
		string notes
	) => new(order, operation, status, javaCallReached, isLive, javaOperation, javaSource, notes);
}
