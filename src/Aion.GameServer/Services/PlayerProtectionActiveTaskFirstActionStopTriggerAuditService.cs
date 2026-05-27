namespace Aion.GameServer.Services;

public enum PlayerProtectionActiveTaskFirstActionStopTriggerSource
{
	CmMove,
	CmMoveInAir,
	CmAttack,
	CmCastSpell,
	CmCompositeStones,
	CmDialogSelect,
	CmEmotion,
	CmShowDialog,
	CmUseItem,
	ProductionWiring,
}

public enum PlayerProtectionActiveTaskFirstActionStopTriggerStatus
{
	WouldStopProtection,
	SkippedByJavaBranch,
	PendingAudit,
	BlockedProductionWiring,
}

public enum PlayerProtectionActiveTaskFirstActionStopTriggerRowKind
{
	MovementThreshold,
	UnconditionalPacketStop,
	ActionPacketStop,
	ProductionBoundary,
}

public sealed record PlayerProtectionActiveTaskFirstActionStopTriggerAuditRequest(
	bool PlayerSpawned,
	bool AntiHackAccepted,
	bool TeleportationModeAbsoluteMove,
	bool PlayerProtectionActive,
	float CurrentX,
	float CurrentY,
	float CurrentZ,
	float PacketX,
	float PacketY,
	float PacketZ,
	bool EvaluateCmMoveInAir = false,
	bool MoveInAirPlayerSpawned = true,
	bool MoveInAirPlayerFlying = true,
	bool MoveInAirProtectionActive = true,
	bool EvaluateCmAttack = false,
	bool CmAttackPlayerDead = false,
	bool CmAttackProtectionActive = true,
	bool EvaluateCmCastSpell = false,
	bool CmCastSpellPlayerDead = false,
	bool CmCastSpellIdZero = false,
	bool CmCastSpellPetOrderWithoutPet = false,
	bool CmCastSpellTemplateMissingOrPassive = false,
	bool CmCastSpellProtectionActive = true,
	bool EvaluateCmUseItem = false,
	bool CmUseItemProtectionActive = true,
	bool EvaluateCmShowDialog = false,
	bool CmShowDialogProtectionActive = true,
	bool EvaluateCmDialogSelect = false,
	bool CmDialogSelectProtectionActive = true);

public sealed record PlayerProtectionActiveTaskFirstActionStopTriggerAuditRow(
	int Order,
	PlayerProtectionActiveTaskFirstActionStopTriggerSource Source,
	PlayerProtectionActiveTaskFirstActionStopTriggerRowKind Kind,
	PlayerProtectionActiveTaskFirstActionStopTriggerStatus Status,
	bool JavaCallReached,
	bool WouldStopProtection,
	string JavaOperation,
	string JavaSource,
	string CSharpTarget,
	string Notes);

public sealed record PlayerProtectionActiveTaskFirstActionStopTriggerAuditReport(
	IReadOnlyList<PlayerProtectionActiveTaskFirstActionStopTriggerAuditRow> Rows,
	bool HasCmMoveThresholdEvidence,
	bool HasCmMoveInAirUnconditionalEvidence,
	bool HasCmMoveInAirOrderingEvidence,
	bool HasPendingCallerSurface,
	bool TriggersStopProtection,
	bool WiresProductionHandlers,
	string JavaSource,
	bool IsLive);

