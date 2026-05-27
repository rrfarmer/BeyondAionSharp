using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerProtectionActiveTaskReadinessAggregateServiceTests
{
	[Fact]
	public async Task Create_StartAggregatesReadinessAuditSimulationAndRuntimeBlockers()
	{
		var summary = await CreateSummaryAsync(PlayerProtectionActiveTaskAdapterAction.Start, player => { }, existingTask: true);
		var readiness = PlayerProtectionActiveTaskLiveReadinessService.Create(summary);
		var taskMapPlan = CreateTaskOperationPlan(summary, existingTask: true);
		var simulation = PlayerProtectionActiveTaskTaskMapSimulationService.Create(new PlayerProtectionActiveTaskTaskMapSimulationRequest(
			taskMapPlan,
			ScheduledTaskHandle: new RecordingTaskHandle(),
			ExistingTaskHandle: new RecordingTaskHandle()));
		var cleanup = PlayerProtectionActiveTaskTaskMapLifecycleCleanupService.Create(new PlayerProtectionActiveTaskTaskMapLifecycleCleanupRequest(
			PendingProtectionTaskHandle: new RecordingTaskHandle()));
		var audit = PlayerProtectionActiveTaskTaskMapAuditService.Create(readiness);
		var ownerSelection = PlayerProtectionActiveTaskTaskMapOwnerSelectionService.Create(new PlayerProtectionActiveTaskTaskMapOwnerSelectionRequest());
		var ownerPrototype = CreateOwnerPrototypeSnapshot(withStoredTask: true);

		var report = PlayerProtectionActiveTaskReadinessAggregateService.Create(new PlayerProtectionActiveTaskReadinessAggregateRequest(
			summary,
			readiness,
			audit,
			[simulation],
			cleanup,
			ownerSelection,
			ownerPrototype));

		Assert.False(report.IsLive);
		Assert.False(report.CanEnableProtectionTaskMapStack);
		Assert.True(report.HasStartStorageEvidence);
		Assert.True(report.HasScheduledTaskHandleAdapterEvidence);
		Assert.Contains(PlayerProtectionActiveTaskReadinessAggregateArea.KnownListCastCancellation, report.BlockedAreas);
		Assert.Contains(PlayerProtectionActiveTaskReadinessAggregateArea.KnownListTargetClear, report.BlockedAreas);
		Assert.Contains(PlayerProtectionActiveTaskReadinessAggregateArea.PacketFanout, report.BlockedAreas);
		Assert.Contains(PlayerProtectionActiveTaskReadinessAggregateArea.SchedulerCallback, report.BlockedAreas);
		Assert.Contains(PlayerProtectionActiveTaskReadinessAggregateArea.ProductionOwnerSelection, report.BlockedAreas);
		Assert.Contains(PlayerProtectionActiveTaskReadinessAggregateArea.JavaRuntimeComparison, report.BlockedAreas);
		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskReadinessAggregateArea.TaskMapStorage
			&& row.Status == PlayerProtectionActiveTaskReadinessAggregateStatus.ObservedNonLive
			&& row.EvidenceSource == "Task-map simulation");
		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskReadinessAggregateArea.ScheduledTaskHandleAdapter
			&& row.Status == PlayerProtectionActiveTaskReadinessAggregateStatus.ObservedNonLive);
		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskReadinessAggregateArea.ProductionOwnerSelection
			&& row.EvidenceSource == "Owner selection report"
			&& row.Notes.Contains("controller-owned task storage", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskReadinessAggregateArea.ProductionOwnerSelection
			&& row.EvidenceSource == "Controller-owned owner prototype snapshot"
			&& row.Status == PlayerProtectionActiveTaskReadinessAggregateStatus.ObservedNonLive
			&& row.Notes.Contains("1 tracked protection task", StringComparison.Ordinal));
	}

	[Fact]
	public async Task Create_StopAggregatesCancellationAndAiMoveBlockers()
	{
		var summary = await CreateSummaryAsync(
			PlayerProtectionActiveTaskAdapterAction.Stop,
			player => player.SetVisualState(PlayerVisualStates.Blinking),
			existingTask: true);
		var readiness = PlayerProtectionActiveTaskLiveReadinessService.Create(summary);
		var taskMapPlan = CreateTaskOperationPlan(summary, existingTask: true);
		var simulation = PlayerProtectionActiveTaskTaskMapSimulationService.Create(new PlayerProtectionActiveTaskTaskMapSimulationRequest(
			taskMapPlan,
			ExistingTaskHandle: new RecordingTaskHandle()));
		var cleanup = PlayerProtectionActiveTaskTaskMapLifecycleCleanupService.Create(new PlayerProtectionActiveTaskTaskMapLifecycleCleanupRequest());
		var audit = PlayerProtectionActiveTaskTaskMapAuditService.Create(readiness);
		var ownerSelection = PlayerProtectionActiveTaskTaskMapOwnerSelectionService.Create(new PlayerProtectionActiveTaskTaskMapOwnerSelectionRequest());
		var ownerPrototype = CreateOwnerPrototypeSnapshot(withStoredTask: false);

		var report = PlayerProtectionActiveTaskReadinessAggregateService.Create(new PlayerProtectionActiveTaskReadinessAggregateRequest(
			summary,
			readiness,
			audit,
			[simulation],
			cleanup,
			ownerSelection,
			ownerPrototype));

		Assert.Equal(PlayerProtectionActiveTaskAdapterAction.Stop, report.Action);
		Assert.False(report.CanEnableProtectionTaskMapStack);
		Assert.True(report.HasStopCancellationEvidence);
		Assert.Contains(PlayerProtectionActiveTaskReadinessAggregateArea.TaskMapCancellation, report.Rows.Select(row => row.Area));
		Assert.Contains(PlayerProtectionActiveTaskReadinessAggregateArea.AiMoveNotification, report.BlockedAreas);
		Assert.DoesNotContain(PlayerProtectionActiveTaskReadinessAggregateArea.KnownListCastCancellation, report.BlockedAreas);
		Assert.DoesNotContain(PlayerProtectionActiveTaskReadinessAggregateArea.KnownListTargetClear, report.BlockedAreas);
		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskReadinessAggregateArea.TaskMapCancellation
			&& row.Status == PlayerProtectionActiveTaskReadinessAggregateStatus.ObservedNonLive
			&& row.JavaOperation == "cancelTask(TaskId.PROTECTION_ACTIVE)");
		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskReadinessAggregateArea.ProductionOwnerSelection
			&& row.EvidenceSource == "Owner selection report"
			&& row.Notes.Contains("Player model storage", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskReadinessAggregateArea.ProductionOwnerSelection
			&& row.EvidenceSource == "Controller-owned owner prototype snapshot"
			&& row.Status == PlayerProtectionActiveTaskReadinessAggregateStatus.Blocked
			&& row.Notes.Contains("not wired to PlayerController", StringComparison.Ordinal));
	}

	[Fact]
	public async Task Create_LifecycleCleanupPrerequisitesRemainLiveBlockers()
	{
		var summary = await CreateSummaryAsync(
			PlayerProtectionActiveTaskAdapterAction.Stop,
			player => player.SetVisualState(PlayerVisualStates.Blinking),
			existingTask: true,
			isSpawned: false);
		var readiness = PlayerProtectionActiveTaskLiveReadinessService.Create(summary);
		var cleanup = PlayerProtectionActiveTaskTaskMapLifecycleCleanupService.Create(new PlayerProtectionActiveTaskTaskMapLifecycleCleanupRequest(
			PendingProtectionTaskHandle: new RecordingTaskHandle(),
			ReplacementProtectionTaskHandle: new RecordingTaskHandle()));
		var ownerSelection = PlayerProtectionActiveTaskTaskMapOwnerSelectionService.Create(new PlayerProtectionActiveTaskTaskMapOwnerSelectionRequest(
			HasConcreteCSharpControllerTaskMapOwner: true));
		var ownerPrototype = CreateOwnerPrototypeSnapshot(withStoredTask: true);

		var report = PlayerProtectionActiveTaskReadinessAggregateService.Create(new PlayerProtectionActiveTaskReadinessAggregateRequest(
			summary,
			readiness,
			PlayerProtectionActiveTaskTaskMapAuditService.Create(readiness),
			Array.Empty<PlayerProtectionActiveTaskTaskMapSimulationReport>(),
			cleanup,
			ownerSelection,
			ownerPrototype));

		Assert.True(report.HasLifecycleCleanupEvidence);
		Assert.False(report.CanEnableProtectionTaskMapStack);
		Assert.Contains(PlayerProtectionActiveTaskReadinessAggregateArea.LifecycleCleanupHook, report.BlockedAreas);
		Assert.Contains(PlayerProtectionActiveTaskReadinessAggregateArea.ProductionOwnerSelection, report.BlockedAreas);
		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskReadinessAggregateArea.LifecycleCleanupHook
			&& row.EvidenceSource == "Lifecycle cleanup report"
			&& row.Notes.Contains("delete or logout", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskReadinessAggregateArea.JavaRuntimeComparison
			&& row.Status == PlayerProtectionActiveTaskReadinessAggregateStatus.NeedsVerification
			&& row.BlocksLiveEnablement);
		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskReadinessAggregateArea.ProductionOwnerSelection
			&& row.EvidenceSource == "Owner selection report"
			&& row.Status == PlayerProtectionActiveTaskReadinessAggregateStatus.ObservedNonLive
			&& row.Notes.Contains("best matches Java lifecycle", StringComparison.Ordinal));
	}

	private static async Task<PlayerProtectionActiveTaskExecutionSummary> CreateSummaryAsync(
		PlayerProtectionActiveTaskAdapterAction action,
		Action<Player> configurePlayer,
		bool existingTask,
		bool isSpawned = true)
	{
		var player = new Player { ObjectId = PlayerObjectId };
		configurePlayer(player);
		var bridge = new PlayerProtectionActiveTaskExecutionBridgeService();
		var bridgeResult = await bridge.ExecuteAsync(new PlayerProtectionActiveTaskExecutionBridgeRequest(
			new PlayerProtectionActiveTaskAdapterRequest(
				player,
				action,
				ExecuteLiveVisualMutation: true,
				HasProtectionActiveTask: action == PlayerProtectionActiveTaskAdapterAction.Stop,
				IsSpawned: isSpawned),
			KnownObjectFacts:
			[
				new PlayerProtectionAttackUtilKnownObjectFact(
					CastingCreatureObjectId,
					PlayerProtectionAttackUtilKnownObjectKind.Creature,
					TargetObjectId: PlayerObjectId,
					IsCasting: true,
					CastingSkillFirstTargetObjectId: PlayerObjectId),
				new PlayerProtectionAttackUtilKnownObjectFact(
					TargetingPlayerObjectId,
					PlayerProtectionAttackUtilKnownObjectKind.Player,
					TargetObjectId: PlayerObjectId,
					IsCasting: false,
					CastingSkillFirstTargetObjectId: null),
			],
			ExistingProtectionTaskPresent: existingTask));

		return PlayerProtectionActiveTaskExecutionSummaryService.Create(bridgeResult);
	}

	private static PlayerProtectionActiveTaskTaskOperationPlan CreateTaskOperationPlan(
		PlayerProtectionActiveTaskExecutionSummary summary,
		bool existingTask)
	{
		var player = new Player { ObjectId = summary.PlayerObjectId };
		if (summary.Action == PlayerProtectionActiveTaskAdapterAction.Stop)
			player.SetVisualState(PlayerVisualStates.Blinking);

		var adapterResult = PlayerProtectionActiveTaskAdapterService.Apply(new PlayerProtectionActiveTaskAdapterRequest(
			player,
			summary.Action,
			ExecuteLiveVisualMutation: true,
			HasProtectionActiveTask: summary.Action == PlayerProtectionActiveTaskAdapterAction.Stop,
			IsSpawned: true));

		return PlayerProtectionActiveTaskTaskOperationPlanService.Create(adapterResult.Plan, existingTask);
	}

	private static PlayerProtectionActiveTaskControllerTaskMapOwnerPrototypeSnapshot CreateOwnerPrototypeSnapshot(
		bool withStoredTask)
	{
		var owner = new PlayerProtectionActiveTaskControllerTaskMapOwnerPrototypeService(PlayerObjectId);
		if (withStoredTask)
			owner.AddTask(new RecordingTaskHandle());

		return owner.CreateSnapshot();
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
	private const int CastingCreatureObjectId = 1002;
	private const int TargetingPlayerObjectId = 1003;
}
