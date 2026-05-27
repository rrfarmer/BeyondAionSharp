namespace Aion.GameServer.Services;

public enum PlayerProtectionActiveTaskReportRowKind
{
	PlannedMetadata,
	LiveStateBoundary,
	UnsupportedSideEffect,
	PacketIntent,
	SchedulerIntent,
	SkippedBranch,
}

public sealed record PlayerProtectionActiveTaskReportRow(
	int Order,
	string JavaArtifact,
	string JavaOperation,
	PlayerProtectionActiveTaskReportRowKind Kind,
	bool IsLive,
	string Notes);

public sealed record PlayerProtectionActiveTaskReport(
	PlayerProtectionActiveTaskAdapterStatus Status,
	int PlayerObjectId,
	IReadOnlyList<PlayerProtectionActiveTaskReportRow> Rows,
	string JavaSource,
	bool IsLive);

public static class PlayerProtectionActiveTaskReportService
{
	public static PlayerProtectionActiveTaskReport CreateReport(PlayerProtectionActiveTaskAdapterResult result)
		=> CreateReport(
			result.Status,
			result.Plan,
			result.FanoutPlan,
			result.MutatedVisualState,
			result.IsLive);

	public static PlayerProtectionActiveTaskReport CreateReport(
		PlayerProtectionActiveTaskAdapterStatus status,
		PlayerProtectionActiveTaskPlan plan,
		PlayerProtectionActiveTaskFanoutPlan fanoutPlan,
		bool mutatedVisualState,
		bool isLive)
	{
		var rows = new List<PlayerProtectionActiveTaskReportRow>();
		foreach (var step in plan.Steps)
		{
			AddStep(rows, plan, fanoutPlan, mutatedVisualState, step);
		}

		if (plan.Status == PlayerProtectionActiveTaskPlanStatus.AlreadyProtected)
		{
			Add(
				rows,
				"PlayerController",
				"return because player is already protection active",
				PlayerProtectionActiveTaskReportRowKind.SkippedBranch,
				isLive: false,
				"Java does not set BLINKING, schedule a task, or send SM_PLAYER_STATE in this branch.");
		}
		else if (plan.Status == PlayerProtectionActiveTaskPlanStatus.StopProtectionUnspawned)
		{
			Add(
				rows,
				"PlayerController",
				"skip spawned-only visual state, SM_PLAYER_STATE fanout, and notifyAIOnMove",
				PlayerProtectionActiveTaskReportRowKind.SkippedBranch,
				isLive: false,
				"Java only executes these operations inside if (player.isSpawned()).");
		}

		return new PlayerProtectionActiveTaskReport(
			status,
			plan.PlayerObjectId,
			rows,
			"com.aionemu.gameserver.controllers.PlayerController.startProtectionActiveTask / stopProtectionActiveTask",
			isLive);
	}

