namespace Aion.GameServer.Services;

public enum PlayerProtectionActiveTaskLiveReadinessCapability
{
	BranchObservation,
	VisualMutation,
	CastCancellation,
	TargetClear,
	PacketConstruction,
	PacketFanout,
	SchedulerTaskMap,
	AiMoveNotification,
}

public enum PlayerProtectionActiveTaskLiveReadinessStatus
{
	Ready,
	Blocked,
	NotReached,
	SkippedBranch,
	LiveOnlyAllowed,
}

public sealed record PlayerProtectionActiveTaskLiveReadinessRow(
	int Order,
	PlayerProtectionActiveTaskLiveReadinessCapability Capability,
	PlayerProtectionActiveTaskLiveReadinessStatus Status,
	bool JavaCallReached,
	bool IsCurrentlyLive,
	bool BlocksAdditionalLiveSideEffects,
	IReadOnlyList<string> BlockedReasons,
	string JavaOperation,
	string JavaSource,
	string Notes
);

public sealed record PlayerProtectionActiveTaskLiveReadinessReport(
	PlayerProtectionActiveTaskAdapterAction Action,
	PlayerProtectionActiveTaskAdapterStatus AdapterStatus,
	int PlayerObjectId,
	IReadOnlyList<PlayerProtectionActiveTaskLiveReadinessRow> Rows,
	bool CanEnableAdditionalLiveSideEffects,
	IReadOnlyList<PlayerProtectionActiveTaskLiveReadinessCapability> BlockedCapabilities,
	string JavaSource
);

public static class PlayerProtectionActiveTaskLiveReadinessService
{
	public static PlayerProtectionActiveTaskLiveReadinessReport Create(PlayerProtectionActiveTaskExecutionSummary summary)
	{
		// Java parity: protection-task start/stop has multiple live side-effect seams beyond the currently
		// allowed visual mutation. This readiness pass turns execution-summary rows into capability gates
		// for packet fanout, scheduler/task-map wiring, AttackUtil helpers, and AI move notification.
		var rows = summary.Rows.Select(CreateRow).ToArray();
		var blockedCapabilities = rows.Where(row => row.BlocksAdditionalLiveSideEffects).Select(row => row.Capability).Distinct().ToArray();

		return new PlayerProtectionActiveTaskLiveReadinessReport(
			summary.Action,
			summary.AdapterStatus,
			summary.PlayerObjectId,
			rows,
			CanEnableAdditionalLiveSideEffects: blockedCapabilities.Length == 0,
			blockedCapabilities,
			"PlayerController protection active task live-readiness gate over summary rows"
		);
	}

	private static PlayerProtectionActiveTaskLiveReadinessRow CreateRow(PlayerProtectionActiveTaskExecutionSummaryRow row)
	{
		var capability = Capability(row.Kind);
		var blockedReasons = BlockedReasons(row, capability).ToArray();
		var status = Status(row, capability, blockedReasons.Length);
		var blocks = status == PlayerProtectionActiveTaskLiveReadinessStatus.Blocked;

		return new PlayerProtectionActiveTaskLiveReadinessRow(
			row.Order,
			capability,
			status,
			row.JavaCallReached,
			row.IsLive,
			blocks,
			blockedReasons,
			row.JavaOperation,
			row.JavaSource,
			Notes(row, capability, status)
		);
	}

	private static PlayerProtectionActiveTaskLiveReadinessCapability Capability(PlayerProtectionActiveTaskExecutionSummaryRowKind kind) =>
		kind switch
		{
			PlayerProtectionActiveTaskExecutionSummaryRowKind.ObservedCondition => PlayerProtectionActiveTaskLiveReadinessCapability.BranchObservation,
			PlayerProtectionActiveTaskExecutionSummaryRowKind.VisualMutation => PlayerProtectionActiveTaskLiveReadinessCapability.VisualMutation,
			PlayerProtectionActiveTaskExecutionSummaryRowKind.AttackUtilCastCancellation =>
				PlayerProtectionActiveTaskLiveReadinessCapability.CastCancellation,
			PlayerProtectionActiveTaskExecutionSummaryRowKind.AttackUtilTargetClear => PlayerProtectionActiveTaskLiveReadinessCapability.TargetClear,
			PlayerProtectionActiveTaskExecutionSummaryRowKind.PacketConstruction => PlayerProtectionActiveTaskLiveReadinessCapability.PacketConstruction,
			PlayerProtectionActiveTaskExecutionSummaryRowKind.PacketFanout => PlayerProtectionActiveTaskLiveReadinessCapability.PacketFanout,
			PlayerProtectionActiveTaskExecutionSummaryRowKind.TaskOperation => PlayerProtectionActiveTaskLiveReadinessCapability.SchedulerTaskMap,
			PlayerProtectionActiveTaskExecutionSummaryRowKind.AiMoveNotification => PlayerProtectionActiveTaskLiveReadinessCapability.AiMoveNotification,
			PlayerProtectionActiveTaskExecutionSummaryRowKind.SkippedBranch => PlayerProtectionActiveTaskLiveReadinessCapability.AiMoveNotification,
			_ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
		};