public static class PlayerProtectionActiveTaskFirstActionStopTriggerAuditService
{
	public static PlayerProtectionActiveTaskFirstActionStopTriggerAuditReport Create(
		PlayerProtectionActiveTaskFirstActionStopTriggerAuditRequest request)
	{
		var rows = new List<PlayerProtectionActiveTaskFirstActionStopTriggerAuditRow>();

		AddCmMove(rows, request);
		AddCmMoveInAir(rows, request);
		AddCmAttack(rows, request);
		AddCmCastSpell(rows, request);
		AddPendingActionCaller(rows, PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmCompositeStones, "CM_COMPOSITE_STONES.runImpl", "after null-player guard");
		AddCmDialogSelect(rows, request);
		AddPendingActionCaller(rows, PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmEmotion, "CM_EMOTION.runImpl", "near the end of the handled emotion flow");
		AddCmShowDialog(rows, request);
		AddCmUseItem(rows, request);
		AddProductionBoundary(rows);

		var rowArray = rows.ToArray();

		return new PlayerProtectionActiveTaskFirstActionStopTriggerAuditReport(
			rowArray,
			HasCmMoveThresholdEvidence: rowArray.Any(row => row.Source == PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmMove),
			HasCmMoveInAirUnconditionalEvidence: rowArray.Any(row => row.Source == PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmMoveInAir),
			HasCmMoveInAirOrderingEvidence: rowArray.Any(row =>
				row.Source == PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmMoveInAir
				&& row.Status != PlayerProtectionActiveTaskFirstActionStopTriggerStatus.PendingAudit),
			HasPendingCallerSurface: rowArray.Any(row => row.Status == PlayerProtectionActiveTaskFirstActionStopTriggerStatus.PendingAudit),
			TriggersStopProtection: rowArray.Any(row => row.WouldStopProtection),
			WiresProductionHandlers: false,
			"PlayerController.stopProtectionActiveTask first-action packet caller audit",
			IsLive: false);
	}

	private static void AddCmMove(
		ICollection<PlayerProtectionActiveTaskFirstActionStopTriggerAuditRow> rows,
		PlayerProtectionActiveTaskFirstActionStopTriggerAuditRequest request)
	{
		var javaCallReached = request.PlayerSpawned
			&& request.AntiHackAccepted
			&& !request.TeleportationModeAbsoluteMove
			&& request.PlayerProtectionActive;
		var xChanged = request.CurrentX != request.PacketX;
		var yChanged = request.CurrentY != request.PacketY;
		var zDroppedPastJavaThreshold = request.CurrentZ > request.PacketZ + 0.5f;
		var wouldStop = javaCallReached && (xChanged || yChanged || zDroppedPastJavaThreshold);

		var note = !javaCallReached
			? "Java returns before the protection stop condition when spawn, anti-hack, teleportation absolute-move, or active-protection prerequisites fail."
			: wouldStop
				? $"Accepted CM_MOVE would stop protection because xChanged={xChanged}, yChanged={yChanged}, oldZGreaterThanPacketZPlusHalf={zDroppedPastJavaThreshold}."
				: "Accepted CM_MOVE keeps protection because x/y are exactly unchanged and old server Z is not greater than packet Z plus 0.5.";

		Add(
			rows,
			PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmMove,
			PlayerProtectionActiveTaskFirstActionStopTriggerRowKind.MovementThreshold,
			wouldStop
				? PlayerProtectionActiveTaskFirstActionStopTriggerStatus.WouldStopProtection
				: PlayerProtectionActiveTaskFirstActionStopTriggerStatus.SkippedByJavaBranch,
			javaCallReached,
			wouldStop,
			"if (player.isProtectionActive() && (player.getX() != x || player.getY() != y || player.getZ() > z + 0.5f)) stopProtectionActiveTask()",
			"CM_MOVE.runImpl",
			"future CM_MOVE protection-stop hook",
			note);
	}

