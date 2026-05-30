namespace Aion.GameServer.Services;

public enum PlayerProtectionActiveTaskTaskMapOwnerOption
{
	ControllerOwned,
	PlayerModelOwned,
	ExternalServiceOwned,
}

public enum PlayerProtectionActiveTaskTaskMapOwnerSelectionArea
{
	JavaTaskStorage,
	HasTask,
	HasScheduledTask,
	GetAndRemoveTask,
	CancelTask,
	CancelTaskIfPresent,
	AddTask,
	CancelAllTasks,
	OnDeleteCleanup,
	ControllerOwnedCandidate,
	PlayerModelOwnedCandidate,
	ExternalServiceOwnedCandidate,
	Recommendation,
	LiveEnablementBlocker,
}

public enum PlayerProtectionActiveTaskTaskMapOwnerSelectionStatus
{
	JavaRequirement,
	PreferredCandidate,
	RejectedCandidate,
	Blocked,
	NeedsVerification,
}

public sealed record PlayerProtectionActiveTaskTaskMapOwnerSelectionRequest(
	bool HasConcreteCSharpControllerTaskMapOwner = false,
	bool HasPlayerModelStorageCandidate = true,
	bool HasExternalServiceStorageCandidate = true
);

public sealed record PlayerProtectionActiveTaskTaskMapOwnerSelectionRow(
	int Order,
	PlayerProtectionActiveTaskTaskMapOwnerSelectionArea Area,
	PlayerProtectionActiveTaskTaskMapOwnerSelectionStatus Status,
	PlayerProtectionActiveTaskTaskMapOwnerOption? Candidate,
	bool BlocksLiveEnablement,
	string JavaOperation,
	string JavaSource,
	string CSharpImplication,
	string Notes
);

public sealed record PlayerProtectionActiveTaskTaskMapOwnerSelectionReport(
	IReadOnlyList<PlayerProtectionActiveTaskTaskMapOwnerSelectionRow> Rows,
	PlayerProtectionActiveTaskTaskMapOwnerOption RecommendedOwner,
	bool CanWireProductionScheduling,
	bool HasConcreteCSharpControllerTaskMapOwner,
	bool RequiresLifecycleCleanupHook,
	bool RequiresRuntimeConcurrencyComparison,
	string JavaSource,
	bool IsLive
);

public static class PlayerProtectionActiveTaskTaskMapOwnerSelectionService
{
	public static PlayerProtectionActiveTaskTaskMapOwnerSelectionReport Create(PlayerProtectionActiveTaskTaskMapOwnerSelectionRequest request)
	{
		// Java parity: CreatureController owns the task map that protection scheduling uses. This service
		// compares possible C# owners against that Java lifecycle contract and records why live wiring is
		// still blocked.
		var rows = new List<PlayerProtectionActiveTaskTaskMapOwnerSelectionRow>();

		AddJavaRequirements(rows);
		AddCandidates(rows, request);
		AddRecommendation(rows, request);

		return new PlayerProtectionActiveTaskTaskMapOwnerSelectionReport(
			rows.ToArray(),
			PlayerProtectionActiveTaskTaskMapOwnerOption.ControllerOwned,
			CanWireProductionScheduling: false,
			request.HasConcreteCSharpControllerTaskMapOwner,
			RequiresLifecycleCleanupHook: true,
			RequiresRuntimeConcurrencyComparison: true,
			"CreatureController.tasks owner selection for PlayerController protection active task",
			IsLive: false
		);
	}