	private static IEnumerable<string> BlockedReasons(
		PlayerProtectionActiveTaskExecutionSummaryRow row,
		PlayerProtectionActiveTaskLiveReadinessCapability capability
	)
	{
		if (!row.JavaCallReached)
			yield break;

		switch (capability)
		{
			case PlayerProtectionActiveTaskLiveReadinessCapability.CastCancellation:
				yield return "Live known-list creature facts are not generated from Player.getKnownList().forEachObject.";
				yield return "Live CreatureController.cancelCurrentSkill(null) integration is disabled.";
				break;
			case PlayerProtectionActiveTaskLiveReadinessCapability.TargetClear:
				yield return "Live known-list player facts are not generated from KnownList.forEachPlayer.";
				yield return "Live Player.setTarget(null) mutation is disabled.";
				break;
			case PlayerProtectionActiveTaskLiveReadinessCapability.PacketFanout:
				if (!row.SentPackets)
					yield return "Live PacketSendUtility.broadcastToSightedPlayers fanout is disabled by the socket executor gate.";
				yield return "Protection SM_PLAYER_STATE Java byte/runtime comparison has not been generated.";
				break;
			case PlayerProtectionActiveTaskLiveReadinessCapability.SchedulerTaskMap:
				yield return "Live ThreadPoolManager.schedule and controller task-map storage/cancel are not wired for protection active tasks.";
				yield return "Java Future.cancel(false) replacement/cancel race behavior has not been runtime-compared.";
				break;
			case PlayerProtectionActiveTaskLiveReadinessCapability.AiMoveNotification:
				if (row.Status == PlayerProtectionActiveTaskExecutionSummaryRowStatus.SkippedBranch)
					yield break;
				yield return "Live MovementNotifyTask.add(owner) integration is not wired.";
				break;
		}
	}

	private static PlayerProtectionActiveTaskLiveReadinessStatus Status(
		PlayerProtectionActiveTaskExecutionSummaryRow row,
		PlayerProtectionActiveTaskLiveReadinessCapability capability,
		int blockedReasonCount
	)
	{
		if (!row.JavaCallReached)
			return PlayerProtectionActiveTaskLiveReadinessStatus.NotReached;
		if (row.Status == PlayerProtectionActiveTaskExecutionSummaryRowStatus.SkippedBranch)
			return PlayerProtectionActiveTaskLiveReadinessStatus.SkippedBranch;
		if (blockedReasonCount > 0)
			return PlayerProtectionActiveTaskLiveReadinessStatus.Blocked;
		if (capability == PlayerProtectionActiveTaskLiveReadinessCapability.VisualMutation && row.IsLive)
			return PlayerProtectionActiveTaskLiveReadinessStatus.LiveOnlyAllowed;

		return PlayerProtectionActiveTaskLiveReadinessStatus.Ready;
	}

	private static string Notes(
		PlayerProtectionActiveTaskExecutionSummaryRow row,
		PlayerProtectionActiveTaskLiveReadinessCapability capability,
		PlayerProtectionActiveTaskLiveReadinessStatus status
	) =>
		status switch
		{
			PlayerProtectionActiveTaskLiveReadinessStatus.Blocked =>
				"Additional live side effects must stay disabled until the listed prerequisites are implemented and verified.",
			PlayerProtectionActiveTaskLiveReadinessStatus.LiveOnlyAllowed =>
				"Visual-state mutation is the only currently allowed live protection side effect.",
			PlayerProtectionActiveTaskLiveReadinessStatus.SkippedBranch => "Java reaches the guard but skips this capability for the current state.",
			PlayerProtectionActiveTaskLiveReadinessStatus.NotReached => "Java branch did not reach this capability for the current state.",
			_ => capability == PlayerProtectionActiveTaskLiveReadinessCapability.PacketConstruction
				? "Concrete packet construction is available, but packet fanout readiness is evaluated separately."
				: row.Notes,
		};
}
