namespace Aion.GameServer.Services;

public enum PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardArea
{
	JavaObserverCoverage,
	JavaHookDetailCoverage,
	JavaToolingAndArtifacts,
	CSharpEmitterCoverage,
	RuntimeEvidence,
	KeyProjection,
	RuntimeComparisonReadiness,
}

public enum PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardStatus
{
	SatisfiedByNonLiveMetadata,
	BlockedMissingJavaTooling,
	BlockedMissingJavaArtifacts,
	BlockedMissingCSharpEmitter,
	BlockedMissingRuntimeEvidence,
	BlockedComparisonNotExecuted,
}

public sealed record PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardRow(
	int Order,
	PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardArea Area,
	PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardStatus Status,
	bool BlocksRuntimeComparison,
	string JavaSource,
	string CSharpSource,
	string Evidence,
	string Notes);

public sealed record PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardReport(
	IReadOnlyList<PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardRow> Rows,
	bool HasJavaObserverCoverage,
	bool HasJavaToolingBlocker,
	bool HasCSharpEmitterCoverage,
	bool HasRuntimeEvidenceBlocker,
	bool HasKeyProjectionEvidence,
	bool HasReadinessEvidence,
	bool HasJavaHookDetailEvidence,
	int JavaHookDetailRowCount,
	bool NeedsProtectionArtifactSerializer,
	bool NeedsJavaObserverImplementation,
	bool NeedsJavaArtifacts,
	bool NeedsCSharpEmitter,
	bool NeedsRuntimeEvidence,
	bool NeedsComparisonExecution,
	bool ReadyForRuntimeComparison,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: non-live dashboard that composes protection stop-trigger observer,
/// generated-artifact, C# emitter, key projection, and readiness reports without enabling runtime hooks.
/// </summary>
public static class PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardReportService
{
	public static PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardReport Create(
		PlayerProtectionActiveTaskStopTriggerJavaObserverRunbookDesignReport javaObserverRunbookDesign,
		PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionPlanReport generatedArtifactExecutionPlan,
		PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterDesignReport csharpTraceEmitterDesign,
		PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessReport readinessReport,
		PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKeyProjectionReport? keyProjectionReport = null,
		PlayerProtectionActiveTaskStopTriggerJavaHookDetailReport? javaHookDetailReport = null)
	{
		var rows = new List<PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardRow>();

		Add(rows,
			PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardArea.JavaObserverCoverage,
			javaObserverRunbookDesign.RequiresJava25Maven
				? PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardStatus.BlockedMissingJavaTooling
				: PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardStatus.SatisfiedByNonLiveMetadata,
			javaObserverRunbookDesign.RequiresJava25Maven,
			javaObserverRunbookDesign.JavaSource,
			"PlayerProtectionActiveTaskStopTriggerJavaObserverRunbookDesignReport",
			$"rows={javaObserverRunbookDesign.Rows.Count}; packetHooks={javaObserverRunbookDesign.HasPacketStopTriggerHooks}; controllerHooks={javaObserverRunbookDesign.HasControllerHooks}; teleportHooks={javaObserverRunbookDesign.HasTeleportHooks}; serializerPlan={javaObserverRunbookDesign.HasSerializerPlan}; requiresJava25Maven={javaObserverRunbookDesign.RequiresJava25Maven}",
			"Java observer/runbook coverage is planned as non-live metadata; artifact generation remains blocked until Java tooling is available.");

		if (javaHookDetailReport != null)
		{
			Add(rows,
				PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardArea.JavaHookDetailCoverage,
				javaHookDetailReport.NeedsProtectionArtifactSerializer || javaHookDetailReport.NeedsJavaObserverImplementation
					? PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardStatus.BlockedMissingJavaArtifacts
					: PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardStatus.SatisfiedByNonLiveMetadata,
				javaHookDetailReport.NeedsProtectionArtifactSerializer || javaHookDetailReport.NeedsJavaObserverImplementation || !javaHookDetailReport.ReadyForRuntimeComparison,
				javaHookDetailReport.JavaSource,
				"PlayerProtectionActiveTaskStopTriggerJavaHookDetailReport",
				$"hookRows={javaHookDetailReport.Rows.Count}; directStopCallers={javaHookDetailReport.HasDirectStopPacketCallers}; teleportRunnable={javaHookDetailReport.HasTeleportRunnableFutureHook}; lifecycleHooks={javaHookDetailReport.HasProtectionLifecycleHook}; taskMapHooks={javaHookDetailReport.HasTaskMapHooks}; packetObserver={javaHookDetailReport.HasGenericPacketSerializationObserver}; needsSerializer={javaHookDetailReport.NeedsProtectionArtifactSerializer}; needsJavaObserver={javaHookDetailReport.NeedsJavaObserverImplementation}",
				"Hook details are source-reviewed metadata only; protection schema-v1 artifact serialization and Java observer wiring remain required before runtime comparison.");
		}

		Add(rows,
			PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardArea.JavaToolingAndArtifacts,
			generatedArtifactExecutionPlan.NeedsJavaTooling
				? PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardStatus.BlockedMissingJavaTooling
				: generatedArtifactExecutionPlan.NeedsJavaArtifacts
					? PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardStatus.BlockedMissingJavaArtifacts
					: PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardStatus.SatisfiedByNonLiveMetadata,
			generatedArtifactExecutionPlan.NeedsJavaTooling || generatedArtifactExecutionPlan.NeedsJavaArtifacts,
			generatedArtifactExecutionPlan.JavaSource,
			"PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionPlanReport",
			$"planRows={generatedArtifactExecutionPlan.Rows.Count}; needsJavaTooling={generatedArtifactExecutionPlan.NeedsJavaTooling}; needsJavaArtifacts={generatedArtifactExecutionPlan.NeedsJavaArtifacts}; ready={generatedArtifactExecutionPlan.ReadyForRuntimeComparison}",
			"Generated Java schema-v1 artifacts do not exist; dashboard must not advance to runtime comparison.");

		Add(rows,
			PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardArea.CSharpEmitterCoverage,
			csharpTraceEmitterDesign.RequiresLiveEmitter
				? PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardStatus.BlockedMissingCSharpEmitter
				: PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardStatus.SatisfiedByNonLiveMetadata,
			csharpTraceEmitterDesign.RequiresLiveEmitter,
			csharpTraceEmitterDesign.JavaSource,
			"PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterDesignReport",
			$"rows={csharpTraceEmitterDesign.Rows.Count}; packetHooks={csharpTraceEmitterDesign.HasPacketHookSites}; controllerHooks={csharpTraceEmitterDesign.HasControllerHookSites}; teleportHooks={csharpTraceEmitterDesign.HasTeleportHookSites}; requiresLiveEmitter={csharpTraceEmitterDesign.RequiresLiveEmitter}",
			"C# emitter hook-site coverage is design-only; no production packet/controller trace rows are emitted.");

		Add(rows,
			PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardArea.RuntimeEvidence,
			readinessReport.NeedsRuntimeComparisonEvidence || readinessReport.NeedsCSharpRuntimeTraceOutput
				? PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardStatus.BlockedMissingRuntimeEvidence
				: PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardStatus.SatisfiedByNonLiveMetadata,
			readinessReport.NeedsRuntimeComparisonEvidence || readinessReport.NeedsCSharpRuntimeTraceOutput,
			readinessReport.JavaSource,
			"PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessReport",
			$"readinessRows={readinessReport.Rows.Count}; needsCSharpRuntimeTrace={readinessReport.NeedsCSharpRuntimeTraceOutput}; needsRuntimeEvidence={readinessReport.NeedsRuntimeComparisonEvidence}; ready={readinessReport.ReadyForRuntimeComparison}",
			"Runtime evidence remains blocked until generated Java artifacts and live C# trace rows both exist.");

		Add(rows,
			PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardArea.KeyProjection,
			keyProjectionReport == null || keyProjectionReport.NeedsJavaKeys || keyProjectionReport.NeedsCSharpKeys
				? PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardStatus.BlockedMissingRuntimeEvidence
				: keyProjectionReport.NeedsKeyAlignment
					? PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardStatus.BlockedMissingRuntimeEvidence
					: keyProjectionReport.NeedsComparisonExecution
						? PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardStatus.BlockedComparisonNotExecuted
						: PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardStatus.SatisfiedByNonLiveMetadata,
			keyProjectionReport == null || keyProjectionReport.NeedsJavaKeys || keyProjectionReport.NeedsCSharpKeys || keyProjectionReport.NeedsKeyAlignment || keyProjectionReport.NeedsComparisonExecution,
			keyProjectionReport?.JavaSource ?? "generated Java artifacts and future C# runtime trace rows",
			"PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKeyProjectionReport",
			keyProjectionReport == null
				? "no key projection report supplied"
				: $"keyRows={keyProjectionReport.Rows.Count}; javaKeys={keyProjectionReport.JavaKeys.Count}; csharpKeys={keyProjectionReport.CSharpKeys.Count}; needsKeyAlignment={keyProjectionReport.NeedsKeyAlignment}; needsComparisonExecution={keyProjectionReport.NeedsComparisonExecution}",
			"Projected keys are a prerequisite dashboard signal only; verified parity still requires deterministic Java/C# runtime comparison.");

		Add(rows,
			PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardArea.RuntimeComparisonReadiness,
			readinessReport.NeedsRuntimeComparisonExecution
				? PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardStatus.BlockedComparisonNotExecuted
				: readinessReport.ReadyForRuntimeComparison
					? PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardStatus.SatisfiedByNonLiveMetadata
					: PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardStatus.BlockedMissingRuntimeEvidence,
			!readinessReport.ReadyForRuntimeComparison,
			readinessReport.JavaSource,
			"PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessReport",
			$"ready={readinessReport.ReadyForRuntimeComparison}; needsExecution={readinessReport.NeedsRuntimeComparisonExecution}; blockingRows={readinessReport.Rows.Count(row => row.BlocksRuntimeComparison)}",
			"Readiness remains the final guard; dashboard rows are advisory metadata and do not execute comparison.");

		var rowArray = rows.ToArray();

		return new PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardReport(
			rowArray,
			HasJavaObserverCoverage: javaObserverRunbookDesign.HasPacketStopTriggerHooks && javaObserverRunbookDesign.HasControllerHooks && javaObserverRunbookDesign.HasTeleportHooks,
			HasJavaToolingBlocker: javaObserverRunbookDesign.RequiresJava25Maven || generatedArtifactExecutionPlan.NeedsJavaTooling,
			HasCSharpEmitterCoverage: csharpTraceEmitterDesign.HasPacketHookSites && csharpTraceEmitterDesign.HasControllerHookSites && csharpTraceEmitterDesign.HasTeleportHookSites,
			HasRuntimeEvidenceBlocker: readinessReport.NeedsRuntimeComparisonEvidence || readinessReport.NeedsCSharpRuntimeTraceOutput,
			HasKeyProjectionEvidence: keyProjectionReport != null,
			HasReadinessEvidence: readinessReport.Rows.Count > 0,
			HasJavaHookDetailEvidence: javaHookDetailReport != null,
			JavaHookDetailRowCount: javaHookDetailReport?.Rows.Count ?? 0,
			NeedsProtectionArtifactSerializer: javaHookDetailReport?.NeedsProtectionArtifactSerializer == true,
			NeedsJavaObserverImplementation: javaHookDetailReport?.NeedsJavaObserverImplementation == true,
			NeedsJavaArtifacts: generatedArtifactExecutionPlan.NeedsJavaArtifacts
				|| readinessReport.NeedsGeneratedJavaTraceArtifacts
				|| javaHookDetailReport?.NeedsProtectionArtifactSerializer == true,
			NeedsCSharpEmitter: generatedArtifactExecutionPlan.NeedsCSharpEmitter || readinessReport.NeedsCSharpTraceEmitter,
			NeedsRuntimeEvidence: generatedArtifactExecutionPlan.NeedsRuntimeEvidence || readinessReport.NeedsRuntimeComparisonEvidence,
			NeedsComparisonExecution: generatedArtifactExecutionPlan.NeedsComparisonExecution
				|| readinessReport.NeedsRuntimeComparisonExecution
				|| keyProjectionReport?.NeedsComparisonExecution == true
				|| javaHookDetailReport is { ReadyForRuntimeComparison: false },
			ReadyForRuntimeComparison: false,
			"Protection stop-trigger prerequisite dashboard",
			IsLive: false);
	}

	private static void Add(
		ICollection<PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardRow> rows,
		PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardArea area,
		PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardStatus status,
		bool blocksRuntimeComparison,
		string javaSource,
		string csharpSource,
		string evidence,
		string notes)
	{
		rows.Add(new PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardRow(
			rows.Count + 1,
			area,
			status,
			blocksRuntimeComparison,
			javaSource,
			csharpSource,
			evidence,
			notes));
	}
}
