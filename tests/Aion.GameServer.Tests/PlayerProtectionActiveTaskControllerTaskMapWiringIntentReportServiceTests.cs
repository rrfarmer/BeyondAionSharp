using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerProtectionActiveTaskControllerTaskMapWiringIntentReportServiceTests
{
	[Fact]
	public async Task Create_FullClosureMapsToBlockedProductionHooks()
	{
		var closure = PlayerProtectionActiveTaskLifecycleClosureReportService.Create(await CreateAggregateAsync(
			includeDelayedPreview: true,
			alreadyProtected: false,
			ownerHasTask: true));

		var report = PlayerProtectionActiveTaskControllerTaskMapWiringIntentReportService.Create(closure);

		Assert.False(report.IsLive);
		Assert.False(report.ReadyForImplementation);
		Assert.True(report.HasStartStorageIntent);
		Assert.True(report.HasStopCancellationIntent);
		Assert.True(report.HasSchedulerCallbackIntent);
		Assert.True(report.HasLifecycleCleanupIntent);
		Assert.True(report.HasRuntimeComparisonBlocker);
		Assert.Contains(report.Rows, row =>
			row.Hook == PlayerProtectionActiveTaskControllerTaskMapWiringHook.StartProtectionTaskStorage
			&& row.ShouldImplementHook
			&& row.BlocksImplementation
			&& row.CSharpTarget.Contains("task-map storage hook", StringComparison.Ordinal)
			&& row.JavaOperation == "addTask(TaskId.PROTECTION_ACTIVE, scheduledFuture)");
		Assert.Contains(report.Rows, row =>
			row.Hook == PlayerProtectionActiveTaskControllerTaskMapWiringHook.SchedulerCallbackExecution
			&& row.Status == PlayerProtectionActiveTaskControllerTaskMapWiringIntentStatus.PlannedBlocked
			&& row.Notes.Contains("live scheduler/callback execution remains disabled", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.Hook == PlayerProtectionActiveTaskControllerTaskMapWiringHook.RuntimeComparison
			&& row.Status == PlayerProtectionActiveTaskControllerTaskMapWiringIntentStatus.NeedsRuntimeVerification
			&& !row.ShouldImplementHook);
	}

	[Fact]
	public async Task Create_AlreadyProtectedPathDoesNotRequestSchedulerOrTaskStorageHooks()
	{
		var closure = PlayerProtectionActiveTaskLifecycleClosureReportService.Create(await CreateAggregateAsync(
			includeDelayedPreview: true,
			alreadyProtected: true,
			ownerHasTask: false));

		var report = PlayerProtectionActiveTaskControllerTaskMapWiringIntentReportService.Create(closure);

		Assert.False(report.ReadyForImplementation);
		Assert.False(report.HasStartStorageIntent);
		Assert.False(report.HasSchedulerCallbackIntent);
		Assert.Contains(report.Rows, row =>
			row.Hook == PlayerProtectionActiveTaskControllerTaskMapWiringHook.StartProtectionTaskStorage
			&& row.Status == PlayerProtectionActiveTaskControllerTaskMapWiringIntentStatus.SkippedByJavaBranch
			&& !row.ShouldImplementHook
			&& !row.BlocksImplementation);
		Assert.Contains(report.Rows, row =>
			row.Hook == PlayerProtectionActiveTaskControllerTaskMapWiringHook.SchedulerCallbackExecution
			&& row.Status == PlayerProtectionActiveTaskControllerTaskMapWiringIntentStatus.SkippedByJavaBranch
			&& row.Notes.Contains("returns before scheduling", StringComparison.Ordinal));
	}

	[Fact]
	public async Task Create_MissingDelayedPreviewBlocksBeforeStopCancellationHookWork()
	{
		var closure = PlayerProtectionActiveTaskLifecycleClosureReportService.Create(await CreateAggregateAsync(
			includeDelayedPreview: false,
			alreadyProtected: false,
			ownerHasTask: true));

		var report = PlayerProtectionActiveTaskControllerTaskMapWiringIntentReportService.Create(closure);

		Assert.False(report.ReadyForImplementation);
		Assert.False(report.HasStopCancellationIntent);
		Assert.Contains(report.Rows, row =>
			row.Hook == PlayerProtectionActiveTaskControllerTaskMapWiringHook.StopProtectionTaskCancellation
			&& row.Status == PlayerProtectionActiveTaskControllerTaskMapWiringIntentStatus.PlannedBlocked
			&& !row.ShouldImplementHook
			&& row.Notes.Contains("Delayed callback preview is missing", StringComparison.Ordinal));
	}

	private static async Task<PlayerProtectionActiveTaskReadinessAggregateReport> CreateAggregateAsync(
		bool includeDelayedPreview,
		bool alreadyProtected,
		bool ownerHasTask)
	{
		var summary = await CreateSummaryAsync(alreadyProtected);
		var readiness = PlayerProtectionActiveTaskLiveReadinessService.Create(summary);
		var cleanup = PlayerProtectionActiveTaskTaskMapLifecycleCleanupService.Create(new PlayerProtectionActiveTaskTaskMapLifecycleCleanupRequest(
			PendingProtectionTaskHandle: ownerHasTask ? new RecordingTaskHandle() : null));
		var ownerSelection = PlayerProtectionActiveTaskTaskMapOwnerSelectionService.Create(new PlayerProtectionActiveTaskTaskMapOwnerSelectionRequest());
		var owner = CreateOwnerPrototype(ownerHasTask);
		var ownerSnapshot = owner.CreateSnapshot();
		var schedulerPlan = PlayerProtectionActiveTaskSchedulerCallbackPlanService.Create(new PlayerProtectionActiveTaskSchedulerCallbackPlanRequest(
			CreateStartPlan(alreadyProtected),
			ownerSnapshot));
		var delayedPreview = includeDelayedPreview
			? PlayerProtectionActiveTaskDelayedStopCallbackPreviewService.Create(new PlayerProtectionActiveTaskDelayedStopCallbackPreviewRequest(
				schedulerPlan,
				CreateStopTaskOperationPlan(ownerHasTask),
				owner))
			: null;

		return PlayerProtectionActiveTaskReadinessAggregateService.Create(new PlayerProtectionActiveTaskReadinessAggregateRequest(
			summary,
			readiness,
			PlayerProtectionActiveTaskTaskMapAuditService.Create(readiness),
			Array.Empty<PlayerProtectionActiveTaskTaskMapSimulationReport>(),
			cleanup,
			ownerSelection,
			ownerSnapshot,
			schedulerPlan,
			delayedPreview));
	}

	private static async Task<PlayerProtectionActiveTaskExecutionSummary> CreateSummaryAsync(bool alreadyProtected)
	{
		var player = new Player { ObjectId = PlayerObjectId };
		if (alreadyProtected)
			player.SetVisualState(PlayerVisualStates.Blinking);

		var bridge = new PlayerProtectionActiveTaskExecutionBridgeService();
		var bridgeResult = await bridge.ExecuteAsync(new PlayerProtectionActiveTaskExecutionBridgeRequest(
			new PlayerProtectionActiveTaskAdapterRequest(
				player,
				PlayerProtectionActiveTaskAdapterAction.Start,
				ExecuteLiveVisualMutation: true),
			ExistingProtectionTaskPresent: false));

		return PlayerProtectionActiveTaskExecutionSummaryService.Create(bridgeResult);
	}

	private static PlayerProtectionActiveTaskPlan CreateStartPlan(bool alreadyProtected)
	{
		var player = new Player { ObjectId = PlayerObjectId };
		if (alreadyProtected)
			player.SetVisualState(PlayerVisualStates.Blinking);

		return PlayerProtectionActiveTaskAdapterService.Apply(new PlayerProtectionActiveTaskAdapterRequest(
			player,
			PlayerProtectionActiveTaskAdapterAction.Start,
			ExecuteLiveVisualMutation: true)).Plan;
	}

	private static PlayerProtectionActiveTaskTaskOperationPlan CreateStopTaskOperationPlan(bool existingTask)
	{
		var player = new Player { ObjectId = PlayerObjectId };
		player.SetVisualState(PlayerVisualStates.Blinking);
		var stopPlan = PlayerProtectionActiveTaskPlanService.CreateStopPlan(
			player,
			hasProtectionActiveTask: existingTask,
			isSpawned: true);

		return PlayerProtectionActiveTaskTaskOperationPlanService.Create(stopPlan, existingTask);
	}

	private static PlayerProtectionActiveTaskControllerTaskMapOwnerPrototypeService CreateOwnerPrototype(bool withStoredTask)
	{
		var owner = new PlayerProtectionActiveTaskControllerTaskMapOwnerPrototypeService(PlayerObjectId);
		if (withStoredTask)
			owner.AddTask(new RecordingTaskHandle());

		return owner;
	}

	private sealed class RecordingTaskHandle : IPlayerProtectionActiveTaskTaskHandle
	{
		public bool IsDone { get; private set; }

		public bool Cancel(bool mayInterruptIfRunning)
		{
			if (IsDone)
				return false;

			IsDone = true;
			return true;
		}
	}

	private const int PlayerObjectId = 1001;
}
