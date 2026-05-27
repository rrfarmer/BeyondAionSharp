using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerProtectionActiveTaskTaskMapLifecycleCleanupServiceTests
{
	[Fact]
	public void Create_WithoutPendingTaskReportsNoOpCleanupAndPrerequisites()
	{
		var report = PlayerProtectionActiveTaskTaskMapLifecycleCleanupService.Create(new PlayerProtectionActiveTaskTaskMapLifecycleCleanupRequest());

		Assert.False(report.IsLive);
		Assert.False(report.HadPendingProtectionTask);
		Assert.False(report.CanceledTaskDuringCleanup);
		Assert.Equal(0, report.CleanupCanceledTaskCount);
		Assert.Equal(0, report.FinalSnapshot.Count);
		Assert.Contains(report.Rows, row =>
			row.Kind == PlayerProtectionActiveTaskTaskMapLifecycleCleanupRowKind.CancelAllTasks
			&& row.AdapterResult?.Status == PlayerProtectionActiveTaskTaskMapOperationStatus.NoTasksToCancel);
		Assert.Contains(report.RemainingPrerequisites, prerequisite => prerequisite.Contains("production C# owner", StringComparison.Ordinal));
		Assert.Contains(report.RemainingPrerequisites, prerequisite => prerequisite.Contains("delete or logout", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_WithPendingProtectionTaskCancelsItDuringCleanup()
	{
		var pending = new RecordingTaskHandle();

		var report = PlayerProtectionActiveTaskTaskMapLifecycleCleanupService.Create(new PlayerProtectionActiveTaskTaskMapLifecycleCleanupRequest(
			PendingProtectionTaskHandle: pending));

		Assert.True(report.HadPendingProtectionTask);
		Assert.False(report.ReplacedPendingProtectionTask);
		Assert.True(report.CanceledTaskDuringCleanup);
		Assert.Equal(1, report.CleanupCanceledTaskCount);
		Assert.Equal(1, pending.CancelCalls);
		Assert.False(pending.MayInterruptIfRunningValues.Single());
		Assert.Equal(0, report.FinalSnapshot.Count);
		Assert.Contains(report.Rows, row =>
			row.Kind == PlayerProtectionActiveTaskTaskMapLifecycleCleanupRowKind.SeedPendingProtectionTask
			&& row.AdapterResult?.Status == PlayerProtectionActiveTaskTaskMapOperationStatus.Stored);
	}

	[Fact]
	public void Create_AfterReplacementCancelsOldDuringReplaceAndNewDuringCleanup()
	{
		var oldHandle = new RecordingTaskHandle();
		var replacement = new RecordingTaskHandle();

		var report = PlayerProtectionActiveTaskTaskMapLifecycleCleanupService.Create(new PlayerProtectionActiveTaskTaskMapLifecycleCleanupRequest(
			PendingProtectionTaskHandle: oldHandle,
			ReplacementProtectionTaskHandle: replacement));

		Assert.True(report.HadPendingProtectionTask);
		Assert.True(report.ReplacedPendingProtectionTask);
		Assert.True(report.CanceledTaskDuringReplacement);
		Assert.True(report.CanceledTaskDuringCleanup);
		Assert.Equal(1, report.CleanupCanceledTaskCount);
		Assert.Equal(1, oldHandle.CancelCalls);
		Assert.Equal(1, replacement.CancelCalls);
		Assert.Equal(0, report.FinalSnapshot.Count);
		Assert.Contains(report.Rows, row =>
			row.Kind == PlayerProtectionActiveTaskTaskMapLifecycleCleanupRowKind.ReplacePendingProtectionTask
			&& row.AdapterResult?.Status == PlayerProtectionActiveTaskTaskMapOperationStatus.ReplacedExistingTask
			&& row.AdapterResult.CanceledTask);
	}

	private sealed class RecordingTaskHandle : IPlayerProtectionActiveTaskTaskHandle
	{
		public bool IsDone { get; private set; }
		public int CancelCalls { get; private set; }
		public List<bool> MayInterruptIfRunningValues { get; } = [];

		public bool Cancel(bool mayInterruptIfRunning)
		{
			CancelCalls++;
			MayInterruptIfRunningValues.Add(mayInterruptIfRunning);
			if (IsDone)
				return false;

			IsDone = true;
			return true;
		}
	}
}
