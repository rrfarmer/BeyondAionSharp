using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerProtectionActiveTaskTaskMapAuditServiceTests
{
	[Fact]
	public void Create_RecordsJavaTaskIdAndScheduleRequirements()
	{
		var report = PlayerProtectionActiveTaskTaskMapAuditService.Create();

		Assert.False(report.HasLiveTaskMapAdapter);
		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskTaskMapAuditArea.TaskId
			&& row.JavaBehavior.Contains("ordinal 3", StringComparison.Ordinal)
			&& row.CSharpCurrentState.Contains("metadata", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskTaskMapAuditArea.JavaSchedule
			&& row.JavaBehavior.Contains("60000 milliseconds", StringComparison.Ordinal)
			&& row.Requirement.Contains("ThreadPoolManager.Schedule", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_RecordsAddReplaceAndCancelSemantics()
	{
		var report = PlayerProtectionActiveTaskTaskMapAuditService.Create();

		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskTaskMapAuditArea.JavaTaskReplacement
			&& row.JavaBehavior.Contains("tasks.compute", StringComparison.Ordinal)
			&& row.JavaBehavior.Contains("cancel(false)", StringComparison.Ordinal)
			&& row.Requirement.Contains("atomic replace-and-cancel", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskTaskMapAuditArea.JavaTaskCancel
			&& row.JavaBehavior.Contains("removes the Future", StringComparison.Ordinal)
			&& row.Requirement.Contains("remove before cancel", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskTaskMapAuditArea.JavaMissingTaskCancel
			&& row.JavaBehavior.Contains("returns null", StringComparison.Ordinal)
			&& row.Requirement.Contains("without throwing", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_RecordsLifecycleAndConditionalCancelDependencies()
	{
		var report = PlayerProtectionActiveTaskTaskMapAuditService.Create();

		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskTaskMapAuditArea.JavaConditionalCancel
			&& row.Status == PlayerProtectionActiveTaskTaskMapAuditStatus.Risk
			&& row.Notes.Contains("discovered dependency", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskTaskMapAuditArea.JavaLifecycleCleanup
			&& row.Requirement.Contains("cleanup", StringComparison.Ordinal)
			&& row.JavaBehavior.Contains("onDelete", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_RecordsCSharpSchedulerPrimitiveAndTaskMapGap()
	{
		var report = PlayerProtectionActiveTaskTaskMapAuditService.Create();

		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskTaskMapAuditArea.CSharpSchedulerHandle
			&& row.Status == PlayerProtectionActiveTaskTaskMapAuditStatus.ExistingCSharpPrimitive
			&& row.CSharpCurrentState.Contains("CancellationTokenSource", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskTaskMapAuditArea.CSharpTaskMapGap
			&& row.Status == PlayerProtectionActiveTaskTaskMapAuditStatus.Gap
			&& row.Requirement.Contains("narrow task-map adapter", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskTaskMapAuditArea.ImplementationChecklist
			&& row.Requirement.Contains("locking/ConcurrentDictionary strategy", StringComparison.Ordinal));
	}

	[Fact]
	public async Task Create_LinksBlockedReadinessReportToTaskMapAudit()
	{
		var readiness = await CreateStopReadinessAsync();

		var report = PlayerProtectionActiveTaskTaskMapAuditService.Create(readiness);

		Assert.True(report.SchedulerCapabilityBlockedByReadiness);
		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskTaskMapAuditArea.ReadinessGate
			&& row.Status == PlayerProtectionActiveTaskTaskMapAuditStatus.Gap
			&& row.Requirement.Contains("disabled", StringComparison.Ordinal));
	}

	private static async Task<PlayerProtectionActiveTaskLiveReadinessReport> CreateStopReadinessAsync()
	{
		var player = new Player { ObjectId = PlayerObjectId };
		player.SetVisualState(PlayerVisualStates.Blinking);
		var bridge = new PlayerProtectionActiveTaskExecutionBridgeService();
		var bridgeResult = await bridge.ExecuteAsync(new PlayerProtectionActiveTaskExecutionBridgeRequest(
			new PlayerProtectionActiveTaskAdapterRequest(
				player,
				PlayerProtectionActiveTaskAdapterAction.Stop,
				ExecuteLiveVisualMutation: true,
				HasProtectionActiveTask: true,
				IsSpawned: true),
			ExistingProtectionTaskPresent: true));
		var summary = PlayerProtectionActiveTaskExecutionSummaryService.Create(bridgeResult);

		return PlayerProtectionActiveTaskLiveReadinessService.Create(summary);
	}

	private const int PlayerObjectId = 1001;
}
