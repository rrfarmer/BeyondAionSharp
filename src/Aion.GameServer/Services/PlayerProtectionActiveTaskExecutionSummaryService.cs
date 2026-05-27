namespace Aion.GameServer.Services;

public enum PlayerProtectionActiveTaskExecutionSummaryRowKind
{
	ObservedCondition,
	VisualMutation,
	AttackUtilCastCancellation,
	AttackUtilTargetClear,
	PacketConstruction,
	PacketFanout,
	TaskOperation,
	AiMoveNotification,
	SkippedBranch,
}

public enum PlayerProtectionActiveTaskExecutionSummaryRowStatus
{
	ObservedCondition,
	SkippedBranch,
	LiveVisualMutation,
	PlannedNotLive,
	DisabledNoSend,
	LiveSocketExecution,
}

public sealed record PlayerProtectionActiveTaskExecutionSummaryRow(
	int Order,
	PlayerProtectionActiveTaskExecutionSummaryRowKind Kind,
	PlayerProtectionActiveTaskExecutionSummaryRowStatus Status,
	bool JavaCallReached,
	bool IsLive,
	bool MutatedState,
	bool SentPackets,
	IReadOnlyList<int> RelatedObjectIds,
	string JavaOperation,
	string JavaSource,
	string Notes);

public sealed record PlayerProtectionActiveTaskExecutionSummary(
	PlayerProtectionActiveTaskAdapterAction Action,
	PlayerProtectionActiveTaskAdapterStatus AdapterStatus,
	PlayerProtectionActiveTaskExecutionBridgeStatus BridgeStatus,
	int PlayerObjectId,
	IReadOnlyList<PlayerProtectionActiveTaskExecutionSummaryRow> Rows,
	bool HasLiveVisualMutationOnly,
	bool HasAttackUtilProjections,
	bool HasTaskOperations,
	bool HasPacketFanout,
	bool HasAiMoveNotification,
	bool SentPackets,
	string JavaSource,
	bool IsLive);

