namespace Aion.GameServer.Services;

public enum PlayerProtectionActiveTaskTaskMapAuditArea
{
	TaskId,
	JavaSchedule,
	JavaTaskMapStorage,
	JavaTaskReplacement,
	JavaTaskCancel,
	JavaMissingTaskCancel,
	JavaConditionalCancel,
	JavaLifecycleCleanup,
	CSharpSchedulerHandle,
	CSharpTaskMapGap,
	ReadinessGate,
	ImplementationChecklist,
}

public enum PlayerProtectionActiveTaskTaskMapAuditStatus
{
	ObservedJavaBehavior,
	ExistingCSharpPrimitive,
	Gap,
	Requirement,
	Risk,
}

public sealed record PlayerProtectionActiveTaskTaskMapAuditRow(
	int Order,
	PlayerProtectionActiveTaskTaskMapAuditArea Area,
	PlayerProtectionActiveTaskTaskMapAuditStatus Status,
	string JavaArtifact,
	string CSharpArtifact,
	string JavaBehavior,
	string CSharpCurrentState,
	string Requirement,
	string JavaSource,
	string Notes);

public sealed record PlayerProtectionActiveTaskTaskMapAuditReport(
	IReadOnlyList<PlayerProtectionActiveTaskTaskMapAuditRow> Rows,
	bool HasLiveTaskMapAdapter,
	bool SchedulerCapabilityBlockedByReadiness,
	string JavaSource);