	private static void AddJavaRequirements(ICollection<PlayerProtectionActiveTaskTaskMapOwnerSelectionRow> rows)
	{
		Add(
			rows,
			PlayerProtectionActiveTaskTaskMapOwnerSelectionArea.JavaTaskStorage,
			PlayerProtectionActiveTaskTaskMapOwnerSelectionStatus.JavaRequirement,
			candidate: null,
			blocksLiveEnablement: false,
			"private final ConcurrentHashMap<Integer, Future<?>> tasks = new ConcurrentHashMap<>()",
			"CreatureController.tasks",
			"C# owner should be per creature/controller, not a process-global protection-only store.",
			"Java stores all controller tasks by TaskId ordinal on the controller instance."
		);
		Add(
			rows,
			PlayerProtectionActiveTaskTaskMapOwnerSelectionArea.HasTask,
			PlayerProtectionActiveTaskTaskMapOwnerSelectionStatus.JavaRequirement,
			candidate: null,
			blocksLiveEnablement: false,
			"tasks.containsKey(taskId.ordinal())",
			"CreatureController.hasTask",
			"C# owner must distinguish stored completed tasks from absent tasks.",
			"Presence check does not inspect Future.isDone."
		);
		Add(
			rows,
			PlayerProtectionActiveTaskTaskMapOwnerSelectionArea.HasScheduledTask,
			PlayerProtectionActiveTaskTaskMapOwnerSelectionStatus.JavaRequirement,
			candidate: null,
			blocksLiveEnablement: false,
			"task != null && !task.isDone()",
			"CreatureController.hasScheduledTask",
			"C# owner must preserve a separate not-done check for scheduled handles.",
			"Completion semantics require Java/C# runtime comparison before live enablement."
		);
		Add(
			rows,
			PlayerProtectionActiveTaskTaskMapOwnerSelectionArea.GetAndRemoveTask,
			PlayerProtectionActiveTaskTaskMapOwnerSelectionStatus.JavaRequirement,
			candidate: null,
			blocksLiveEnablement: false,
			"tasks.remove(taskId.ordinal())",
			"CreatureController.getAndRemoveTask",
			"C# owner must expose remove-before-cancel ordering.",
			"Removed task handle is returned to callers in Java."
		);
		Add(
			rows,
			PlayerProtectionActiveTaskTaskMapOwnerSelectionArea.CancelTask,
			PlayerProtectionActiveTaskTaskMapOwnerSelectionStatus.JavaRequirement,
			candidate: null,
			blocksLiveEnablement: false,
			"Future<?> task = getAndRemoveTask(taskId); if (task != null) task.cancel(false)",
			"CreatureController.cancelTask",
			"C# owner must cancel after removal and no-op when absent.",
			"Protection stop calls this before spawned-state checks."
		);
		Add(
			rows,
			PlayerProtectionActiveTaskTaskMapOwnerSelectionArea.CancelTaskIfPresent,
			PlayerProtectionActiveTaskTaskMapOwnerSelectionStatus.JavaRequirement,
			candidate: null,
			blocksLiveEnablement: false,
			"tasks.remove(taskId.ordinal(), task) then task.cancel(false)",
			"CreatureController.cancelTaskIfPresent",
			"C# owner should define object-identity conditional removal even if protection does not call it yet.",
			"Discovered dependency from the shared task-map contract."
		);
		Add(
			rows,
			PlayerProtectionActiveTaskTaskMapOwnerSelectionArea.AddTask,
			PlayerProtectionActiveTaskTaskMapOwnerSelectionStatus.JavaRequirement,
			candidate: null,
			blocksLiveEnablement: false,
			"tasks.compute(taskId.ordinal(), ...) cancels old Future before returning the new task",
			"CreatureController.addTask",
			"C# owner must make replace-and-cancel atomic enough to avoid leaked delayed-stop callbacks.",
			"Java only logs DESPAWN replacement, not PROTECTION_ACTIVE replacement."
		);
		Add(
			rows,
			PlayerProtectionActiveTaskTaskMapOwnerSelectionArea.CancelAllTasks,
			PlayerProtectionActiveTaskTaskMapOwnerSelectionStatus.JavaRequirement,
			candidate: null,
			blocksLiveEnablement: false,
			"for each task cancel(false); tasks.clear()",
			"CreatureController.cancelAllTasks",
			"C# owner must cancel every stored handle and clear the map.",
			"Iteration/clear race behavior remains a runtime comparison blocker."
		);
		Add(
			rows,
			PlayerProtectionActiveTaskTaskMapOwnerSelectionArea.OnDeleteCleanup,
			PlayerProtectionActiveTaskTaskMapOwnerSelectionStatus.JavaRequirement,
			candidate: null,
			blocksLiveEnablement: false,
			"onDelete() -> cancelAllTasks(); super.onDelete()",
			"CreatureController.onDelete",
			"C# owner must be reachable from future delete/logout/world-removal lifecycle cleanup.",
			"Lifecycle cleanup is the main reason an external orphaned store is risky."
		);
	}

