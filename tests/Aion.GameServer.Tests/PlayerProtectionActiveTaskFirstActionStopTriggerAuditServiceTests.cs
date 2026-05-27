using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerProtectionActiveTaskFirstActionStopTriggerAuditServiceTests
{
	[Fact]
	public void Create_CmMoveXChangeStopsProtection()
	{
		var report = PlayerProtectionActiveTaskFirstActionStopTriggerAuditService.Create(CreateRequest(packetX: 101f));

		Assert.False(report.IsLive);
		Assert.False(report.WiresProductionHandlers);
		Assert.True(report.HasCmMoveThresholdEvidence);
		Assert.True(report.TriggersStopProtection);
		Assert.Contains(report.Rows, row =>
			row.Source == PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmMove
			&& row.Status == PlayerProtectionActiveTaskFirstActionStopTriggerStatus.WouldStopProtection
			&& row.JavaCallReached
			&& row.WouldStopProtection
			&& row.Notes.Contains("xChanged=True", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_CmMoveYChangeStopsProtection()
	{
		var report = PlayerProtectionActiveTaskFirstActionStopTriggerAuditService.Create(CreateRequest(packetY: 201f));

		Assert.True(report.TriggersStopProtection);
		Assert.Contains(report.Rows, row =>
			row.Source == PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmMove
			&& row.Status == PlayerProtectionActiveTaskFirstActionStopTriggerStatus.WouldStopProtection
			&& row.Notes.Contains("yChanged=True", StringComparison.Ordinal));
	}

	[Theory]
	[InlineData(49.4999f, true)]
	[InlineData(49.5f, false)]
	[InlineData(51f, false)]
	public void Create_CmMoveUsesJavaAsymmetricZDropThreshold(float packetZ, bool expectedStop)
	{
		var report = PlayerProtectionActiveTaskFirstActionStopTriggerAuditService.Create(CreateRequest(packetZ: packetZ));

		Assert.Equal(expectedStop, report.TriggersStopProtection);
		Assert.Contains(report.Rows, row =>
			row.Source == PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmMove
			&& row.WouldStopProtection == expectedStop
			&& row.Status == (expectedStop
				? PlayerProtectionActiveTaskFirstActionStopTriggerStatus.WouldStopProtection
				: PlayerProtectionActiveTaskFirstActionStopTriggerStatus.SkippedByJavaBranch));
	}

	[Fact]
	public void Create_CmMoveSamePositionHeadingTurnSkipsStop()
	{
		var report = PlayerProtectionActiveTaskFirstActionStopTriggerAuditService.Create(CreateRequest());

		Assert.False(report.TriggersStopProtection);
		Assert.Contains(report.Rows, row =>
			row.Source == PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmMove
			&& row.Status == PlayerProtectionActiveTaskFirstActionStopTriggerStatus.SkippedByJavaBranch
			&& row.JavaCallReached
			&& row.Notes.Contains("x/y are exactly unchanged", StringComparison.Ordinal));
	}

	[Theory]
	[InlineData(false, true, false, true)]
	[InlineData(true, false, false, true)]
	[InlineData(true, true, true, true)]
	[InlineData(true, true, false, false)]
	public void Create_CmMoveEarlyJavaBranchesSkipStop(
		bool spawned,
		bool antiHackAccepted,
		bool teleportationModeAbsoluteMove,
		bool protectionActive)
	{
		var report = PlayerProtectionActiveTaskFirstActionStopTriggerAuditService.Create(CreateRequest(
			packetX: 101f,
			spawned: spawned,
			antiHackAccepted: antiHackAccepted,
			teleportationModeAbsoluteMove: teleportationModeAbsoluteMove,
			protectionActive: protectionActive));

		Assert.False(report.TriggersStopProtection);
		Assert.Contains(report.Rows, row =>
			row.Source == PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmMove
			&& row.Status == PlayerProtectionActiveTaskFirstActionStopTriggerStatus.SkippedByJavaBranch
			&& !row.JavaCallReached
			&& row.Notes.Contains("returns before the protection stop condition", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_ListsMoveInAirAndActionPacketCallersAsPending()
	{
		var report = PlayerProtectionActiveTaskFirstActionStopTriggerAuditService.Create(CreateRequest());

		Assert.True(report.HasCmMoveInAirUnconditionalEvidence);
		Assert.False(report.HasCmMoveInAirOrderingEvidence);
		Assert.True(report.HasPendingCallerSurface);
		Assert.Contains(report.Rows, row =>
			row.Source == PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmMoveInAir
			&& row.Status == PlayerProtectionActiveTaskFirstActionStopTriggerStatus.PendingAudit
			&& row.JavaOperation == "if (player.isProtectionActive()) stopProtectionActiveTask()");
		Assert.Contains(report.Rows, row => row.Source == PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmAttack && row.Status == PlayerProtectionActiveTaskFirstActionStopTriggerStatus.PendingAudit);
		Assert.Contains(report.Rows, row => row.Source == PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmCastSpell && row.Status == PlayerProtectionActiveTaskFirstActionStopTriggerStatus.PendingAudit);
		Assert.Contains(report.Rows, row => row.Source == PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmCompositeStones && row.Status == PlayerProtectionActiveTaskFirstActionStopTriggerStatus.PendingAudit);
		Assert.Contains(report.Rows, row => row.Source == PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmDialogSelect && row.Status == PlayerProtectionActiveTaskFirstActionStopTriggerStatus.PendingAudit);
		Assert.Contains(report.Rows, row => row.Source == PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmEmotion && row.Status == PlayerProtectionActiveTaskFirstActionStopTriggerStatus.PendingAudit);
		Assert.Contains(report.Rows, row => row.Source == PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmShowDialog && row.Status == PlayerProtectionActiveTaskFirstActionStopTriggerStatus.PendingAudit);
		Assert.Contains(report.Rows, row => row.Source == PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmUseItem && row.Status == PlayerProtectionActiveTaskFirstActionStopTriggerStatus.PendingAudit);
	}

	[Fact]
	public void Create_CmMoveInAirSpawnedFlyingProtectedStopsBeforeWorldUpdate()
	{
		var report = PlayerProtectionActiveTaskFirstActionStopTriggerAuditService.Create(CreateRequest(
			evaluateCmMoveInAir: true));

		Assert.True(report.HasCmMoveInAirOrderingEvidence);
		Assert.True(report.TriggersStopProtection);
		Assert.Contains(report.Rows, row =>
			row.Source == PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmMoveInAir
			&& row.Status == PlayerProtectionActiveTaskFirstActionStopTriggerStatus.WouldStopProtection
			&& row.JavaCallReached
			&& row.WouldStopProtection
			&& row.Notes.Contains("before World.updatePosition", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_CmMoveInAirNotSpawnedSkipsBeforeFlyingAndProtection()
	{
		var report = PlayerProtectionActiveTaskFirstActionStopTriggerAuditService.Create(CreateRequest(
			evaluateCmMoveInAir: true,
			moveInAirPlayerSpawned: false));

		Assert.False(report.TriggersStopProtection);
		Assert.Contains(report.Rows, row =>
			row.Source == PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmMoveInAir
			&& row.Status == PlayerProtectionActiveTaskFirstActionStopTriggerStatus.SkippedByJavaBranch
			&& !row.JavaCallReached
			&& row.Notes.Contains("returns at the spawned guard", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_CmMoveInAirNotFlyingSkipsBeforeProtectionStop()
	{
		var report = PlayerProtectionActiveTaskFirstActionStopTriggerAuditService.Create(CreateRequest(
			evaluateCmMoveInAir: true,
			moveInAirPlayerFlying: false));

		Assert.False(report.TriggersStopProtection);
		Assert.Contains(report.Rows, row =>
			row.Source == PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmMoveInAir
			&& row.Status == PlayerProtectionActiveTaskFirstActionStopTriggerStatus.SkippedByJavaBranch
			&& !row.JavaCallReached
			&& row.Notes.Contains("returns at the flying guard", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_CmMoveInAirInactiveProtectionSkipsAfterSpawnedFlyingGuards()
	{
		var report = PlayerProtectionActiveTaskFirstActionStopTriggerAuditService.Create(CreateRequest(
			evaluateCmMoveInAir: true,
			moveInAirProtectionActive: false));

		Assert.False(report.TriggersStopProtection);
		Assert.Contains(report.Rows, row =>
			row.Source == PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmMoveInAir
			&& row.Status == PlayerProtectionActiveTaskFirstActionStopTriggerStatus.SkippedByJavaBranch
			&& !row.JavaCallReached
			&& row.Notes.Contains("protection is not active", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_CmAttackStopsAfterDeadGuardBeforeTargetLookup()
	{
		var report = PlayerProtectionActiveTaskFirstActionStopTriggerAuditService.Create(CreateRequest(
			evaluateCmAttack: true));

		Assert.True(report.TriggersStopProtection);
		Assert.Contains(report.Rows, row =>
			row.Source == PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmAttack
			&& row.Status == PlayerProtectionActiveTaskFirstActionStopTriggerStatus.WouldStopProtection
			&& row.JavaCallReached
			&& row.Notes.Contains("before known-list target lookup", StringComparison.Ordinal));
	}

	[Theory]
	[InlineData(true, true, "dead-player guard")]
	[InlineData(false, false, "protection is not active")]
	public void Create_CmAttackSkippedBranchesDoNotStop(
		bool playerDead,
		bool protectionActive,
		string expectedNote)
	{
		var report = PlayerProtectionActiveTaskFirstActionStopTriggerAuditService.Create(CreateRequest(
			evaluateCmAttack: true,
			cmAttackPlayerDead: playerDead,
			cmAttackProtectionActive: protectionActive));

		Assert.False(report.TriggersStopProtection);
		Assert.Contains(report.Rows, row =>
			row.Source == PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmAttack
			&& row.Status == PlayerProtectionActiveTaskFirstActionStopTriggerStatus.SkippedByJavaBranch
			&& !row.JavaCallReached
			&& row.Notes.Contains(expectedNote, StringComparison.Ordinal));
	}

	[Fact]
	public void Create_CmCastSpellStopsAfterPreconditionGuardsBeforeCancelUseItem()
	{
		var report = PlayerProtectionActiveTaskFirstActionStopTriggerAuditService.Create(CreateRequest(
			evaluateCmCastSpell: true));

		Assert.True(report.TriggersStopProtection);
		Assert.Contains(report.Rows, row =>
			row.Source == PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmCastSpell
			&& row.Status == PlayerProtectionActiveTaskFirstActionStopTriggerStatus.WouldStopProtection
			&& row.JavaCallReached
			&& row.JavaOperation.Contains("cancelUseItem", StringComparison.Ordinal)
			&& row.Notes.Contains("then cancels item use", StringComparison.Ordinal));
	}

	[Theory]
	[InlineData(true, false, false, false, true, "dead-player guard")]
	[InlineData(false, true, false, false, true, "spellid is zero")]
	[InlineData(false, false, true, false, true, "invalid pet-order skills")]
	[InlineData(false, false, false, true, true, "missing or passive skill templates")]
	[InlineData(false, false, false, false, false, "protection is not active")]
	public void Create_CmCastSpellSkippedBranchesDoNotStop(
		bool playerDead,
		bool spellIdZero,
		bool petOrderWithoutPet,
		bool templateMissingOrPassive,
		bool protectionActive,
		string expectedNote)
	{
		var report = PlayerProtectionActiveTaskFirstActionStopTriggerAuditService.Create(CreateRequest(
			evaluateCmCastSpell: true,
			cmCastSpellPlayerDead: playerDead,
			cmCastSpellIdZero: spellIdZero,
			cmCastSpellPetOrderWithoutPet: petOrderWithoutPet,
			cmCastSpellTemplateMissingOrPassive: templateMissingOrPassive,
			cmCastSpellProtectionActive: protectionActive));

		Assert.False(report.TriggersStopProtection);
		Assert.Contains(report.Rows, row =>
			row.Source == PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmCastSpell
			&& row.Status == PlayerProtectionActiveTaskFirstActionStopTriggerStatus.SkippedByJavaBranch
			&& !row.JavaCallReached
			&& row.Notes.Contains(expectedNote, StringComparison.Ordinal));
	}

	[Theory]
	[InlineData(PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmUseItem, "before source item lookup")]
	[InlineData(PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmShowDialog, "before the trading guard")]
	[InlineData(PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmDialogSelect, "before the trading guard")]
	public void Create_UseItemAndDialogPacketsStopBeforeJavaValidation(
		PlayerProtectionActiveTaskFirstActionStopTriggerSource source,
		string expectedNote)
	{
		var report = PlayerProtectionActiveTaskFirstActionStopTriggerAuditService.Create(CreateRequest(
			evaluateCmUseItem: source == PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmUseItem,
			evaluateCmShowDialog: source == PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmShowDialog,
			evaluateCmDialogSelect: source == PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmDialogSelect));

		Assert.True(report.TriggersStopProtection);
		Assert.Contains(report.Rows, row =>
			row.Source == source
			&& row.Status == PlayerProtectionActiveTaskFirstActionStopTriggerStatus.WouldStopProtection
			&& row.JavaCallReached
			&& row.WouldStopProtection
			&& row.Notes.Contains(expectedNote, StringComparison.Ordinal));
	}

	[Theory]
	[InlineData(PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmUseItem, "source item lookup")]
	[InlineData(PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmShowDialog, "trading and NPC validation")]
	[InlineData(PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmDialogSelect, "trading and dialog validation")]
	public void Create_UseItemAndDialogPacketsInactiveProtectionSkipsStop(
		PlayerProtectionActiveTaskFirstActionStopTriggerSource source,
		string expectedNote)
	{
		var report = PlayerProtectionActiveTaskFirstActionStopTriggerAuditService.Create(CreateRequest(
			evaluateCmUseItem: source == PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmUseItem,
			cmUseItemProtectionActive: false,
			evaluateCmShowDialog: source == PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmShowDialog,
			cmShowDialogProtectionActive: false,
			evaluateCmDialogSelect: source == PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmDialogSelect,
			cmDialogSelectProtectionActive: false));

		Assert.False(report.TriggersStopProtection);
		Assert.Contains(report.Rows, row =>
			row.Source == source
			&& row.Status == PlayerProtectionActiveTaskFirstActionStopTriggerStatus.SkippedByJavaBranch
			&& !row.JavaCallReached
			&& row.Notes.Contains(expectedNote, StringComparison.Ordinal));
	}

	[Fact]
	public void Create_RemainingCompositeAndEmotionCallersStayPendingAfterUseItemAndDialogAudit()
	{
		var report = PlayerProtectionActiveTaskFirstActionStopTriggerAuditService.Create(CreateRequest(
			evaluateCmUseItem: true,
			evaluateCmShowDialog: true,
			evaluateCmDialogSelect: true));

		Assert.True(report.HasPendingCallerSurface);
		Assert.Contains(report.Rows, row =>
			row.Source == PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmCompositeStones
			&& row.Status == PlayerProtectionActiveTaskFirstActionStopTriggerStatus.PendingAudit);
		Assert.Contains(report.Rows, row =>
			row.Source == PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmEmotion
			&& row.Status == PlayerProtectionActiveTaskFirstActionStopTriggerStatus.PendingAudit);
	}

	private static PlayerProtectionActiveTaskFirstActionStopTriggerAuditRequest CreateRequest(
		float packetX = CurrentX,
		float packetY = CurrentY,
		float packetZ = CurrentZ,
		bool spawned = true,
		bool antiHackAccepted = true,
		bool teleportationModeAbsoluteMove = false,
		bool protectionActive = true,
		bool evaluateCmMoveInAir = false,
		bool moveInAirPlayerSpawned = true,
		bool moveInAirPlayerFlying = true,
		bool moveInAirProtectionActive = true,
		bool evaluateCmAttack = false,
		bool cmAttackPlayerDead = false,
		bool cmAttackProtectionActive = true,
		bool evaluateCmCastSpell = false,
		bool cmCastSpellPlayerDead = false,
		bool cmCastSpellIdZero = false,
		bool cmCastSpellPetOrderWithoutPet = false,
		bool cmCastSpellTemplateMissingOrPassive = false,
		bool cmCastSpellProtectionActive = true,
		bool evaluateCmUseItem = false,
		bool cmUseItemProtectionActive = true,
		bool evaluateCmShowDialog = false,
		bool cmShowDialogProtectionActive = true,
		bool evaluateCmDialogSelect = false,
		bool cmDialogSelectProtectionActive = true) =>
		new(
			spawned,
			antiHackAccepted,
			teleportationModeAbsoluteMove,
			protectionActive,
			CurrentX,
			CurrentY,
			CurrentZ,
			packetX,
			packetY,
			packetZ,
			evaluateCmMoveInAir,
			moveInAirPlayerSpawned,
			moveInAirPlayerFlying,
			moveInAirProtectionActive,
			evaluateCmAttack,
			cmAttackPlayerDead,
			cmAttackProtectionActive,
			evaluateCmCastSpell,
			cmCastSpellPlayerDead,
			cmCastSpellIdZero,
			cmCastSpellPetOrderWithoutPet,
			cmCastSpellTemplateMissingOrPassive,
			cmCastSpellProtectionActive,
			evaluateCmUseItem,
			cmUseItemProtectionActive,
			evaluateCmShowDialog,
			cmShowDialogProtectionActive,
			evaluateCmDialogSelect,
			cmDialogSelectProtectionActive);

	private const float CurrentX = 100f;
	private const float CurrentY = 200f;
	private const float CurrentZ = 50f;
}