public static class PlayerProtectionActiveTaskTaskMapAuditService
{
	public static PlayerProtectionActiveTaskTaskMapAuditReport Create(
		PlayerProtectionActiveTaskLiveReadinessReport? readinessReport = null)
	{
		var rows = new List<PlayerProtectionActiveTaskTaskMapAuditRow>();

		Add(
			rows,
			PlayerProtectionActiveTaskTaskMapAuditArea.TaskId,
			PlayerProtectionActiveTaskTaskMapAuditStatus.ObservedJavaBehavior,
			"com.aionemu.gameserver.model.TaskId",
			"PlayerProtectionActiveTaskPlanService / task-operation metadata",
			"TaskId.PROTECTION_ACTIVE is ordinal 3 in the Java enum.",
			"C# protection plans preserve the name and ordinal as metadata.",
			"Future live task map must key protection tasks consistently with Java ordinal behavior or document an intentional adapter mapping.",
			"TaskId.java",
			"Do not assume other TaskId ordinals are safe until the full enum is represented.");
		Add(
			rows,
			PlayerProtectionActiveTaskTaskMapAuditArea.JavaSchedule,
			PlayerProtectionActiveTaskTaskMapAuditStatus.ObservedJavaBehavior,
			"com.aionemu.gameserver.controllers.PlayerController",
			"PlayerProtectionActiveTaskTaskOperationPlanService",
			"startProtectionActiveTask schedules this::stopProtectionActiveTask after 60000 milliseconds.",
			"C# records the 60000 ms delay but does not create a ScheduledTask for protection.",
			"Live adapter needs a ThreadPoolManager.Schedule callback that invokes stopProtectionActiveTask through the eventual controller boundary.",
			"PlayerController.startProtectionActiveTask -> ThreadPoolManager.getInstance().schedule(this::stopProtectionActiveTask, 60000)",
			"Callback threading and exception behavior remain unverified against Java runtime.");
		Add(
			rows,
			PlayerProtectionActiveTaskTaskMapAuditArea.JavaTaskMapStorage,
			PlayerProtectionActiveTaskTaskMapAuditStatus.ObservedJavaBehavior,
			"com.aionemu.gameserver.controllers.CreatureController",
			"missing protection task-map owner",
			"CreatureController stores tasks in a ConcurrentHashMap<Integer, Future<?>> keyed by taskId.ordinal().",
			"C# has no player/controller task map for protection active tasks.",
			"Define a controller-owned or player-owned concurrent map before live scheduling is enabled.",
			"CreatureController.tasks",
			"Ownership must align with future controller lifecycle cleanup.");
		Add(
			rows,
			PlayerProtectionActiveTaskTaskMapAuditArea.JavaTaskReplacement,
			PlayerProtectionActiveTaskTaskMapAuditStatus.ObservedJavaBehavior,
			"com.aionemu.gameserver.controllers.CreatureController",
			"PlayerProtectionActiveTaskTaskOperationPlanService",
			"addTask uses tasks.compute(taskId.ordinal(), ...) and cancels any old Future with cancel(false) before storing the new task.",
			"C# task-operation plan records replacement intent only.",
			"Live adapter must perform atomic replace-and-cancel so overlapping protection starts cannot leak old delayed-stop callbacks.",
			"CreatureController.addTask",
			"Java logs only for DESPAWN replacement; protection replacement has no warning side effect.");
		Add(
			rows,
			PlayerProtectionActiveTaskTaskMapAuditArea.JavaTaskCancel,
			PlayerProtectionActiveTaskTaskMapAuditStatus.ObservedJavaBehavior,
			"com.aionemu.gameserver.controllers.CreatureController",
			"PlayerProtectionActiveTaskTaskOperationPlanService",
			"cancelTask removes the Future, calls cancel(false) if present, and returns the removed task.",
			"C# task-operation plan records existing-task cancellation only.",
			"Live adapter must remove before cancel and expose missing/existing outcomes for tests.",
			"CreatureController.cancelTask",
			"Java cancel(false) does not interrupt a running task.");
		Add(
			rows,
			PlayerProtectionActiveTaskTaskMapAuditArea.JavaMissingTaskCancel,
			PlayerProtectionActiveTaskTaskMapAuditStatus.ObservedJavaBehavior,
			"com.aionemu.gameserver.controllers.CreatureController",
			"PlayerProtectionActiveTaskTaskOperationPlanService",
			"cancelTask returns null and cancels nothing when the task id is absent.",
			"C# task-operation plan records missing-task cancel as a no-op.",
			"Live adapter should preserve no-op missing cancel semantics without throwing.",
			"CreatureController.cancelTask",
			"Stop protection calls cancelTask before the spawned guard, so this no-op can occur for unspawned players too.");
		Add(
			rows,
			PlayerProtectionActiveTaskTaskMapAuditArea.JavaConditionalCancel,
			PlayerProtectionActiveTaskTaskMapAuditStatus.Risk,
			"com.aionemu.gameserver.controllers.CreatureController",
			"missing protection task-map owner",
			"cancelTaskIfPresent removes only if the stored Future instance matches and then calls cancel(false).",
			"No C# protection equivalent exists.",
			"Task-map adapter should decide whether protection needs conditional cancel support or document why stop protection only uses cancelTask.",
			"CreatureController.cancelTaskIfPresent",
			"This is a discovered dependency even though protection active task does not call it directly.");
		Add(
			rows,
			PlayerProtectionActiveTaskTaskMapAuditArea.JavaLifecycleCleanup,
			PlayerProtectionActiveTaskTaskMapAuditStatus.Requirement,
			"com.aionemu.gameserver.controllers.CreatureController",
			"missing protection task-map owner",
			"cancelAllTasks cancels every stored Future with cancel(false), clears the map, and is invoked by onDelete.",
			"No C# protection task-map lifecycle cleanup exists.",
			"Live task-map owner must define cleanup on player/controller deletion, logout, or world removal before scheduling is safe.",
			"CreatureController.cancelAllTasks / onDelete",
			"Lifecycle cleanup is required to avoid delayed callbacks after owner disposal.");
		Add(
			rows,
			PlayerProtectionActiveTaskTaskMapAuditArea.CSharpSchedulerHandle,
			PlayerProtectionActiveTaskTaskMapAuditStatus.ExistingCSharpPrimitive,
			"com.aionemu.gameserver.utils.ThreadPoolManager",
			"Aion.GameServer.Utils.ThreadPoolManager / ScheduledTask",
			"Java schedule returns ScheduledFuture<?>; Future.cancel(false) requests cancellation without interrupting running work.",
			"C# ThreadPoolManager.Schedule returns ScheduledTask with Cancel() backed by CancellationTokenSource.",
			"Adapter tests must prove C# Cancel() behavior is acceptable for Java cancel(false), including after completion.",
			"ThreadPoolManager.schedule / ScheduledFuture.cancel(false)",
			"C# cancellation is cooperative; Java non-interrupt cancel also cannot stop already-running work.");
		Add(
			rows,
			PlayerProtectionActiveTaskTaskMapAuditArea.CSharpTaskMapGap,
			PlayerProtectionActiveTaskTaskMapAuditStatus.Gap,
			"com.aionemu.gameserver.controllers.CreatureController",
			"missing protection controller task map",
			"Java exposes hasTask, hasScheduledTask, getAndRemoveTask, cancelTask, cancelTaskIfPresent, addTask, and cancelAllTasks.",
			"C# protection work currently has only non-live task-operation/readiness metadata.",
			"Implement a narrow task-map adapter only after owner lifecycle and concurrency boundaries are chosen.",
			"CreatureController task methods",
			"Do not wire production protection scheduling directly to ThreadPoolManager without the map adapter.");

		if (readinessReport?.BlockedCapabilities.Contains(PlayerProtectionActiveTaskLiveReadinessCapability.SchedulerTaskMap) == true)
		{
			Add(
				rows,
				PlayerProtectionActiveTaskTaskMapAuditArea.ReadinessGate,
				PlayerProtectionActiveTaskTaskMapAuditStatus.Gap,
				"com.aionemu.gameserver.controllers.PlayerController",
				"PlayerProtectionActiveTaskLiveReadinessService",
				"Protection start/stop reaches scheduler/task-map Java calls.",
				"Readiness report blocks SchedulerTaskMap for the current summary.",
				"Keep live scheduler/task-map execution disabled until this audit checklist is satisfied.",
				"PlayerController.startProtectionActiveTask / stopProtectionActiveTask",
				"Readiness linkage confirms the future adapter is still gated.");
		}

		Add(
			rows,
			PlayerProtectionActiveTaskTaskMapAuditArea.ImplementationChecklist,
			PlayerProtectionActiveTaskTaskMapAuditStatus.Requirement,
			"com.aionemu.gameserver.controllers.CreatureController",
			"future protection task-map adapter",
			"Java requires atomic replace, remove-before-cancel, missing no-op, optional conditional cancel, and lifecycle cancel-all semantics.",
			"No single C# adapter currently satisfies this full contract for players.",
			"Before live enablement: choose owner, key shape, locking/ConcurrentDictionary strategy, ScheduledTask handle storage, cleanup hook, and runtime parity tests.",
			"CreatureController.addTask/cancelTask/cancelAllTasks",
			"Checklist is intentionally conservative because scheduler races are runtime-sensitive.");

		return new PlayerProtectionActiveTaskTaskMapAuditReport(
			rows,
			HasLiveTaskMapAdapter: false,
			SchedulerCapabilityBlockedByReadiness: readinessReport?.BlockedCapabilities.Contains(PlayerProtectionActiveTaskLiveReadinessCapability.SchedulerTaskMap) == true,
			"CreatureController task map / PlayerController protection active task / ThreadPoolManager schedule audit");
	}

	private static void Add(
		ICollection<PlayerProtectionActiveTaskTaskMapAuditRow> rows,
		PlayerProtectionActiveTaskTaskMapAuditArea area,
		PlayerProtectionActiveTaskTaskMapAuditStatus status,
		string javaArtifact,
		string csharpArtifact,
		string javaBehavior,
		string csharpCurrentState,
		string requirement,
		string javaSource,
		string notes)
	{
		rows.Add(new PlayerProtectionActiveTaskTaskMapAuditRow(
			rows.Count + 1,
			area,
			status,
			javaArtifact,
			csharpArtifact,
			javaBehavior,
			csharpCurrentState,
			requirement,
			javaSource,
			notes));
	}
}