	private static void AddCmMoveInAir(
		ICollection<PlayerProtectionActiveTaskFirstActionStopTriggerAuditRow> rows,
		PlayerProtectionActiveTaskFirstActionStopTriggerAuditRequest request)
	{
		if (!request.EvaluateCmMoveInAir)
		{
			Add(
				rows,
				PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmMoveInAir,
				PlayerProtectionActiveTaskFirstActionStopTriggerRowKind.UnconditionalPacketStop,
				PlayerProtectionActiveTaskFirstActionStopTriggerStatus.PendingAudit,
				javaCallReached: false,
				wouldStopProtection: false,
				"if (player.isProtectionActive()) stopProtectionActiveTask()",
				"CM_MOVE_IN_AIR.runImpl",
				"future CM_MOVE_IN_AIR protection-stop hook",
				"Java stops unconditionally after spawned/flying guards; this row is cataloged for the next detailed packet-order audit.");
			return;
		}

		var javaCallReached = request.MoveInAirPlayerSpawned
			&& request.MoveInAirPlayerFlying
			&& request.MoveInAirProtectionActive;
		var note = javaCallReached
			? "Java CM_MOVE_IN_AIR reaches unconditional protection stop after spawned/flying guards and before World.updatePosition, onMoveFromClient, and onMove."
			: !request.MoveInAirPlayerSpawned
				? "Java CM_MOVE_IN_AIR returns at the spawned guard before flying, protection, distance, or world-position handling."
				: !request.MoveInAirPlayerFlying
					? "Java CM_MOVE_IN_AIR returns at the flying guard before protection stop, distance update, or world-position handling."
					: "Java CM_MOVE_IN_AIR reaches spawned/flying path but skips stopProtectionActiveTask because protection is not active.";

		Add(
			rows,
			PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmMoveInAir,
			PlayerProtectionActiveTaskFirstActionStopTriggerRowKind.UnconditionalPacketStop,
			javaCallReached
				? PlayerProtectionActiveTaskFirstActionStopTriggerStatus.WouldStopProtection
				: PlayerProtectionActiveTaskFirstActionStopTriggerStatus.SkippedByJavaBranch,
			javaCallReached,
			javaCallReached,
			"if (player.isProtectionActive()) stopProtectionActiveTask()",
			"CM_MOVE_IN_AIR.runImpl",
			"future CM_MOVE_IN_AIR protection-stop hook",
			note);
	}

	private static void AddPendingActionCaller(
		ICollection<PlayerProtectionActiveTaskFirstActionStopTriggerAuditRow> rows,
		PlayerProtectionActiveTaskFirstActionStopTriggerSource source,
		string javaSource,
		string javaOrdering) =>
		Add(
			rows,
			source,
			PlayerProtectionActiveTaskFirstActionStopTriggerRowKind.ActionPacketStop,
			PlayerProtectionActiveTaskFirstActionStopTriggerStatus.PendingAudit,
			javaCallReached: false,
			wouldStopProtection: false,
			"if (player.isProtectionActive()) stopProtectionActiveTask()",
			javaSource,
			$"future {source} protection-stop hook",
			$"Direct Java caller discovered {javaOrdering}; exact production ordering and packet side effects still need a class-specific audit.");

	private static void AddCmAttack(
		ICollection<PlayerProtectionActiveTaskFirstActionStopTriggerAuditRow> rows,
		PlayerProtectionActiveTaskFirstActionStopTriggerAuditRequest request)
	{
		if (!request.EvaluateCmAttack)
		{
			AddPendingActionCaller(rows, PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmAttack, "CM_ATTACK.runImpl", "after dead-player guard");
			return;
		}

		var javaCallReached = !request.CmAttackPlayerDead && request.CmAttackProtectionActive;
		var note = request.CmAttackPlayerDead
			? "Java CM_ATTACK returns at the dead-player guard before protection stop, known-list lookup, or attackTarget."
			: request.CmAttackProtectionActive
				? "Java CM_ATTACK stops protection after the dead-player guard and before known-list target lookup or attackTarget."
				: "Java CM_ATTACK reaches the attack path but skips stopProtectionActiveTask because protection is not active.";

		Add(
			rows,
			PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmAttack,
			PlayerProtectionActiveTaskFirstActionStopTriggerRowKind.ActionPacketStop,
			javaCallReached
				? PlayerProtectionActiveTaskFirstActionStopTriggerStatus.WouldStopProtection
				: PlayerProtectionActiveTaskFirstActionStopTriggerStatus.SkippedByJavaBranch,
			javaCallReached,
			javaCallReached,
			"if (player.isProtectionActive()) stopProtectionActiveTask()",
			"CM_ATTACK.runImpl",
			"future CM_ATTACK protection-stop hook",
			note);
	}