	private static void AddStep(
		ICollection<PlayerProtectionActiveTaskReportRow> rows,
		PlayerProtectionActiveTaskPlan plan,
		PlayerProtectionActiveTaskFanoutPlan fanoutPlan,
		bool mutatedVisualState,
		PlayerProtectionActiveTaskPlanStep step)
	{
		switch (step)
		{
			case PlayerProtectionActiveTaskPlanStep.CheckProtectionActive:
				Add(rows, "PlayerController", "if (!getOwner().isProtectionActive())", PlayerProtectionActiveTaskReportRowKind.PlannedMetadata, false, "Branch is derived from current BLINKING visual state.");
				break;
			case PlayerProtectionActiveTaskPlanStep.SetBlinkingVisualState:
				Add(rows, "Player", "setVisualState(CreatureVisualState.BLINKING)", PlayerProtectionActiveTaskReportRowKind.LiveStateBoundary, mutatedVisualState, "Only this visual-state mutation may be executed by the opt-in adapter.");
				break;
			case PlayerProtectionActiveTaskPlanStep.CancelCastOnPlayer:
				Add(rows, "AttackUtil", "cancelCastOn(getOwner())", PlayerProtectionActiveTaskReportRowKind.UnsupportedSideEffect, false, "Live cast cancellation is not executed.");
				break;
			case PlayerProtectionActiveTaskPlanStep.RemovePlayerFromTargets:
				Add(rows, "AttackUtil", "removeTargetFrom(getOwner())", PlayerProtectionActiveTaskReportRowKind.UnsupportedSideEffect, false, "Live attacker target cleanup is not executed.");
				break;
			case PlayerProtectionActiveTaskPlanStep.BroadcastPlayerState:
				AddBroadcast(rows, fanoutPlan);
				break;
			case PlayerProtectionActiveTaskPlanStep.ScheduleProtectionActiveTask:
				Add(rows, "ThreadPoolManager", "schedule(this::stopProtectionActiveTask, 60000)", PlayerProtectionActiveTaskReportRowKind.SchedulerIntent, false, "Live scheduler mutation is disabled.");
				break;
			case PlayerProtectionActiveTaskPlanStep.StoreProtectionActiveTask:
				Add(rows, "CreatureController", "addTask(TaskId.PROTECTION_ACTIVE, future)", PlayerProtectionActiveTaskReportRowKind.SchedulerIntent, false, "Task storage is metadata only.");
				break;
			case PlayerProtectionActiveTaskPlanStep.CancelProtectionActiveTask:
				Add(rows, "CreatureController", "cancelTask(TaskId.PROTECTION_ACTIVE)", PlayerProtectionActiveTaskReportRowKind.SchedulerIntent, false, plan.ShouldCancelTask ? "Represented task cancellation is planned only." : "Java still calls cancelTask; current C# inputs represent no stored task.");
				break;
			case PlayerProtectionActiveTaskPlanStep.UnsetBlinkingVisualState:
				Add(rows, "Player", "unsetVisualState(CreatureVisualState.BLINKING)", PlayerProtectionActiveTaskReportRowKind.LiveStateBoundary, mutatedVisualState, "Only this visual-state mutation may be executed by the opt-in adapter.");
				break;
			case PlayerProtectionActiveTaskPlanStep.NotifyAiOnMove:
				Add(rows, "PlayerController", "notifyAIOnMove()", PlayerProtectionActiveTaskReportRowKind.UnsupportedSideEffect, false, "Live AI movement notification is not executed.");
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(step), step, null);
		}
	}

	private static void AddBroadcast(
		ICollection<PlayerProtectionActiveTaskReportRow> rows,
		PlayerProtectionActiveTaskFanoutPlan fanoutPlan)
	{
		Add(
			rows,
			"SM_PLAYER_STATE",
			"new SM_PLAYER_STATE(player)",
			PlayerProtectionActiveTaskReportRowKind.PacketIntent,
			isLive: false,
			fanoutPlan.PacketOpCode is { } opcode
				? $"Packet construction is planned after visual-state mutation; opcode {opcode}."
				: "Packet construction is skipped for this branch.");
		Add(
			rows,
			"PacketSendUtility",
			"broadcastToSightedPlayers(player, packet, true)",
			PlayerProtectionActiveTaskReportRowKind.PacketIntent,
			isLive: false,
			fanoutPlan.ShouldBroadcast
				? fanoutPlan.RecipientSelection
				: "No broadcast recipients because Java does not call PacketSendUtility in this branch.");
	}

	private static void Add(
		ICollection<PlayerProtectionActiveTaskReportRow> rows,
		string javaArtifact,
		string javaOperation,
		PlayerProtectionActiveTaskReportRowKind kind,
		bool isLive,
		string notes)
	{
		rows.Add(new PlayerProtectionActiveTaskReportRow(
			rows.Count + 1,
			javaArtifact,
			javaOperation,
			kind,
			isLive,
			notes));
	}
}
