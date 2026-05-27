using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerProtectionActiveTaskLifecycleClosureReportServiceTests
{
	[Fact]
	public async Task Create_FullNonLiveEvidenceStackStillBlockedByLiveSideEffectsAndRuntimeComparison()
	{
		var aggregate = await CreateAggregateAsync(
			includeDelayedPreview: true,
			alreadyProtected: false,
			ownerHasTask: true);

		var report = PlayerProtectionActiveTaskLifecycleClosureReportService.Create(aggregate);

		Assert.False(report.IsLive);
		Assert.False(report.CanEnableProductionProtectionLifecycle);
		Assert.True(report.HasOwnerSelectionEvidence);
		Assert.True(report.HasSchedulerCallbackEvidence);
		Assert.True(report.HasDelayedStopPreviewEvidence);
		Assert.True(report.HasLifecycleCleanupEvidence);
		Assert.True(report.NeedsRuntimeComparison);
		Assert.Contains(report.Rows, row =>
			row.Prerequisite == PlayerProtectionActiveTaskLifecycleClosurePrerequisite.DelayedStopCallbackPreview
			&& row.Status == PlayerProtectionActiveTaskLifecycleClosureStatus.Blocked
			&& row.Notes.Contains("live callback invocation remains disabled", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.Prerequisite == PlayerProtectionActiveTaskLifecycleClosurePrerequisite.LiveSideEffects
			&& row.BlocksProductionEnablement
			&& row.Notes.Contains("InvokesSocketFanout=False", StringComparison.Ordinal)
			&& row.Notes.Contains("InvokesAiMoveNotification=False", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.Prerequisite == PlayerProtectionActiveTaskLifecycleClosurePrerequisite.RuntimeComparison
			&& row.Status == PlayerProtectionActiveTaskLifecycleClosureStatus.NeedsVerification);
	}

	[Fact]
	public async Task Create_MissingDelayedCallbackPreviewRemainsBlocked()
	{
		var aggregate = await CreateAggregateAsync(
			includeDelayedPreview: false,
			alreadyProtected: false,
			ownerHasTask: true);

		var report = PlayerProtectionActiveTaskLifecycleClosureReportService.Create(aggregate);

		Assert.False(report.HasDelayedStopPreviewEvidence);
		Assert.False(report.CanEnableProductionProtectionLifecycle);
		Assert.Contains(report.Rows, row =>
			row.Prerequisite == PlayerProtectionActiveTaskLifecycleClosurePrerequisite.DelayedStopCallbackPreview
			&& row.Status == PlayerProtectionActiveTaskLifecycleClosureStatus.Blocked
			&& row.Notes.Contains("Delayed-stop callback preview is required", StringComparison.Ordinal));
	}

	[Fact]
	public async Task Create_AlreadyProtectedCallbackPathRemainsSkippedNotReady()
	{
		var aggregate = await CreateAggregateAsync(
			includeDelayedPreview: true,
			alreadyProtected: true,
			ownerHasTask: false);

		var report = PlayerProtectionActiveTaskLifecycleClosureReportService.Create(aggregate);

		Assert.True(report.HasDelayedStopPreviewEvidence);
		Assert.False(report.CanEnableProductionProtectionLifecycle);
		Assert.Contains(report.Rows, row =>
			row.Prerequisite == PlayerProtectionActiveTaskLifecycleClosurePrerequisite.DelayedStopCallbackPreview
			&& row.Status == PlayerProtectionActiveTaskLifecycleClosureStatus.Skipped
			&& !row.BlocksProductionEnablement);
		Assert.Contains(report.Rows, row =>
			row.Prerequisite == PlayerProtectionActiveTaskLifecycleClosurePrerequisite.SchedulerCallbackPlan
			&& row.Status == PlayerProtectionActiveTaskLifecycleClosureStatus.Skipped);
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