	private static void AddCmCastSpell(
		ICollection<PlayerProtectionActiveTaskFirstActionStopTriggerAuditRow> rows,
		PlayerProtectionActiveTaskFirstActionStopTriggerAuditRequest request)
	{
		if (!request.EvaluateCmCastSpell)
		{
			AddPendingActionCaller(rows, PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmCastSpell, "CM_CASTSPELL.runImpl", "after dead, zero spell, pet order, template, and passive checks");
			return;
		}

		var preconditionSkipped = request.CmCastSpellPlayerDead
			|| request.CmCastSpellIdZero
			|| request.CmCastSpellPetOrderWithoutPet
			|| request.CmCastSpellTemplateMissingOrPassive;
		var javaCallReached = !preconditionSkipped && request.CmCastSpellProtectionActive;
		var note = request.CmCastSpellPlayerDead
			? "Java CM_CASTSPELL sends cannot-cast message and returns at the dead-player guard before protection stop."
			: request.CmCastSpellIdZero
				? "Java CM_CASTSPELL cancels the current skill and returns when spellid is zero before protection stop."
				: request.CmCastSpellPetOrderWithoutPet
					? "Java CM_CASTSPELL sends pet-required message and returns for invalid pet-order skills before protection stop."
					: request.CmCastSpellTemplateMissingOrPassive
						? "Java CM_CASTSPELL returns for missing or passive skill templates before protection stop."
						: request.CmCastSpellProtectionActive
							? "Java CM_CASTSPELL stops protection after dead, zero-spell, pet-order, template, and passive guards, then cancels item use."
							: "Java CM_CASTSPELL reaches the cast path but skips stopProtectionActiveTask because protection is not active, then cancels item use.";

		Add(
			rows,
			PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmCastSpell,
			PlayerProtectionActiveTaskFirstActionStopTriggerRowKind.ActionPacketStop,
			javaCallReached
				? PlayerProtectionActiveTaskFirstActionStopTriggerStatus.WouldStopProtection
				: PlayerProtectionActiveTaskFirstActionStopTriggerStatus.SkippedByJavaBranch,
			javaCallReached,
			javaCallReached,
			"if (player.isProtectionActive()) stopProtectionActiveTask(); player.getController().cancelUseItem()",
			"CM_CASTSPELL.runImpl",
			"future CM_CASTSPELL protection-stop hook",
			note);
	}

	private static void AddCmUseItem(
		ICollection<PlayerProtectionActiveTaskFirstActionStopTriggerAuditRow> rows,
		PlayerProtectionActiveTaskFirstActionStopTriggerAuditRequest request)
	{
		if (!request.EvaluateCmUseItem)
		{
			AddPendingActionCaller(rows, PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmUseItem, "CM_USE_ITEM.runImpl", "before item lookup and restriction checks");
			return;
		}

		var note = request.CmUseItemProtectionActive
			? "Java CM_USE_ITEM stops protection immediately after resolving the active player and before source item lookup, target item lookup, casting cancellation, restrictions, quest item-use callback, cooldown, observers, or action execution."
			: "Java CM_USE_ITEM reaches the item-use path but skips stopProtectionActiveTask because protection is not active, then continues to source item lookup and later item-use guards.";

		Add(
			rows,
			PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmUseItem,
			PlayerProtectionActiveTaskFirstActionStopTriggerRowKind.ActionPacketStop,
			request.CmUseItemProtectionActive
				? PlayerProtectionActiveTaskFirstActionStopTriggerStatus.WouldStopProtection
				: PlayerProtectionActiveTaskFirstActionStopTriggerStatus.SkippedByJavaBranch,
			request.CmUseItemProtectionActive,
			request.CmUseItemProtectionActive,
			"if (player.isProtectionActive()) stopProtectionActiveTask()",
			"CM_USE_ITEM.runImpl",
			"future CM_USE_ITEM protection-stop hook",
			note);
	}

