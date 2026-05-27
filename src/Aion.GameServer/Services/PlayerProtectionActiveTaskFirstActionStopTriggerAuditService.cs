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
	float PacketZ);

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
		AddCmMoveInAir(rows);
		AddPendingActionCaller(rows, PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmAttack, "CM_ATTACK.runImpl", "after dead-player guard");
		AddPendingActionCaller(rows, PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmCastSpell, "CM_CASTSPELL.runImpl", "after dead, zero spell, pet order, template, and passive checks");
		AddPendingActionCaller(rows, PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmCompositeStones, "CM_COMPOSITE_STONES.runImpl", "after null-player guard");
		AddPendingActionCaller(rows, PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmDialogSelect, "CM_DIALOG_SELECT.runImpl", "before trading and dialog validation");
		AddPendingActionCaller(rows, PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmEmotion, "CM_EMOTION.runImpl", "near the end of the handled emotion flow");
		AddPendingActionCaller(rows, PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmShowDialog, "CM_SHOW_DIALOG.runImpl", "before trading and NPC validation");
		AddPendingActionCaller(rows, PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmUseItem, "CM_USE_ITEM.runImpl", "before item lookup and restriction checks");
		AddProductionBoundary(rows);

		var rowArray = rows.ToArray();

		return new PlayerProtectionActiveTaskFirstActionStopTriggerAuditReport(
			rowArray,
			HasCmMoveThresholdEvidence: rowArray.Any(row => row.Source == PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmMove),
			HasCmMoveInAirUnconditionalEvidence: rowArray.Any(row => row.Source == PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmMoveInAir),
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

	private static void AddCmMoveInAir(ICollection<PlayerProtectionActiveTaskFirstActionStopTriggerAuditRow> rows) =>
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
