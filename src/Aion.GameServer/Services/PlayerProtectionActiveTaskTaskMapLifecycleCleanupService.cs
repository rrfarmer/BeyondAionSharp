namespace Aion.GameServer.Services;

public sealed record PlayerProtectionActiveTaskTaskMapLifecycleCleanupRequest(
	IPlayerProtectionActiveTaskTaskHandle? PendingProtectionTaskHandle = null,
	IPlayerProtectionActiveTaskTaskHandle? ReplacementProtectionTaskHandle = null);

public enum PlayerProtectionActiveTaskTaskMapLifecycleCleanupRowKind
{
	ObserveLifecycleTrigger,
	SeedPendingProtectionTask,
	ReplacePendingProtectionTask,
	CancelAllTasks,
	ReportPrerequisite,
}

public sealed record PlayerProtectionActiveTaskTaskMapLifecycleCleanupRow(
	int Order,
	PlayerProtectionActiveTaskTaskMapLifecycleCleanupRowKind Kind,
	PlayerProtectionActiveTaskTaskMapOperationResult? AdapterResult,
	string JavaOperation,
	string JavaSource,
	string Notes);

public sealed record PlayerProtectionActiveTaskTaskMapLifecycleCleanupReport(
	IReadOnlyList<PlayerProtectionActiveTaskTaskMapLifecycleCleanupRow> Rows,
	PlayerProtectionActiveTaskTaskMapSnapshot FinalSnapshot,
	bool HadPendingProtectionTask,
	bool ReplacedPendingProtectionTask,
	bool CanceledTaskDuringReplacement,
	bool CanceledTaskDuringCleanup,
	int CleanupCanceledTaskCount,
	IReadOnlyList<string> RemainingPrerequisites,
	string JavaSource,
	bool IsLive);

public static class PlayerProtectionActiveTaskTaskMapLifecycleCleanupService
{
	private const int ProtectionActiveTaskIdOrdinal = 3;
	private const string ProtectionActiveTaskIdName = "PROTECTION_ACTIVE";

	public static PlayerProtectionActiveTaskTaskMapLifecycleCleanupReport Create(
		PlayerProtectionActiveTaskTaskMapLifecycleCleanupRequest request)
	{
		var adapter = new PlayerProtectionActiveTaskTaskMapAdapterService();
		var rows = new List<PlayerProtectionActiveTaskTaskMapLifecycleCleanupRow>();

		Add(
			rows,
			PlayerProtectionActiveTaskTaskMapLifecycleCleanupRowKind.ObserveLifecycleTrigger,
			adapterResult: null,
			"onDelete() -> cancelAllTasks()",
			"CreatureController.onDelete",
			"Java cancels all controller tasks during delete; C# protection lifecycle hook is not wired.");

		if (request.PendingProtectionTaskHandle != null)
		{
			var seed = adapter.AddTask(ProtectionActiveTaskIdOrdinal, ProtectionActiveTaskIdName, request.PendingProtectionTaskHandle);
			Add(
				rows,
				PlayerProtectionActiveTaskTaskMapLifecycleCleanupRowKind.SeedPendingProtectionTask,
				seed,
				"addTask(TaskId.PROTECTION_ACTIVE, future)",
				"CreatureController.addTask",
				"Seeds the non-live adapter with a pending protection task before lifecycle cleanup.");
		}

		if (request.ReplacementProtectionTaskHandle != null)
		{
			var replacement = adapter.AddTask(ProtectionActiveTaskIdOrdinal, ProtectionActiveTaskIdName, request.ReplacementProtectionTaskHandle);
			Add(
				rows,
				PlayerProtectionActiveTaskTaskMapLifecycleCleanupRowKind.ReplacePendingProtectionTask,
				replacement,
				"addTask(TaskId.PROTECTION_ACTIVE, replacementFuture)",
				"CreatureController.addTask",
				"Optional replacement simulates Java addTask canceling the previous future before cleanup.");
		}

		var cleanup = adapter.CancelAllTasks();
		Add(
			rows,
			PlayerProtectionActiveTaskTaskMapLifecycleCleanupRowKind.CancelAllTasks,
			cleanup,
			"cancelAllTasks()",
			"CreatureController.cancelAllTasks",
			"Lifecycle cleanup simulation cancels remaining task handles and clears the map.");

		foreach (var prerequisite in RemainingPrerequisites())
		{
			Add(
				rows,
				PlayerProtectionActiveTaskTaskMapLifecycleCleanupRowKind.ReportPrerequisite,
				adapterResult: null,
				"future C# lifecycle integration",
				"CreatureController.onDelete / player logout-delete lifecycle",
				prerequisite);
		}

		var rowArray = rows.ToArray();
		return new PlayerProtectionActiveTaskTaskMapLifecycleCleanupReport(
			rowArray,
			adapter.CreateSnapshot(),
			HadPendingProtectionTask: request.PendingProtectionTaskHandle != null,
			ReplacedPendingProtectionTask: request.ReplacementProtectionTaskHandle != null,
			CanceledTaskDuringReplacement: rowArray.Any(row => row.Kind == PlayerProtectionActiveTaskTaskMapLifecycleCleanupRowKind.ReplacePendingProtectionTask && row.AdapterResult?.CanceledTask == true),
			CanceledTaskDuringCleanup: cleanup.CanceledTask,
			cleanup.CanceledTaskCount,
			RemainingPrerequisites().ToArray(),
			"CreatureController.onDelete -> cancelAllTasks protection task-map lifecycle cleanup plan",
			IsLive: false);
	}

	private static IEnumerable<string> RemainingPrerequisites()
	{
		yield return "Choose the production C# owner for the protection task map.";
		yield return "Wire cleanup to the future player/controller delete or logout lifecycle.";
		yield return "Runtime-compare Java cancelAllTasks and Future.cancel(false) race behavior before live enablement.";
	}

	private static void Add(
		ICollection<PlayerProtectionActiveTaskTaskMapLifecycleCleanupRow> rows,
		PlayerProtectionActiveTaskTaskMapLifecycleCleanupRowKind kind,
		PlayerProtectionActiveTaskTaskMapOperationResult? adapterResult,
		string javaOperation,
		string javaSource,
		string notes)
	{
		rows.Add(new PlayerProtectionActiveTaskTaskMapLifecycleCleanupRow(
			rows.Count + 1,
			kind,
			adapterResult,
			javaOperation,
			javaSource,
			notes));
	}
}