public static class PlayerProtectionActiveTaskExecutionSummaryService
{
	public static PlayerProtectionActiveTaskExecutionSummary Create(
		PlayerProtectionActiveTaskExecutionBridgeResult result)
	{
		var rows = new List<PlayerProtectionActiveTaskExecutionSummaryRow>();

		foreach (var row in result.SideEffectOperationPlan.Rows)
		{
			switch (row.Operation)
			{
				case PlayerProtectionActiveTaskSideEffectOperation.CheckProtectionActive:
				case PlayerProtectionActiveTaskSideEffectOperation.CheckSpawned:
					AddSideEffect(rows, row, PlayerProtectionActiveTaskExecutionSummaryRowKind.ObservedCondition, Status(row), Array.Empty<int>(), row.Notes);
					break;
				case PlayerProtectionActiveTaskSideEffectOperation.SetBlinkingVisualState:
				case PlayerProtectionActiveTaskSideEffectOperation.UnsetBlinkingVisualState:
					AddSideEffect(
						rows,
						row,
						PlayerProtectionActiveTaskExecutionSummaryRowKind.VisualMutation,
						Status(row),
						Array.Empty<int>(),
						row.IsLive
							? "C# executed the visual-state mutation; all other Java side effects remain disabled or planned."
							: row.Notes);
					break;
				case PlayerProtectionActiveTaskSideEffectOperation.CancelCastOnKnownCreatures:
					AddSideEffect(
						rows,
						row,
						PlayerProtectionActiveTaskExecutionSummaryRowKind.AttackUtilCastCancellation,
						PlayerProtectionActiveTaskExecutionSummaryRowStatus.PlannedNotLive,
						result.AttackUtilRecipientPlan.CastCancellationObjectIds,
						result.AttackUtilRecipientPlan.CastCancellationObjectIds.Count > 0
							? "Known-object facts project creatures whose current cast would be cancelled; C# does not call cancelCurrentSkill."
							: "No supplied known-object facts projected a live cast cancellation.");
					break;
				case PlayerProtectionActiveTaskSideEffectOperation.RemoveTargetFromKnownPlayers:
					AddSideEffect(
						rows,
						row,
						PlayerProtectionActiveTaskExecutionSummaryRowKind.AttackUtilTargetClear,
						PlayerProtectionActiveTaskExecutionSummaryRowStatus.PlannedNotLive,
						result.AttackUtilRecipientPlan.TargetClearPlayerObjectIds,
						result.AttackUtilRecipientPlan.TargetClearPlayerObjectIds.Count > 0
							? "Known-object facts project players whose target would be cleared; C# does not call setTarget(null)."
							: "No supplied known-object facts projected a player target clear.");
					break;
				case PlayerProtectionActiveTaskSideEffectOperation.BroadcastPlayerState:
					AddPacketConstruction(rows, result, row);
					AddPacketFanout(rows, result, row);
					break;
				case PlayerProtectionActiveTaskSideEffectOperation.ScheduleDelayedStopTask:
				case PlayerProtectionActiveTaskSideEffectOperation.StoreProtectionTask:
				case PlayerProtectionActiveTaskSideEffectOperation.CancelProtectionTask:
					AddTaskRows(rows, result, row);
					break;
				case PlayerProtectionActiveTaskSideEffectOperation.NotifyAiOnMove:
				case PlayerProtectionActiveTaskSideEffectOperation.SkipNotifyAiOnMoveForFlightPath:
					AddSideEffect(
						rows,
						row,
						row.Operation == PlayerProtectionActiveTaskSideEffectOperation.NotifyAiOnMove
							? PlayerProtectionActiveTaskExecutionSummaryRowKind.AiMoveNotification
							: PlayerProtectionActiveTaskExecutionSummaryRowKind.SkippedBranch,
						Status(row),
						Array.Empty<int>(),
						row.Notes);
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(row.Operation), row.Operation, null);
			}
		}

		var rowArray = rows.ToArray();
		var action = result.AdapterResult.Plan.Status is PlayerProtectionActiveTaskPlanStatus.StartProtection or PlayerProtectionActiveTaskPlanStatus.AlreadyProtected
			? PlayerProtectionActiveTaskAdapterAction.Start
			: PlayerProtectionActiveTaskAdapterAction.Stop;

