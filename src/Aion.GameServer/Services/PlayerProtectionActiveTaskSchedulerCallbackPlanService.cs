namespace Aion.GameServer.Services;

public enum PlayerProtectionActiveTaskSchedulerCallbackPlanStatus
{
	PlannedNotLive,
	SkippedAlreadyProtected,
	BlockedMissingOwnerPrototype,
}

public enum PlayerProtectionActiveTaskSchedulerCallbackPlanRowKind
{
	ObserveStartBranch,
	RequireOwnerPrototype,
	RecordScheduleCall,
	RecordCallbackTarget,
	RecordTaskMapStorage,
	RecordRuntimeBlocker,
}

public sealed record PlayerProtectionActiveTaskSchedulerCallbackPlanRequest(
	PlayerProtectionActiveTaskPlan Plan,
	PlayerProtectionActiveTaskControllerTaskMapOwnerPrototypeSnapshot? OwnerPrototypeSnapshot = null
);

public sealed record PlayerProtectionActiveTaskSchedulerCallbackPlanRow(
	int Order,
	PlayerProtectionActiveTaskSchedulerCallbackPlanRowKind Kind,
	PlayerProtectionActiveTaskSchedulerCallbackPlanStatus Status,
	bool JavaCallReached,
	bool IsLive,
	string JavaOperation,
	string JavaSource,
	string Notes
);

public sealed record PlayerProtectionActiveTaskSchedulerCallbackPlan(
	PlayerProtectionActiveTaskSchedulerCallbackPlanStatus Status,
	int PlayerObjectId,
	int DelayMilliseconds,
	IReadOnlyList<PlayerProtectionActiveTaskSchedulerCallbackPlanRow> Rows,
	bool SchedulesDelayedStop,
	bool StoresScheduledFuture,
	bool InvokesScheduler,
	bool InvokesCallback,
	bool HasOwnerPrototypeEvidence,
	string JavaSource,
	bool IsLive
);

public static class PlayerProtectionActiveTaskSchedulerCallbackPlanService
{
	public const int ProtectionActiveDelayMilliseconds = 60000;