	private static void AddCandidates(
		ICollection<PlayerProtectionActiveTaskTaskMapOwnerSelectionRow> rows,
		PlayerProtectionActiveTaskTaskMapOwnerSelectionRequest request
	)
	{
		Add(
			rows,
			PlayerProtectionActiveTaskTaskMapOwnerSelectionArea.ControllerOwnedCandidate,
			request.HasConcreteCSharpControllerTaskMapOwner
				? PlayerProtectionActiveTaskTaskMapOwnerSelectionStatus.PreferredCandidate
				: PlayerProtectionActiveTaskTaskMapOwnerSelectionStatus.Blocked,
			PlayerProtectionActiveTaskTaskMapOwnerOption.ControllerOwned,
			blocksLiveEnablement: !request.HasConcreteCSharpControllerTaskMapOwner,
			"CreatureController owns tasks and onDelete cleanup",
			"CreatureController.tasks / onDelete",
			"Preferred parity target: attach task-map ownership to the eventual C# controller/creature lifecycle boundary.",
			request.HasConcreteCSharpControllerTaskMapOwner
				? "Controller-owned storage best matches Java lifecycle and task contract."
				: "No concrete C# controller task-map owner exists yet, so live scheduling must remain blocked."
		);
		Add(
			rows,
			PlayerProtectionActiveTaskTaskMapOwnerSelectionArea.PlayerModelOwnedCandidate,
			PlayerProtectionActiveTaskTaskMapOwnerSelectionStatus.RejectedCandidate,
			PlayerProtectionActiveTaskTaskMapOwnerOption.PlayerModelOwned,
			blocksLiveEnablement: request.HasPlayerModelStorageCandidate,
			"PlayerController delegates task storage to inherited CreatureController methods",
			"PlayerController.startProtectionActiveTask / CreatureController.addTask",
			"Player model storage would place scheduler handles on gameplay state rather than controller lifecycle.",
			"Rejected for now because it risks persistence/model leakage and does not naturally cover non-player CreatureController task parity."
		);
		Add(
			rows,
			PlayerProtectionActiveTaskTaskMapOwnerSelectionArea.ExternalServiceOwnedCandidate,
			PlayerProtectionActiveTaskTaskMapOwnerSelectionStatus.RejectedCandidate,
			PlayerProtectionActiveTaskTaskMapOwnerOption.ExternalServiceOwned,
			blocksLiveEnablement: request.HasExternalServiceStorageCandidate,
			"CreatureController methods are instance methods, not a global task registry",
			"CreatureController task methods",
			"External service storage can work for narrow staged adapters but must still hook owner deletion exactly once.",
			"Rejected as the default protection owner because orphan cleanup and object-id reuse risks are higher than controller-local ownership."
		);
	}

	private static void AddRecommendation(
		ICollection<PlayerProtectionActiveTaskTaskMapOwnerSelectionRow> rows,
		PlayerProtectionActiveTaskTaskMapOwnerSelectionRequest request
	)
	{
		Add(
			rows,
			PlayerProtectionActiveTaskTaskMapOwnerSelectionArea.Recommendation,
			PlayerProtectionActiveTaskTaskMapOwnerSelectionStatus.Blocked,
			PlayerProtectionActiveTaskTaskMapOwnerOption.ControllerOwned,
			blocksLiveEnablement: true,
			"preserve CreatureController.tasks semantics before enabling PlayerController protection scheduling",
			"CreatureController.tasks / PlayerController.startProtectionActiveTask",
			"Implement controller-owned task storage first, then adapt `ScheduledTask` handles through the existing wrapper.",
			"Recommendation is non-live and conservative until controller lifecycle ownership exists."
		);
		Add(
			rows,
			PlayerProtectionActiveTaskTaskMapOwnerSelectionArea.LiveEnablementBlocker,
			PlayerProtectionActiveTaskTaskMapOwnerSelectionStatus.NeedsVerification,
			PlayerProtectionActiveTaskTaskMapOwnerOption.ControllerOwned,
			blocksLiveEnablement: true,
			"ConcurrentHashMap.compute/remove/cancelAllTasks and Future.cancel(false)",
			"CreatureController.addTask/cancelTask/cancelAllTasks",
			"Runtime-compare Java and C# replacement/cancel/cleanup race behavior before live scheduling.",
			request.HasConcreteCSharpControllerTaskMapOwner
				? "Even with an owner, Java runtime comparison remains required."
				: "Owner selection plus runtime comparison both remain blockers."
		);
	}

	private static void Add(
		ICollection<PlayerProtectionActiveTaskTaskMapOwnerSelectionRow> rows,
		PlayerProtectionActiveTaskTaskMapOwnerSelectionArea area,
		PlayerProtectionActiveTaskTaskMapOwnerSelectionStatus status,
		PlayerProtectionActiveTaskTaskMapOwnerOption? candidate,
		bool blocksLiveEnablement,
		string javaOperation,
		string javaSource,
		string csharpImplication,
		string notes
	)
	{
		rows.Add(
			new PlayerProtectionActiveTaskTaskMapOwnerSelectionRow(
				rows.Count + 1,
				area,
				status,
				candidate,
				blocksLiveEnablement,
				javaOperation,
				javaSource,
				csharpImplication,
				notes
			)
		);
	}
}
