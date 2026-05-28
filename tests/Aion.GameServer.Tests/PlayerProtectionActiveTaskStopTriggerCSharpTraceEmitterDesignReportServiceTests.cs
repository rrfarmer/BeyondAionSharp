using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterDesignReportServiceTests
{
	[Fact]
	public void Create_ListsPacketControllerAndTeleportHookSites()
	{
		var report = PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterDesignReportService.Create(
			CreateRuntimeDesign(),
			CreateTraceSchema());

		Assert.False(report.IsLive);
		Assert.True(report.HasPacketHookSites);
		Assert.True(report.HasControllerHookSites);
		Assert.True(report.HasTeleportHookSites);
		Assert.True(report.RequiresLiveEmitter);
		Assert.False(report.ReadyForRuntimeComparison);
	}

	[Fact]
	public void Create_PacketRowsRequireRuntimeTraceRowKeyFields()
	{
		var report = PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterDesignReportService.Create(
			CreateRuntimeDesign(),
			CreateTraceSchema());

		Assert.Contains(report.Rows, row =>
			row.HookSite == PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterHookSite.PacketGuardAndStopDecision
			&& row.RequiredTraceFields.Contains("Scenario", StringComparison.Ordinal)
			&& row.RequiredTraceFields.Contains("ReturnReason", StringComparison.Ordinal)
			&& row.RequiredTraceFields.Contains("ExpectsStopProtectionCall", StringComparison.Ordinal)
			&& row.Notes.Contains("Java packet guard order", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_ControllerRowsDocumentStopTaskVisualFanoutAndAiSources()
	{
		var report = PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterDesignReportService.Create(
			CreateRuntimeDesign(),
			CreateTraceSchema());

		Assert.Contains(report.Rows, row => row.HookSite == PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterHookSite.ControllerStopEntry);
		Assert.Contains(report.Rows, row => row.HookSite == PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterHookSite.TaskCancellation);
		Assert.Contains(report.Rows, row => row.HookSite == PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterHookSite.VisualStateMutation);
		Assert.Contains(report.Rows, row => row.HookSite == PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterHookSite.StateBroadcastFanout);
		Assert.Contains(report.Rows, row => row.HookSite == PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterHookSite.AiMoveNotification);
		Assert.All(report.Rows, row => Assert.Equal(PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterDesignStatus.BlockedMissingLiveEmitter, row.Status));
	}

	[Fact]
	public void Create_TeleportRowDocumentsAnimationDoneAndSpawnTaskBranches()
	{
		var report = PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterDesignReportService.Create(
			CreateRuntimeDesign(),
			CreateTraceSchema());

		Assert.Contains(report.Rows, row =>
			row.HookSite == PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterHookSite.TeleportAnimationTaskDispatch
			&& row.JavaSource.Contains("CM_TELEPORT_ANIMATION_DONE", StringComparison.Ordinal)
			&& row.Notes.Contains("exception fallback", StringComparison.Ordinal)
			&& row.Notes.Contains("same-map protection-start skip", StringComparison.Ordinal));
	}

	private static PlayerProtectionActiveTaskStopTriggerTraceArtifactSchemaReport CreateTraceSchema() =>
		PlayerProtectionActiveTaskStopTriggerTraceArtifactSchemaReportService.Create(CreateRuntimeDesign());

	private static PlayerProtectionActiveTaskStopTriggerRuntimeComparisonDesignReport CreateRuntimeDesign() =>
		PlayerProtectionActiveTaskStopTriggerRuntimeComparisonDesignReportService.Create(CreateDetailedSummary());

	private static PlayerProtectionActiveTaskFirstActionStopTriggerSummaryReport CreateDetailedSummary() =>
		PlayerProtectionActiveTaskFirstActionStopTriggerSummaryReportService.Create(
			PlayerProtectionActiveTaskFirstActionStopTriggerAuditService.Create(new PlayerProtectionActiveTaskFirstActionStopTriggerAuditRequest(
				PlayerSpawned: true,
				AntiHackAccepted: true,
				TeleportationModeAbsoluteMove: false,
				PlayerProtectionActive: true,
				CurrentX: 100f,
				CurrentY: 200f,
				CurrentZ: 50f,
				PacketX: 101f,
				PacketY: 200f,
				PacketZ: 50f,
				EvaluateCmMoveInAir: true,
				EvaluateCmAttack: true,
				EvaluateCmCastSpell: true,
				EvaluateCmUseItem: true,
				EvaluateCmShowDialog: true,
				EvaluateCmDialogSelect: true,
				EvaluateCmCompositeStones: true,
				EvaluateCmEmotion: true)));
}
