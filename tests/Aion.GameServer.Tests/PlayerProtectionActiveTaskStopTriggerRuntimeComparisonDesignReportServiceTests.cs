using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerProtectionActiveTaskStopTriggerRuntimeComparisonDesignReportServiceTests
{
	[Fact]
	public void Create_IncludesAllRequiredRuntimeComparisonScenarios()
	{
		var report = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonDesignReportService.Create(CreateDetailedSummary());

		Assert.False(report.IsLive);
		Assert.True(report.HasMovementThresholdScenario);
		Assert.True(report.HasAirMovementScenario);
		Assert.True(report.HasEarlyActionScenario);
		Assert.True(report.HasInvalidAfterStopScenario);
		Assert.True(report.HasLateEmotionScenario);
		Assert.True(report.HasEmotionEarlyReturnScenario);
	}

	[Fact]
	public void Create_CmMoveScenarioDocumentsThresholdAndSkipBranches()
	{
		var report = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonDesignReportService.Create(CreateDetailedSummary());

		Assert.Contains(report.Rows, row =>
			row.Scenario == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonScenario.CmMoveThresholdBranches
			&& row.ExpectsStopProtectionCall
			&& row.ExpectedStopPosition.Contains("oldZ > packetZ + 0.5", StringComparison.Ordinal)
			&& row.RequiredJavaTraceArtifact.Contains("same-position turn", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_ListsControllerSideEffectObservablesForStopScenarios()
	{
		var report = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonDesignReportService.Create(CreateDetailedSummary());

		Assert.Contains(report.Rows, row =>
			row.ExpectsStopProtectionCall
			&& row.ExpectedControllerObservables.Contains("cancelTask(TaskId.PROTECTION_ACTIVE)", StringComparison.Ordinal)
			&& row.ExpectedControllerObservables.Contains("SM_PLAYER_STATE", StringComparison.Ordinal)
			&& row.ExpectedControllerObservables.Contains("notifyAIOnMove", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_CompositeScenarioRequiresInvalidAfterStopArtifacts()
	{
		var report = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonDesignReportService.Create(CreateDetailedSummary());

		Assert.Contains(report.Rows, row =>
			row.Scenario == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonScenario.CmCompositeStonesInvalidAfterStop
			&& row.ExpectedStopPosition.Contains("later invalid branches", StringComparison.Ordinal)
			&& row.RequiredJavaTraceArtifact.Contains("invalid-after-stop", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_EmotionEarlyReturnScenarioExpectsNoStop()
	{
		var report = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonDesignReportService.Create(CreateDetailedSummary());

		Assert.Contains(report.Rows, row =>
			row.Scenario == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonScenario.CmEmotionEarlyReturnNoStop
			&& !row.ExpectsStopProtectionCall
			&& row.ExpectedControllerObservables.Contains("No controller stop observables expected", StringComparison.Ordinal)
			&& row.ExpectedPacketOrActionObservables.Contains("stance rejection packet", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_RemainsBlockedUntilJavaTraceArtifactsAndLiveHooksExist()
	{
		var report = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonDesignReportService.Create(CreateDetailedSummary());

		Assert.False(report.ReadyForRuntimeComparison);
		Assert.True(report.RequiresJavaTraceArtifacts);
		Assert.True(report.RequiresLiveCSharpPacketHooks);
		Assert.All(report.Rows, row => Assert.Equal(PlayerProtectionActiveTaskStopTriggerRuntimeComparisonStatus.BlockedMissingJavaTraceArtifact, row.Status));
	}

	private static PlayerProtectionActiveTaskFirstActionStopTriggerSummaryReport CreateDetailedSummary() =>
		PlayerProtectionActiveTaskFirstActionStopTriggerSummaryReportService.Create(
			PlayerProtectionActiveTaskFirstActionStopTriggerAuditService.Create(CreateBaseRequest(
				packetX: 101f,
				evaluateCmMoveInAir: true,
				evaluateCmAttack: true,
				evaluateCmCastSpell: true,
				evaluateCmUseItem: true,
				evaluateCmShowDialog: true,
				evaluateCmDialogSelect: true,
				evaluateCmCompositeStones: true,
				evaluateCmEmotion: true)));

	private static PlayerProtectionActiveTaskFirstActionStopTriggerAuditRequest CreateBaseRequest(
		float packetX = CurrentX,
		bool evaluateCmMoveInAir = false,
		bool evaluateCmAttack = false,
		bool evaluateCmCastSpell = false,
		bool evaluateCmUseItem = false,
		bool evaluateCmShowDialog = false,
		bool evaluateCmDialogSelect = false,
		bool evaluateCmCompositeStones = false,
		bool evaluateCmEmotion = false) =>
		new(
			PlayerSpawned: true,
			AntiHackAccepted: true,
			TeleportationModeAbsoluteMove: false,
			PlayerProtectionActive: true,
			CurrentX,
			CurrentY,
			CurrentZ,
			packetX,
			CurrentY,
			CurrentZ,
			EvaluateCmMoveInAir: evaluateCmMoveInAir,
			EvaluateCmAttack: evaluateCmAttack,
			EvaluateCmCastSpell: evaluateCmCastSpell,
			EvaluateCmUseItem: evaluateCmUseItem,
			EvaluateCmShowDialog: evaluateCmShowDialog,
			EvaluateCmDialogSelect: evaluateCmDialogSelect,
			EvaluateCmCompositeStones: evaluateCmCompositeStones,
			EvaluateCmEmotion: evaluateCmEmotion);

	private const float CurrentX = 100f;
	private const float CurrentY = 200f;
	private const float CurrentZ = 50f;
}