		return new PlayerProtectionActiveTaskExecutionSummary(
			action,
			result.AdapterResult.Status,
			result.Status,
			result.AdapterResult.Plan.PlayerObjectId,
			rowArray,
			result.SideEffectOperationPlan.HasLiveVisualMutationOnly,
			result.AttackUtilRecipientPlan.CastCancellationObjectIds.Count > 0
				|| result.AttackUtilRecipientPlan.TargetClearPlayerObjectIds.Count > 0,
			rowArray.Any(row => row.Kind == PlayerProtectionActiveTaskExecutionSummaryRowKind.TaskOperation && row.JavaCallReached),
			rowArray.Any(row => row.Kind == PlayerProtectionActiveTaskExecutionSummaryRowKind.PacketFanout && row.JavaCallReached),
			rowArray.Any(row => row.Kind == PlayerProtectionActiveTaskExecutionSummaryRowKind.AiMoveNotification && row.JavaCallReached),
			result.SentPackets,
			"PlayerController protection task bridge summary: PlayerController -> AttackUtil -> SM_PLAYER_STATE -> PacketSendUtility -> ThreadPoolManager/TaskId -> MovementNotifyTask",
			result.IsLive);
	}

	private static void AddTaskRows(
		ICollection<PlayerProtectionActiveTaskExecutionSummaryRow> rows,
		PlayerProtectionActiveTaskExecutionBridgeResult result,
		PlayerProtectionActiveTaskSideEffectOperationRow sideEffectRow)
	{
		var taskRows = result.TaskOperationPlan.Rows
			.Where(taskRow => MatchesTaskOperation(sideEffectRow.Operation, taskRow.Operation))
			.ToArray();

		if (taskRows.Length == 0)
		{
			AddSideEffect(rows, sideEffectRow, PlayerProtectionActiveTaskExecutionSummaryRowKind.TaskOperation, Status(sideEffectRow), Array.Empty<int>(), sideEffectRow.Notes);
			return;
		}

		foreach (var taskRow in taskRows)
		{
			Add(
				rows,
				PlayerProtectionActiveTaskExecutionSummaryRowKind.TaskOperation,
				TaskStatus(taskRow),
				taskRow.JavaCallReached,
				isLive: taskRow.IsLive,
				mutatedState: false,
				sentPackets: false,
				Array.Empty<int>(),
				taskRow.JavaOperation,
				taskRow.JavaSource,
				taskRow.Notes);
		}
	}

	private static bool MatchesTaskOperation(
		PlayerProtectionActiveTaskSideEffectOperation sideEffectOperation,
		PlayerProtectionActiveTaskTaskOperation taskOperation) =>
		(sideEffectOperation, taskOperation) switch
		{
			(PlayerProtectionActiveTaskSideEffectOperation.ScheduleDelayedStopTask, PlayerProtectionActiveTaskTaskOperation.ScheduleDelayedStop) => true,
			(PlayerProtectionActiveTaskSideEffectOperation.StoreProtectionTask, PlayerProtectionActiveTaskTaskOperation.AddTaskAndMaybeReplaceExisting) => true,
			(PlayerProtectionActiveTaskSideEffectOperation.CancelProtectionTask, PlayerProtectionActiveTaskTaskOperation.CancelTask) => true,
			_ => false,
		};

	private static void AddPacketConstruction(
		ICollection<PlayerProtectionActiveTaskExecutionSummaryRow> rows,
		PlayerProtectionActiveTaskExecutionBridgeResult result,
		PlayerProtectionActiveTaskSideEffectOperationRow sideEffectRow)
	{
		Add(
			rows,
			PlayerProtectionActiveTaskExecutionSummaryRowKind.PacketConstruction,
			result.ConstructedPacket
				? PlayerProtectionActiveTaskExecutionSummaryRowStatus.PlannedNotLive
				: PlayerProtectionActiveTaskExecutionSummaryRowStatus.SkippedBranch,
			result.ConstructedPacket,
			isLive: false,
			mutatedState: false,
			sentPackets: false,
			Array.Empty<int>(),
			"new SM_PLAYER_STATE(player)",
			sideEffectRow.JavaSource,
			result.ConstructedPacket
				? "C# constructs the concrete SM_PLAYER_STATE packet, but construction is still part of the disabled fanout boundary."
				: "Packet construction was skipped because Java does not broadcast in this branch.");
	}

	private static void AddPacketFanout(
		ICollection<PlayerProtectionActiveTaskExecutionSummaryRow> rows,
		PlayerProtectionActiveTaskExecutionBridgeResult result,
		PlayerProtectionActiveTaskSideEffectOperationRow sideEffectRow)
	{
		var recipientIds = result.SocketExecutorResult.Recipients
			.Select(recipient => recipient.Recipient.PlayerObjectId)
			.ToArray();
		var status = result.SocketExecutorResult.Status == PlayerProtectionActiveTaskSightedRecipientSocketExecutorStatus.Completed
			? PlayerProtectionActiveTaskExecutionSummaryRowStatus.LiveSocketExecution
			: result.SocketExecutorResult.Status == PlayerProtectionActiveTaskSightedRecipientSocketExecutorStatus.DisabledNoSend
				? PlayerProtectionActiveTaskExecutionSummaryRowStatus.DisabledNoSend
				: PlayerProtectionActiveTaskExecutionSummaryRowStatus.SkippedBranch;

		Add(
			rows,
			PlayerProtectionActiveTaskExecutionSummaryRowKind.PacketFanout,
			status,
			result.AdapterResult.FanoutPlan.ShouldBroadcast,
			result.SocketExecutorResult.IsLive,
			mutatedState: false,
			result.SentPackets,
			recipientIds,
			sideEffectRow.JavaOperation,
			result.SocketExecutorResult.JavaSource,
			result.SocketExecutorResult.Status == PlayerProtectionActiveTaskSightedRecipientSocketExecutorStatus.DisabledNoSend
				? "Sighted-recipient fanout is projected and concrete recipients are recorded, but the socket executor is disabled."
				: result.SocketExecutorResult.Status == PlayerProtectionActiveTaskSightedRecipientSocketExecutorStatus.NoPacket
					? "No packet was available because Java branch did not broadcast."
					: "Socket executor status records the current live fanout boundary.");
	}

	private static void AddSideEffect(
		ICollection<PlayerProtectionActiveTaskExecutionSummaryRow> rows,
		PlayerProtectionActiveTaskSideEffectOperationRow source,
		PlayerProtectionActiveTaskExecutionSummaryRowKind kind,
		PlayerProtectionActiveTaskExecutionSummaryRowStatus status,
		IReadOnlyList<int> relatedObjectIds,
		string notes) =>
		Add(
			rows,
			kind,
			status,
			source.JavaCallReached,
			source.IsLive,
			mutatedState: source.IsLive && kind == PlayerProtectionActiveTaskExecutionSummaryRowKind.VisualMutation,
			sentPackets: false,
			relatedObjectIds,
			source.JavaOperation,
			source.JavaSource,
			notes);

	private static PlayerProtectionActiveTaskExecutionSummaryRowStatus Status(
		PlayerProtectionActiveTaskSideEffectOperationRow row) =>
		row.Status switch
		{
			PlayerProtectionActiveTaskSideEffectOperationStatus.ObservedCondition => PlayerProtectionActiveTaskExecutionSummaryRowStatus.ObservedCondition,
			PlayerProtectionActiveTaskSideEffectOperationStatus.SkippedBranch => PlayerProtectionActiveTaskExecutionSummaryRowStatus.SkippedBranch,
			PlayerProtectionActiveTaskSideEffectOperationStatus.LiveVisualMutation => PlayerProtectionActiveTaskExecutionSummaryRowStatus.LiveVisualMutation,
			PlayerProtectionActiveTaskSideEffectOperationStatus.PlannedNotLive => PlayerProtectionActiveTaskExecutionSummaryRowStatus.PlannedNotLive,
			_ => throw new ArgumentOutOfRangeException(nameof(row.Status), row.Status, null),
		};

	private static PlayerProtectionActiveTaskExecutionSummaryRowStatus TaskStatus(
		PlayerProtectionActiveTaskTaskOperationRow row) =>
		row.Status switch
		{
			PlayerProtectionActiveTaskTaskOperationStatus.SkippedNoOpBranch => PlayerProtectionActiveTaskExecutionSummaryRowStatus.SkippedBranch,
			_ => PlayerProtectionActiveTaskExecutionSummaryRowStatus.PlannedNotLive,
		};

	private static void Add(
		ICollection<PlayerProtectionActiveTaskExecutionSummaryRow> rows,
		PlayerProtectionActiveTaskExecutionSummaryRowKind kind,
		PlayerProtectionActiveTaskExecutionSummaryRowStatus status,
		bool javaCallReached,
		bool isLive,
		bool mutatedState,
		bool sentPackets,
		IReadOnlyList<int> relatedObjectIds,
		string javaOperation,
		string javaSource,
		string notes)
	{
		rows.Add(new PlayerProtectionActiveTaskExecutionSummaryRow(
			rows.Count + 1,
			kind,
			status,
			javaCallReached,
			isLive,
			mutatedState,
			sentPackets,
			relatedObjectIds,
			javaOperation,
			javaSource,
			notes));
	}
}
