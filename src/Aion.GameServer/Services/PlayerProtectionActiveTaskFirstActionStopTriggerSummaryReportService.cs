namespace Aion.GameServer.Services;

public enum PlayerProtectionActiveTaskFirstActionStopTriggerSummaryCategory
{
	ThresholdedMovement,
	AcceptedAirMovement,
	EarlyActionStop,
	EarlyAfterNullGuardActionStop,
	LateGuardedEmotionStop,
	ProductionBoundary,
	RuntimeComparisonBlocker,
}

public enum PlayerProtectionActiveTaskFirstActionStopTriggerSummaryStatus
{
	ModeledNotLive,
	PendingDetailedAudit,
	BlockedProductionWiring,
	NeedsRuntimeVerification,
}

public sealed record PlayerProtectionActiveTaskFirstActionStopTriggerSummaryRow(
	int Order,
	PlayerProtectionActiveTaskFirstActionStopTriggerSource Source,
	PlayerProtectionActiveTaskFirstActionStopTriggerSummaryCategory Category,
	PlayerProtectionActiveTaskFirstActionStopTriggerSummaryStatus Status,
	string JavaSource,
	string CSharpTarget,
	string Notes);

public sealed record PlayerProtectionActiveTaskFirstActionStopTriggerSummaryReport(
	IReadOnlyList<PlayerProtectionActiveTaskFirstActionStopTriggerSummaryRow> Rows,
	bool HasAllKnownPacketSources,
	bool HasThresholdedMovementSource,
	bool HasEarlyActionStopSources,
	bool HasLateGuardedEmotionSource,
	bool HasProductionWiringBlocker,
	bool HasRuntimeComparisonBlocker,
	bool ReadyForProductionPacketStopWiring,
	string JavaSource,
	bool IsLive);

public static class PlayerProtectionActiveTaskFirstActionStopTriggerSummaryReportService
{
	private static readonly PlayerProtectionActiveTaskFirstActionStopTriggerSource[] KnownPacketSources =
	[
		PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmMove,
		PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmMoveInAir,
		PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmAttack,
		PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmCastSpell,
		PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmCompositeStones,
		PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmDialogSelect,
		PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmEmotion,
		PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmShowDialog,
		PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmUseItem,
	];

	public static PlayerProtectionActiveTaskFirstActionStopTriggerSummaryReport Create(
		PlayerProtectionActiveTaskFirstActionStopTriggerAuditReport audit)
	{
		var rows = new List<PlayerProtectionActiveTaskFirstActionStopTriggerSummaryRow>();
		foreach (var row in audit.Rows.Where(row => row.Source != PlayerProtectionActiveTaskFirstActionStopTriggerSource.ProductionWiring))
		{
			Add(rows, row);
		}

		AddProductionBoundary(rows, audit);
		AddRuntimeComparisonBoundary(rows);

		var rowArray = rows.ToArray();
		var sources = audit.Rows.Select(row => row.Source).ToHashSet();
		var hasAllSources = KnownPacketSources.All(sources.Contains);
		var hasPending = rowArray.Any(row => row.Status == PlayerProtectionActiveTaskFirstActionStopTriggerSummaryStatus.PendingDetailedAudit);
		var hasBlocker = rowArray.Any(row => row.Status is PlayerProtectionActiveTaskFirstActionStopTriggerSummaryStatus.BlockedProductionWiring or PlayerProtectionActiveTaskFirstActionStopTriggerSummaryStatus.NeedsRuntimeVerification);

		return new PlayerProtectionActiveTaskFirstActionStopTriggerSummaryReport(
			rowArray,
			HasAllKnownPacketSources: hasAllSources,
			HasThresholdedMovementSource: rowArray.Any(row => row.Category == PlayerProtectionActiveTaskFirstActionStopTriggerSummaryCategory.ThresholdedMovement),
			HasEarlyActionStopSources: rowArray.Any(row => row.Category is PlayerProtectionActiveTaskFirstActionStopTriggerSummaryCategory.EarlyActionStop or PlayerProtectionActiveTaskFirstActionStopTriggerSummaryCategory.EarlyAfterNullGuardActionStop),
			HasLateGuardedEmotionSource: rowArray.Any(row => row.Category == PlayerProtectionActiveTaskFirstActionStopTriggerSummaryCategory.LateGuardedEmotionStop),
			HasProductionWiringBlocker: rowArray.Any(row => row.Status == PlayerProtectionActiveTaskFirstActionStopTriggerSummaryStatus.BlockedProductionWiring),
			HasRuntimeComparisonBlocker: rowArray.Any(row => row.Status == PlayerProtectionActiveTaskFirstActionStopTriggerSummaryStatus.NeedsRuntimeVerification),
			ReadyForProductionPacketStopWiring: audit.IsLive && audit.WiresProductionHandlers && hasAllSources && !hasPending && !hasBlocker,
			"first-action protection stop trigger summary over Java packet callers",
			IsLive: false);
	}