	private static void AddCmShowDialog(
		ICollection<PlayerProtectionActiveTaskFirstActionStopTriggerAuditRow> rows,
		PlayerProtectionActiveTaskFirstActionStopTriggerAuditRequest request)
	{
		if (!request.EvaluateCmShowDialog)
		{
			AddPendingActionCaller(rows, PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmShowDialog, "CM_SHOW_DIALOG.runImpl", "before trading and NPC validation");
			return;
		}

		var note = request.CmShowDialogProtectionActive
			? "Java CM_SHOW_DIALOG stops protection immediately after resolving the active player and before the trading guard, known-list NPC lookup, hide removal, or onDialogRequest."
			: "Java CM_SHOW_DIALOG reaches the dialog request path but skips stopProtectionActiveTask because protection is not active, then evaluates trading and NPC validation.";

		Add(
			rows,
			PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmShowDialog,
			PlayerProtectionActiveTaskFirstActionStopTriggerRowKind.ActionPacketStop,
			request.CmShowDialogProtectionActive
				? PlayerProtectionActiveTaskFirstActionStopTriggerStatus.WouldStopProtection
				: PlayerProtectionActiveTaskFirstActionStopTriggerStatus.SkippedByJavaBranch,
			request.CmShowDialogProtectionActive,
			request.CmShowDialogProtectionActive,
			"if (player.isProtectionActive()) stopProtectionActiveTask()",
			"CM_SHOW_DIALOG.runImpl",
			"future CM_SHOW_DIALOG protection-stop hook",
			note);
	}

	private static void AddCmDialogSelect(
		ICollection<PlayerProtectionActiveTaskFirstActionStopTriggerAuditRow> rows,
		PlayerProtectionActiveTaskFirstActionStopTriggerAuditRequest request)
	{
		if (!request.EvaluateCmDialogSelect)
		{
			AddPendingActionCaller(rows, PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmDialogSelect, "CM_DIALOG_SELECT.runImpl", "before trading and dialog validation");
			return;
		}

		var note = request.CmDialogSelectProtectionActive
			? "Java CM_DIALOG_SELECT stops protection immediately after resolving the active player and before the trading guard, admin dialog-info message, action-name lookup, quest handling, NPC validation, or controller onDialogSelect."
			: "Java CM_DIALOG_SELECT reaches the dialog select path but skips stopProtectionActiveTask because protection is not active, then evaluates trading and dialog validation.";

		Add(
			rows,
			PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmDialogSelect,
			PlayerProtectionActiveTaskFirstActionStopTriggerRowKind.ActionPacketStop,
			request.CmDialogSelectProtectionActive
				? PlayerProtectionActiveTaskFirstActionStopTriggerStatus.WouldStopProtection
				: PlayerProtectionActiveTaskFirstActionStopTriggerStatus.SkippedByJavaBranch,
			request.CmDialogSelectProtectionActive,
			request.CmDialogSelectProtectionActive,
			"if (player.isProtectionActive()) stopProtectionActiveTask()",
			"CM_DIALOG_SELECT.runImpl",
			"future CM_DIALOG_SELECT protection-stop hook",
			note);
	}

	private static void AddProductionBoundary(ICollection<PlayerProtectionActiveTaskFirstActionStopTriggerAuditRow> rows) =>
		Add(
			rows,
			PlayerProtectionActiveTaskFirstActionStopTriggerSource.ProductionWiring,
			PlayerProtectionActiveTaskFirstActionStopTriggerRowKind.ProductionBoundary,
			PlayerProtectionActiveTaskFirstActionStopTriggerStatus.BlockedProductionWiring,
			javaCallReached: false,
			wouldStopProtection: false,
			"client packet runImpl -> player.getController().stopProtectionActiveTask()",
			"CM_MOVE / CM_MOVE_IN_AIR / action packet callers",
			"future production packet-handler protection-stop integration",
			"Non-live audit only; production handlers stay disabled until packet ordering, controller task-map, scheduler, and runtime comparison gates are closed.");

	private static void Add(
		ICollection<PlayerProtectionActiveTaskFirstActionStopTriggerAuditRow> rows,
		PlayerProtectionActiveTaskFirstActionStopTriggerSource source,
		PlayerProtectionActiveTaskFirstActionStopTriggerRowKind kind,
		PlayerProtectionActiveTaskFirstActionStopTriggerStatus status,
		bool javaCallReached,
		bool wouldStopProtection,
		string javaOperation,
		string javaSource,
		string cSharpTarget,
		string notes)
	{
		rows.Add(new PlayerProtectionActiveTaskFirstActionStopTriggerAuditRow(
			rows.Count + 1,
			source,
			kind,
			status,
			javaCallReached,
			wouldStopProtection,
			javaOperation,
			javaSource,
			cSharpTarget,
			notes));
	}
}
