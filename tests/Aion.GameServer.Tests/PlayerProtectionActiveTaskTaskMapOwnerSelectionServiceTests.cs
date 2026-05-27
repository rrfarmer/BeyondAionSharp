using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerProtectionActiveTaskTaskMapOwnerSelectionServiceTests
{
	[Fact]
	public void Create_RecordsFullJavaCreatureControllerTaskContract()
	{
		var report = PlayerProtectionActiveTaskTaskMapOwnerSelectionService.Create(new PlayerProtectionActiveTaskTaskMapOwnerSelectionRequest());

		Assert.False(report.IsLive);
		Assert.False(report.CanWireProductionScheduling);
		Assert.Equal(PlayerProtectionActiveTaskTaskMapOwnerOption.ControllerOwned, report.RecommendedOwner);
		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskTaskMapOwnerSelectionArea.JavaTaskStorage
			&& row.JavaOperation.Contains("ConcurrentHashMap<Integer, Future<?>>", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskTaskMapOwnerSelectionArea.HasTask
			&& row.JavaOperation.Contains("containsKey", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskTaskMapOwnerSelectionArea.HasScheduledTask
			&& row.JavaOperation.Contains("!task.isDone()", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskTaskMapOwnerSelectionArea.GetAndRemoveTask
			&& row.CSharpImplication.Contains("remove-before-cancel", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskTaskMapOwnerSelectionArea.CancelTask
			&& row.JavaOperation.Contains("task.cancel(false)", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskTaskMapOwnerSelectionArea.CancelTaskIfPresent
			&& row.JavaOperation.Contains("tasks.remove", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskTaskMapOwnerSelectionArea.AddTask
			&& row.JavaOperation.Contains("tasks.compute", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskTaskMapOwnerSelectionArea.CancelAllTasks
			&& row.JavaOperation.Contains("tasks.clear", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskTaskMapOwnerSelectionArea.OnDeleteCleanup
			&& row.JavaOperation.Contains("super.onDelete", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_RecommendsControllerOwnedAndRejectsModelAndExternalDefaults()
	{
		var report = PlayerProtectionActiveTaskTaskMapOwnerSelectionService.Create(new PlayerProtectionActiveTaskTaskMapOwnerSelectionRequest());

		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskTaskMapOwnerSelectionArea.ControllerOwnedCandidate
			&& row.Status == PlayerProtectionActiveTaskTaskMapOwnerSelectionStatus.Blocked
			&& row.Candidate == PlayerProtectionActiveTaskTaskMapOwnerOption.ControllerOwned
			&& row.BlocksLiveEnablement);
		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskTaskMapOwnerSelectionArea.PlayerModelOwnedCandidate
			&& row.Status == PlayerProtectionActiveTaskTaskMapOwnerSelectionStatus.RejectedCandidate
			&& row.Candidate == PlayerProtectionActiveTaskTaskMapOwnerOption.PlayerModelOwned
			&& row.Notes.Contains("persistence/model leakage", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskTaskMapOwnerSelectionArea.ExternalServiceOwnedCandidate
			&& row.Status == PlayerProtectionActiveTaskTaskMapOwnerSelectionStatus.RejectedCandidate
			&& row.Candidate == PlayerProtectionActiveTaskTaskMapOwnerOption.ExternalServiceOwned
			&& row.Notes.Contains("orphan cleanup", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskTaskMapOwnerSelectionArea.Recommendation
			&& row.Candidate == PlayerProtectionActiveTaskTaskMapOwnerOption.ControllerOwned
			&& row.CSharpImplication.Contains("controller-owned task storage", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_WithConcreteControllerOwnerStillRequiresRuntimeComparisonBeforeLiveScheduling()
	{
		var report = PlayerProtectionActiveTaskTaskMapOwnerSelectionService.Create(new PlayerProtectionActiveTaskTaskMapOwnerSelectionRequest(
			HasConcreteCSharpControllerTaskMapOwner: true));

		Assert.True(report.HasConcreteCSharpControllerTaskMapOwner);
		Assert.False(report.CanWireProductionScheduling);
		Assert.True(report.RequiresLifecycleCleanupHook);
		Assert.True(report.RequiresRuntimeConcurrencyComparison);
		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskTaskMapOwnerSelectionArea.ControllerOwnedCandidate
			&& row.Status == PlayerProtectionActiveTaskTaskMapOwnerSelectionStatus.PreferredCandidate
			&& !row.BlocksLiveEnablement);
		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskTaskMapOwnerSelectionArea.LiveEnablementBlocker
			&& row.Status == PlayerProtectionActiveTaskTaskMapOwnerSelectionStatus.NeedsVerification
			&& row.BlocksLiveEnablement
			&& row.Notes.Contains("runtime comparison remains required", StringComparison.Ordinal));
	}
}
