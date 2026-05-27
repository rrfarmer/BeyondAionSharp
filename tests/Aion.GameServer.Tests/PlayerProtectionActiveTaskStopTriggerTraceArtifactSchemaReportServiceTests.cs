using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerProtectionActiveTaskStopTriggerTraceArtifactSchemaReportServiceTests
{
	[Fact]
	public void Create_IncludesAllRequiredTracePhases()
	{
		var report = PlayerProtectionActiveTaskStopTriggerTraceArtifactSchemaReportService.Create(CreateRuntimeDesign());

		Assert.False(report.IsLive);
		Assert.True(report.HasAllRequiredPhases);
		Assert.Contains(report.Phases, row => row.Phase == PlayerProtectionActiveTaskStopTriggerTraceArtifactPhase.PacketEnter);
		Assert.Contains(report.Phases, row => row.Phase == PlayerProtectionActiveTaskStopTriggerTraceArtifactPhase.GuardReturn);
		Assert.Contains(report.Phases, row => row.Phase == PlayerProtectionActiveTaskStopTriggerTraceArtifactPhase.StopCalled);
		Assert.Contains(report.Phases, row => row.Phase == PlayerProtectionActiveTaskStopTriggerTraceArtifactPhase.TaskCancel);
		Assert.Contains(report.Phases, row => row.Phase == PlayerProtectionActiveTaskStopTriggerTraceArtifactPhase.VisualMutate);
		Assert.Contains(report.Phases, row => row.Phase == PlayerProtectionActiveTaskStopTriggerTraceArtifactPhase.PacketFanout);
		Assert.Contains(report.Phases, row => row.Phase == PlayerProtectionActiveTaskStopTriggerTraceArtifactPhase.AiNotify);
		Assert.Contains(report.Phases, row => row.Phase == PlayerProtectionActiveTaskStopTriggerTraceArtifactPhase.PacketExit);
	}

	[Fact]
	public void Create_RequiresMovementPrecisionAndTaskCancellationFields()
	{
		var report = PlayerProtectionActiveTaskStopTriggerTraceArtifactSchemaReportService.Create(CreateRuntimeDesign());

		Assert.True(report.HasMovementPrecisionFields);
		Assert.True(report.HasTaskCancellationFields);
		Assert.Contains(report.Fields, row =>
			row.Field == PlayerProtectionActiveTaskStopTriggerTraceArtifactField.MovementZDelta
			&& row.Notes.Contains("strict > 0.5", StringComparison.Ordinal));
		Assert.Contains(report.Fields, row =>
			row.Field == PlayerProtectionActiveTaskStopTriggerTraceArtifactField.TaskRemovedBeforeCancel
			&& row.Notes.Contains("remove-before-cancel", StringComparison.Ordinal));
		Assert.Contains(report.Fields, row =>
			row.Field == PlayerProtectionActiveTaskStopTriggerTraceArtifactField.FutureCancelArgument
			&& row.Notes.Contains("Future.cancel(false)", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_RequiresFanoutAndAiNotifyFields()
	{
		var report = PlayerProtectionActiveTaskStopTriggerTraceArtifactSchemaReportService.Create(CreateRuntimeDesign());

		Assert.True(report.HasFanoutAndAiFields);
		Assert.Contains(report.Fields, row =>
			row.Field == PlayerProtectionActiveTaskStopTriggerTraceArtifactField.FanoutIncludeSelf
			&& row.RequiredFor.Contains("direct packet", StringComparison.Ordinal));
		Assert.Contains(report.ControllerObservables, row =>
			row.JavaOperation.Contains("SM_PLAYER_STATE", StringComparison.Ordinal)
			&& row.RequiredFields.Contains("FanoutRecipientCount", StringComparison.Ordinal));
		Assert.Contains(report.ControllerObservables, row =>
			row.JavaOperation == "notifyAIOnMove()"
			&& row.RequiredFields.Contains("NotifyAiOnMoveCalled", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_IncludesTeleportAnimationDoneGeneratedArtifactPhases()
	{
		var report = PlayerProtectionActiveTaskStopTriggerTraceArtifactSchemaReportService.Create(CreateRuntimeDesign());

		Assert.Contains(report.Phases, row =>
			row.Phase == PlayerProtectionActiveTaskStopTriggerTraceArtifactPhase.TeleportTaskRemove
			&& row.JavaSource == "CM_TELEPORT_ANIMATION_DONE.runImpl");
		Assert.Contains(report.Phases, row =>
			row.Phase == PlayerProtectionActiveTaskStopTriggerTraceArtifactPhase.TeleportTaskNoOp
			&& row.RequiredObservation.Contains("missing/done/non-runnable", StringComparison.Ordinal));
		Assert.Contains(report.Phases, row =>
			row.Phase == PlayerProtectionActiveTaskStopTriggerTraceArtifactPhase.SpawnTaskRun
			&& row.Notes.Contains("run inline", StringComparison.Ordinal));
		Assert.Contains(report.Phases, row =>
			row.Phase == PlayerProtectionActiveTaskStopTriggerTraceArtifactPhase.SpawnTaskGetException
			&& row.JavaSource == "CM_TELEPORT_ANIMATION_DONE.runImpl");
		Assert.Contains(report.Phases, row =>
			row.Phase == PlayerProtectionActiveTaskStopTriggerTraceArtifactPhase.ExceptionLogged
			&& row.RequiredObservation.Contains("e.getCause()", StringComparison.Ordinal));
		Assert.Contains(report.Phases, row =>
			row.Phase == PlayerProtectionActiveTaskStopTriggerTraceArtifactPhase.SpawnedGuardNoOp
			&& row.Notes.Contains("skips fallback packet and spawn", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_IncludesTeleportCallerOriginAndSpawnTaskFields()
	{
		var report = PlayerProtectionActiveTaskStopTriggerTraceArtifactSchemaReportService.Create(CreateRuntimeDesign());

		Assert.Contains(report.Fields, row =>
			row.Field == PlayerProtectionActiveTaskStopTriggerTraceArtifactField.CallerClass
			&& row.SerializationNote.Contains("string", StringComparison.Ordinal));
		Assert.Contains(report.Fields, row =>
			row.Field == PlayerProtectionActiveTaskStopTriggerTraceArtifactField.StartProtectionLine
			&& row.Notes.Contains("do not invoke", StringComparison.Ordinal));
		Assert.Contains(report.Fields, row =>
			row.Field == PlayerProtectionActiveTaskStopTriggerTraceArtifactField.StartsProtectionBeforeWorldSpawn
			&& row.Notes.Contains("Beritra", StringComparison.Ordinal));
		Assert.Contains(report.Fields, row =>
			row.Field == PlayerProtectionActiveTaskStopTriggerTraceArtifactField.TeleportTaskRunnableFuture
			&& row.Notes.Contains("instanceof RunnableFuture", StringComparison.Ordinal));
		Assert.Contains(report.Fields, row =>
			row.Field == PlayerProtectionActiveTaskStopTriggerTraceArtifactField.TeleportTaskIsDone
			&& row.Notes.Contains("Observational only", StringComparison.Ordinal));
		Assert.Contains(report.Fields, row =>
			row.Field == PlayerProtectionActiveTaskStopTriggerTraceArtifactField.SpawnTaskGetExceptionType
			&& row.RequiredFor.Contains("exception", StringComparison.Ordinal));
		Assert.Contains(report.Fields, row =>
			row.Field == PlayerProtectionActiveTaskStopTriggerTraceArtifactField.InstanceExists
			&& row.Notes.Contains("missing-instance", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_ListsPacketSpecificReturnReasonsWithStopExpectations()
	{
		var report = PlayerProtectionActiveTaskStopTriggerTraceArtifactSchemaReportService.Create(CreateRuntimeDesign());

		Assert.True(report.HasPacketReturnReasons);
		Assert.Contains(report.PacketReturnReasons, row =>
			row.Reason == PlayerProtectionActiveTaskStopTriggerTraceArtifactPacketReturnReason.CmMoveAntiHackRejected
			&& !row.ExpectsStopProtectionCall);
		Assert.Contains(report.PacketReturnReasons, row =>
			row.Reason == PlayerProtectionActiveTaskStopTriggerTraceArtifactPacketReturnReason.CmMoveAcceptedZDropThreshold
			&& row.ExpectsStopProtectionCall);
		Assert.Contains(report.PacketReturnReasons, row =>
			row.Reason == PlayerProtectionActiveTaskStopTriggerTraceArtifactPacketReturnReason.CmCompositeInvalidAfterStop
			&& row.ExpectsStopProtectionCall);
		Assert.Contains(report.PacketReturnReasons, row =>
			row.Reason == PlayerProtectionActiveTaskStopTriggerTraceArtifactPacketReturnReason.CmEmotionStanceRejectionReturn
			&& !row.ExpectsStopProtectionCall);
		Assert.Contains(report.PacketReturnReasons, row =>
			row.Reason == PlayerProtectionActiveTaskStopTriggerTraceArtifactPacketReturnReason.TeleportAnimationDoneNoPendingRunnableTask
			&& row.PacketName == "CM_TELEPORT_ANIMATION_DONE"
			&& !row.ExpectsStopProtectionCall);
		Assert.Contains(report.PacketReturnReasons, row =>
			row.Reason == PlayerProtectionActiveTaskStopTriggerTraceArtifactPacketReturnReason.TeleportAnimationDoneMissingInstanceFallback
			&& row.Notes.Contains("without position set", StringComparison.Ordinal)
			&& !row.ExpectsStopProtectionCall);
		Assert.Contains(report.PacketReturnReasons, row =>
			row.Reason == PlayerProtectionActiveTaskStopTriggerTraceArtifactPacketReturnReason.TeleportSpawnOnSameMapProtectionStartSkip
			&& row.JavaSource == "TeleportService.spawnOnSameMap"
			&& !row.ExpectsStopProtectionCall);
	}

	[Fact]
	public void Create_DocumentsInstrumentationCaveatsThatProtectJavaTiming()
	{
		var report = PlayerProtectionActiveTaskStopTriggerTraceArtifactSchemaReportService.Create(CreateRuntimeDesign());

		Assert.Contains(report.InstrumentationCaveats, row =>
			row.Caveat.Contains("Do not call Future.isDone", StringComparison.Ordinal));
		Assert.Contains(report.InstrumentationCaveats, row =>
			row.Caveat.Contains("Do not add synchronization", StringComparison.Ordinal));
		Assert.Contains(report.InstrumentationCaveats, row =>
			row.Caveat.Contains("Tag scheduled callback stops separately", StringComparison.Ordinal));
		Assert.Contains(report.InstrumentationCaveats, row =>
			row.Caveat.Contains("without rounding", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_RemainsBlockedUntilJavaInstrumentationAndTraceSerializerExist()
	{
		var report = PlayerProtectionActiveTaskStopTriggerTraceArtifactSchemaReportService.Create(CreateRuntimeDesign());

		Assert.True(report.RequiresJavaInstrumentation);
		Assert.True(report.RequiresTraceSerializer);
		Assert.False(report.ReadyForRuntimeComparison);
		Assert.All(report.Phases, row => Assert.Equal(PlayerProtectionActiveTaskStopTriggerTraceArtifactStatus.BlockedMissingJavaInstrumentation, row.Status));
		Assert.All(report.Fields, row => Assert.Equal(PlayerProtectionActiveTaskStopTriggerTraceArtifactStatus.BlockedMissingTraceSerializer, row.Status));
	}

	private static PlayerProtectionActiveTaskStopTriggerRuntimeComparisonDesignReport CreateRuntimeDesign() =>
		PlayerProtectionActiveTaskStopTriggerRuntimeComparisonDesignReportService.Create(CreateDetailedSummary());

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