	private static void Add(
		ICollection<PlayerProtectionActiveTaskFirstActionStopTriggerSummaryRow> rows,
		PlayerProtectionActiveTaskFirstActionStopTriggerAuditRow auditRow)
	{
		rows.Add(new PlayerProtectionActiveTaskFirstActionStopTriggerSummaryRow(
			rows.Count + 1,
			auditRow.Source,
			CategoryFor(auditRow.Source),
			auditRow.Status == PlayerProtectionActiveTaskFirstActionStopTriggerStatus.PendingAudit
				? PlayerProtectionActiveTaskFirstActionStopTriggerSummaryStatus.PendingDetailedAudit
				: PlayerProtectionActiveTaskFirstActionStopTriggerSummaryStatus.ModeledNotLive,
			auditRow.JavaSource,
			auditRow.CSharpTarget,
			NotesFor(auditRow.Source, auditRow.Notes)));
	}

	private static void AddProductionBoundary(
		ICollection<PlayerProtectionActiveTaskFirstActionStopTriggerSummaryRow> rows,
		PlayerProtectionActiveTaskFirstActionStopTriggerAuditReport audit)
	{
		rows.Add(new PlayerProtectionActiveTaskFirstActionStopTriggerSummaryRow(
			rows.Count + 1,
			PlayerProtectionActiveTaskFirstActionStopTriggerSource.ProductionWiring,
			PlayerProtectionActiveTaskFirstActionStopTriggerSummaryCategory.ProductionBoundary,
			PlayerProtectionActiveTaskFirstActionStopTriggerSummaryStatus.BlockedProductionWiring,
			audit.JavaSource,
			"future packet-handler stopProtectionActiveTask wiring",
			"Production packet-handler stop wiring remains disabled; summary is metadata-only and does not call controller stop hooks."));
	}

	private static void AddRuntimeComparisonBoundary(ICollection<PlayerProtectionActiveTaskFirstActionStopTriggerSummaryRow> rows)
	{
		rows.Add(new PlayerProtectionActiveTaskFirstActionStopTriggerSummaryRow(
			rows.Count + 1,
			PlayerProtectionActiveTaskFirstActionStopTriggerSource.ProductionWiring,
			PlayerProtectionActiveTaskFirstActionStopTriggerSummaryCategory.RuntimeComparisonBlocker,
			PlayerProtectionActiveTaskFirstActionStopTriggerSummaryStatus.NeedsRuntimeVerification,
			"Java runtime packet traces / PlayerController.stopProtectionActiveTask side effects",
			"future Java/C# runtime comparison artifacts",
			"Java runtime comparison is still required for packet ordering, stop callback side effects, task-map cancellation, and scheduler/concurrency behavior."));
	}

	private static PlayerProtectionActiveTaskFirstActionStopTriggerSummaryCategory CategoryFor(
		PlayerProtectionActiveTaskFirstActionStopTriggerSource source) =>
		source switch
		{
			PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmMove => PlayerProtectionActiveTaskFirstActionStopTriggerSummaryCategory.ThresholdedMovement,
			PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmMoveInAir => PlayerProtectionActiveTaskFirstActionStopTriggerSummaryCategory.AcceptedAirMovement,
			PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmCompositeStones => PlayerProtectionActiveTaskFirstActionStopTriggerSummaryCategory.EarlyAfterNullGuardActionStop,
			PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmEmotion => PlayerProtectionActiveTaskFirstActionStopTriggerSummaryCategory.LateGuardedEmotionStop,
			_ => PlayerProtectionActiveTaskFirstActionStopTriggerSummaryCategory.EarlyActionStop,
		};

	private static string NotesFor(PlayerProtectionActiveTaskFirstActionStopTriggerSource source, string auditNotes) =>
		source switch
		{
			PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmMove => $"Thresholded movement source; keep exact x/y float inequality and asymmetric z-drop threshold. {auditNotes}",
			PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmMoveInAir => $"Accepted air-movement source; stop is unconditional after spawned/flying guards. {auditNotes}",
			PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmCompositeStones => $"Early action source after null-player guard; invalid later composition branches may still have stopped protection. {auditNotes}",
			PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmEmotion => $"Late guarded emotion source; many early returns do not stop protection and side effects may occur before stop. {auditNotes}",
			_ => $"Early action source before core packet validation/side effects. {auditNotes}",
		};
}