	public static PlayerProtectionActiveTaskSchedulerCallbackPlan Create(PlayerProtectionActiveTaskSchedulerCallbackPlanRequest request)
	{
		// Java parity: PlayerController.startProtectionActiveTask schedules stopProtectionActiveTask after
		// 60 seconds and stores the ScheduledFuture in the controller task map. This planner records that
		// scheduler and callback boundary without invoking it.
		var rows = new List<PlayerProtectionActiveTaskSchedulerCallbackPlanRow>();

		if (request.Plan.Status == PlayerProtectionActiveTaskPlanStatus.AlreadyProtected)
		{
			Add(
				rows,
				PlayerProtectionActiveTaskSchedulerCallbackPlanRowKind.ObserveStartBranch,
				PlayerProtectionActiveTaskSchedulerCallbackPlanStatus.SkippedAlreadyProtected,
				javaCallReached: true,
				isLive: false,
				"if (!getOwner().isProtectionActive())",
				"PlayerController.startProtectionActiveTask",
				"Java returns before scheduling when the player is already protected."
			);

			return CreateReport(
				request,
				PlayerProtectionActiveTaskSchedulerCallbackPlanStatus.SkippedAlreadyProtected,
				rows,
				schedulesDelayedStop: false,
				storesScheduledFuture: false,
				hasOwnerPrototypeEvidence: request.OwnerPrototypeSnapshot != null
			);
		}

		Add(
			rows,
			PlayerProtectionActiveTaskSchedulerCallbackPlanRowKind.ObserveStartBranch,
			PlayerProtectionActiveTaskSchedulerCallbackPlanStatus.PlannedNotLive,
			javaCallReached: request.Plan.Status == PlayerProtectionActiveTaskPlanStatus.StartProtection,
			isLive: false,
			"if (!getOwner().isProtectionActive())",
			"PlayerController.startProtectionActiveTask",
			"Start branch reaches scheduler metadata when protection is not already active."
		);

		if (request.OwnerPrototypeSnapshot == null)
		{
			Add(
				rows,
				PlayerProtectionActiveTaskSchedulerCallbackPlanRowKind.RequireOwnerPrototype,
				PlayerProtectionActiveTaskSchedulerCallbackPlanStatus.BlockedMissingOwnerPrototype,
				javaCallReached: true,
				isLive: false,
				"addTask(TaskId.PROTECTION_ACTIVE, scheduledFuture)",
				"PlayerController.startProtectionActiveTask / CreatureController.addTask",
				"Scheduler metadata requires an owner-shaped task-map target before live scheduling can be considered."
			);

			return CreateReport(
				request,
				PlayerProtectionActiveTaskSchedulerCallbackPlanStatus.BlockedMissingOwnerPrototype,
				rows,
				schedulesDelayedStop: false,
				storesScheduledFuture: false,
				hasOwnerPrototypeEvidence: false
			);
		}

		Add(
			rows,
			PlayerProtectionActiveTaskSchedulerCallbackPlanRowKind.RecordScheduleCall,
			PlayerProtectionActiveTaskSchedulerCallbackPlanStatus.PlannedNotLive,
			javaCallReached: true,
			isLive: false,
			"ThreadPoolManager.getInstance().schedule(this::stopProtectionActiveTask, 60000)",
			"PlayerController.startProtectionActiveTask",
			"C# records the Java scheduler call but does not invoke ThreadPoolManager.Schedule."
		);
		Add(
			rows,
			PlayerProtectionActiveTaskSchedulerCallbackPlanRowKind.RecordCallbackTarget,
			PlayerProtectionActiveTaskSchedulerCallbackPlanStatus.PlannedNotLive,
			javaCallReached: true,
			isLive: false,
			"this::stopProtectionActiveTask",
			"PlayerController.stopProtectionActiveTask",
			"Callback target remains metadata-only and is not invoked by this plan."
		);
		Add(
			rows,
			PlayerProtectionActiveTaskSchedulerCallbackPlanRowKind.RecordTaskMapStorage,
			PlayerProtectionActiveTaskSchedulerCallbackPlanStatus.PlannedNotLive,
			javaCallReached: true,
			isLive: false,
			"addTask(TaskId.PROTECTION_ACTIVE, scheduledFuture)",
			request.OwnerPrototypeSnapshot.JavaSource,
			$"Owner prototype evidence exists for object id {request.OwnerPrototypeSnapshot.OwnerObjectId}, but production storage is still not wired."
		);
		Add(
			rows,
			PlayerProtectionActiveTaskSchedulerCallbackPlanRowKind.RecordRuntimeBlocker,
			PlayerProtectionActiveTaskSchedulerCallbackPlanStatus.PlannedNotLive,
			javaCallReached: true,
			isLive: false,
			"ScheduledFuture.cancel(false) / Future.isDone",
			"ThreadPoolManager.schedule / CreatureController.addTask",
			"Java/C# scheduler and future cancellation runtime comparison is still required."
		);

		return CreateReport(
			request,
			PlayerProtectionActiveTaskSchedulerCallbackPlanStatus.PlannedNotLive,
			rows,
			schedulesDelayedStop: true,
			storesScheduledFuture: true,
			hasOwnerPrototypeEvidence: true
		);
	}

	private static PlayerProtectionActiveTaskSchedulerCallbackPlan CreateReport(
		PlayerProtectionActiveTaskSchedulerCallbackPlanRequest request,
		PlayerProtectionActiveTaskSchedulerCallbackPlanStatus status,
		IReadOnlyList<PlayerProtectionActiveTaskSchedulerCallbackPlanRow> rows,
		bool schedulesDelayedStop,
		bool storesScheduledFuture,
		bool hasOwnerPrototypeEvidence
	) =>
		new(
			status,
			request.Plan.PlayerObjectId,
			ProtectionActiveDelayMilliseconds,
			rows,
			schedulesDelayedStop,
			storesScheduledFuture,
			InvokesScheduler: false,
			InvokesCallback: false,
			hasOwnerPrototypeEvidence,
			"PlayerController.startProtectionActiveTask scheduler callback metadata plan",
			IsLive: false
		);

	private static void Add(
		ICollection<PlayerProtectionActiveTaskSchedulerCallbackPlanRow> rows,
		PlayerProtectionActiveTaskSchedulerCallbackPlanRowKind kind,
		PlayerProtectionActiveTaskSchedulerCallbackPlanStatus status,
		bool javaCallReached,
		bool isLive,
		string javaOperation,
		string javaSource,
		string notes
	)
	{
		rows.Add(
			new PlayerProtectionActiveTaskSchedulerCallbackPlanRow(rows.Count + 1, kind, status, javaCallReached, isLive, javaOperation, javaSource, notes)
		);
	}
}
