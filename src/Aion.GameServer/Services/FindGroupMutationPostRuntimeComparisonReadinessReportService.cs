namespace Aion.GameServer.Services;

public enum FindGroupMutationPostRuntimeComparisonReadinessBlocker
{
	MutationPostTraceSchema,
	JavaInstrumentationDesign,
	JavaArtifactReader,
	CSharpTraceEmitterDesign,
	LiveCSharpBoundaryCapture,
	RuntimeComparisonExecution,
}

public enum FindGroupMutationPostRuntimeComparisonReadinessStatus
{
	SatisfiedByNonLiveMetadata,
	BlockedMissingPrerequisite,
	BlockedMissingJavaArtifact,
	BlockedInvalidJavaArtifact,
	BlockedMissingLiveBoundaryCapture,
	BlockedMissingCSharpRuntimeTrace,
	BlockedComparisonNotExecuted,
}

public sealed record FindGroupMutationPostRuntimeComparisonReadinessRow(
	int Order,
	FindGroupMutationPostRuntimeComparisonReadinessBlocker Blocker,
	FindGroupMutationPostRuntimeComparisonReadinessStatus Status,
	bool BlocksRuntimeComparison,
	string JavaSource,
	string CSharpTarget,
	string Evidence,
	string Notes);

public sealed record FindGroupMutationPostRuntimeComparisonReadinessReport(
	IReadOnlyList<FindGroupMutationPostRuntimeComparisonReadinessRow> Rows,
	bool HasTraceSchema,
	bool HasJavaInstrumentationDesign,
	bool HasJavaArtifactReader,
	bool HasShapeValidJavaArtifacts,
	bool HasCSharpTraceEmitterDesign,
	bool NeedsGeneratedJavaArtifacts,
	bool NeedsLiveBoundaryCapture,
	bool NeedsCSharpRuntimeTrace,
	bool NeedsComparisonExecution,
	bool ReadyForRuntimeComparison,
	string TraceName,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: readiness gate for CM_FIND_GROUP action 2/6 mutation-post
/// Java/C# runtime trace comparison. This is an aggregate report only; it emits no live traces.
/// </summary>
public static class FindGroupMutationPostRuntimeComparisonReadinessReportService
{
	public static FindGroupMutationPostRuntimeComparisonReadinessReport Create(
		FindGroupDirectPacketMutationPostBoundaryTraceSchema? schema = null,
		FindGroupMutationPostJavaInstrumentationDesignReport? javaInstrumentationDesign = null,
		FindGroupMutationPostJavaTraceArtifactDirectoryReport? javaArtifactReader = null,
		FindGroupMutationPostCSharpTraceEmitterDesignReport? csharpTraceEmitterDesign = null)
	{
		schema ??= FindGroupDirectPacketMutationPostBoundaryTraceSchemaService.CreateSchema();
		javaInstrumentationDesign ??= FindGroupMutationPostJavaInstrumentationDesignReportService.Create();
		javaArtifactReader ??= FindGroupMutationPostJavaTraceArtifactDirectoryReportService.Create();
		csharpTraceEmitterDesign ??= FindGroupMutationPostCSharpTraceEmitterDesignReportService.Create();

		var rows = new List<FindGroupMutationPostRuntimeComparisonReadinessRow>();
		AddTraceSchema(rows, schema);
		AddJavaInstrumentationDesign(rows, javaInstrumentationDesign);
		AddJavaArtifactReader(rows, schema, javaArtifactReader);
		AddCSharpTraceEmitterDesign(rows, csharpTraceEmitterDesign);
		AddLiveBoundaryCapture(rows, csharpTraceEmitterDesign);
		AddRuntimeComparisonExecution(rows, javaArtifactReader, csharpTraceEmitterDesign);

		var rowArray = rows.ToArray();

		return new FindGroupMutationPostRuntimeComparisonReadinessReport(
			rowArray,
			HasTraceSchema: schema != null,
			HasJavaInstrumentationDesign: javaInstrumentationDesign != null,
			HasJavaArtifactReader: javaArtifactReader != null,
			HasShapeValidJavaArtifacts: javaArtifactReader?.Status == FindGroupMutationPostJavaTraceArtifactDirectoryStatus.AllExpectedArtifactsShapeValid,
			HasCSharpTraceEmitterDesign: csharpTraceEmitterDesign != null,
			NeedsGeneratedJavaArtifacts: rowArray.Any(row => row.Blocker == FindGroupMutationPostRuntimeComparisonReadinessBlocker.JavaArtifactReader && row.BlocksRuntimeComparison),
			NeedsLiveBoundaryCapture: rowArray.Any(row => row.Blocker == FindGroupMutationPostRuntimeComparisonReadinessBlocker.LiveCSharpBoundaryCapture && row.BlocksRuntimeComparison),
			NeedsCSharpRuntimeTrace: rowArray.Any(row => row.Blocker == FindGroupMutationPostRuntimeComparisonReadinessBlocker.CSharpTraceEmitterDesign && row.BlocksRuntimeComparison)
				|| rowArray.Any(row => row.Blocker == FindGroupMutationPostRuntimeComparisonReadinessBlocker.LiveCSharpBoundaryCapture && row.BlocksRuntimeComparison),
			NeedsComparisonExecution: rowArray.Any(row => row.Blocker == FindGroupMutationPostRuntimeComparisonReadinessBlocker.RuntimeComparisonExecution && row.BlocksRuntimeComparison),
			ReadyForRuntimeComparison: rowArray.Length > 0 && rowArray.All(row => !row.BlocksRuntimeComparison),
			schema?.TraceName ?? "cm-find-group-direct-mutation-post-boundary",
			schema?.JavaSource ?? "CM_FIND_GROUP.runImpl actions 2/6; FindGroupService.addRecruitment/addApplication",
			IsLive: false);
	}

	private static void AddTraceSchema(
		ICollection<FindGroupMutationPostRuntimeComparisonReadinessRow> rows,
		FindGroupDirectPacketMutationPostBoundaryTraceSchema? schema)
	{
		if (schema == null)
		{
			Add(rows,
				FindGroupMutationPostRuntimeComparisonReadinessBlocker.MutationPostTraceSchema,
				FindGroupMutationPostRuntimeComparisonReadinessStatus.BlockedMissingPrerequisite,
				blocks: true,
				"CM_FIND_GROUP mutation-post trace requirements",
				"FindGroupDirectPacketMutationPostBoundaryTraceSchemaService",
				"missing schema report",
				"Runtime comparison cannot proceed without action 2/6 schema fields and Java mappings.");
			return;
		}

		Add(rows,
			FindGroupMutationPostRuntimeComparisonReadinessBlocker.MutationPostTraceSchema,
			FindGroupMutationPostRuntimeComparisonReadinessStatus.SatisfiedByNonLiveMetadata,
			blocks: false,
			schema.JavaSource,
			"FindGroupDirectPacketMutationPostBoundaryTraceSchema",
			$"schemaVersion={schema.SchemaVersion}; traceName={schema.TraceName}; fields={schema.RequiredFields.Count}; actions={string.Join("/", schema.SupportedActions.Select(action => action.Action))}",
			"Schema metadata exists, but runtime comparison still requires generated Java and live C# trace rows.");
	}

	private static void AddJavaInstrumentationDesign(
		ICollection<FindGroupMutationPostRuntimeComparisonReadinessRow> rows,
		FindGroupMutationPostJavaInstrumentationDesignReport? design)
	{
		if (design == null)
		{
			Add(rows,
				FindGroupMutationPostRuntimeComparisonReadinessBlocker.JavaInstrumentationDesign,
				FindGroupMutationPostRuntimeComparisonReadinessStatus.BlockedMissingPrerequisite,
				blocks: true,
				"FindGroupService.addRecruitment/addApplication instrumentation plan",
				"FindGroupMutationPostJavaInstrumentationDesignReportService",
				"missing Java instrumentation design report",
				"Java observer placement must be planned before generated artifact work can be trusted.");
			return;
		}

		Add(rows,
			FindGroupMutationPostRuntimeComparisonReadinessBlocker.JavaInstrumentationDesign,
			FindGroupMutationPostRuntimeComparisonReadinessStatus.SatisfiedByNonLiveMetadata,
			blocks: false,
			design.JavaSource,
			"FindGroupMutationPostJavaInstrumentationDesignReport",
			$"points={design.Points.Count}; coversActionsTwoAndSix={design.CoversActionsTwoAndSix}; preservesSendOrdering={design.PreservesJavaSendOrdering}; requiresJavaInstrumentation={design.RequiresJavaInstrumentation}; requiresTraceSerializer={design.RequiresTraceSerializer}",
			"Java instrumentation design exists as non-live metadata only; no Java hooks or serializer have been implemented.");
	}

	private static void AddJavaArtifactReader(
		ICollection<FindGroupMutationPostRuntimeComparisonReadinessRow> rows,
		FindGroupDirectPacketMutationPostBoundaryTraceSchema? schema,
		FindGroupMutationPostJavaTraceArtifactDirectoryReport? reader)
	{
		if (schema == null || reader == null)
		{
			Add(rows,
				FindGroupMutationPostRuntimeComparisonReadinessBlocker.JavaArtifactReader,
				FindGroupMutationPostRuntimeComparisonReadinessStatus.BlockedMissingPrerequisite,
				blocks: true,
				"generated Java action 2/6 mutation-post artifacts",
				"FindGroupMutationPostJavaTraceArtifactDirectoryReportService",
				schema == null ? "trace schema missing" : "artifact reader missing",
				"Artifact discovery cannot be evaluated without schema and reader reports.");
			return;
		}

		var status = reader.Status switch
		{
			FindGroupMutationPostJavaTraceArtifactDirectoryStatus.AllExpectedArtifactsShapeValid => FindGroupMutationPostRuntimeComparisonReadinessStatus.SatisfiedByNonLiveMetadata,
			FindGroupMutationPostJavaTraceArtifactDirectoryStatus.InvalidArtifacts => FindGroupMutationPostRuntimeComparisonReadinessStatus.BlockedInvalidJavaArtifact,
			_ => FindGroupMutationPostRuntimeComparisonReadinessStatus.BlockedMissingJavaArtifact,
		};

		Add(rows,
			FindGroupMutationPostRuntimeComparisonReadinessBlocker.JavaArtifactReader,
			status,
			blocks: status != FindGroupMutationPostRuntimeComparisonReadinessStatus.SatisfiedByNonLiveMetadata,
			schema.JavaSource,
			"FindGroupMutationPostJavaTraceArtifactDirectoryReport",
			$"status={reader.Status}; files={reader.Files.Count}; hasGenerated={reader.HasGeneratedJavaArtifacts}; hasAllExpected={reader.HasAllExpectedFiles}; shapeValid={reader.HasOnlyShapeValidArtifacts}",
			status == FindGroupMutationPostRuntimeComparisonReadinessStatus.SatisfiedByNonLiveMetadata
				? "Expected Java artifacts are shape-valid only; live C# trace rows and comparison execution are still required."
				: "Expected generated Java action 2/6 artifacts are missing or invalid.");
	}

	private static void AddCSharpTraceEmitterDesign(
		ICollection<FindGroupMutationPostRuntimeComparisonReadinessRow> rows,
		FindGroupMutationPostCSharpTraceEmitterDesignReport? design)
	{
		if (design == null)
		{
			Add(rows,
				FindGroupMutationPostRuntimeComparisonReadinessBlocker.CSharpTraceEmitterDesign,
				FindGroupMutationPostRuntimeComparisonReadinessStatus.BlockedMissingPrerequisite,
				blocks: true,
				"future C# action 2/6 trace emitter",
				"FindGroupMutationPostCSharpTraceEmitterDesignReportService",
				"missing C# trace emitter design report",
				"C# trace row hook sites must be planned before live trace capture can be considered.");
			return;
		}

		Add(rows,
			FindGroupMutationPostRuntimeComparisonReadinessBlocker.CSharpTraceEmitterDesign,
			FindGroupMutationPostRuntimeComparisonReadinessStatus.SatisfiedByNonLiveMetadata,
			blocks: false,
			design.JavaSource,
			"FindGroupMutationPostCSharpTraceEmitterDesignReport",
			$"rows={design.Rows.Count}; boundaryHook={design.HasBoundaryHookSite}; directPacketHooks={design.HasDirectPacketHookSites}; serializationPlan={design.HasRuntimeRowSerializationPlan}; requiresLiveEmitter={design.RequiresLiveEmitter}",
			"C# trace-emitter design exists as non-live metadata only; live runtime rows are still missing.");
	}

	private static void AddLiveBoundaryCapture(
		ICollection<FindGroupMutationPostRuntimeComparisonReadinessRow> rows,
		FindGroupMutationPostCSharpTraceEmitterDesignReport? design)
	{
		Add(rows,
			FindGroupMutationPostRuntimeComparisonReadinessBlocker.LiveCSharpBoundaryCapture,
			FindGroupMutationPostRuntimeComparisonReadinessStatus.BlockedMissingLiveBoundaryCapture,
			blocks: true,
			"CM_FIND_GROUP.runImpl synchronous action 2/6 execution",
			"GameServerConnection.ProcessPacketAsync live CmFindGroup boundary and registry send observations",
			design == null ? "C# emitter design missing" : $"requiresLiveBoundaryCapture={design.RequiresLiveBoundaryCapture}; requiresLiveEmitter={design.RequiresLiveEmitter}",
			"Live C# boundary capture, executor invocation, registry send ordering, and runtime row serialization are missing.");
	}

	private static void AddRuntimeComparisonExecution(
		ICollection<FindGroupMutationPostRuntimeComparisonReadinessRow> rows,
		FindGroupMutationPostJavaTraceArtifactDirectoryReport? reader,
		FindGroupMutationPostCSharpTraceEmitterDesignReport? design)
	{
		Add(rows,
			FindGroupMutationPostRuntimeComparisonReadinessBlocker.RuntimeComparisonExecution,
			FindGroupMutationPostRuntimeComparisonReadinessStatus.BlockedComparisonNotExecuted,
			blocks: true,
			"generated Java mutation-post artifacts and live C# trace rows",
			"future deterministic FindGroup mutation-post runtime comparison",
			$"javaArtifactsShapeValid={reader?.Status == FindGroupMutationPostJavaTraceArtifactDirectoryStatus.AllExpectedArtifactsShapeValid}; csharpEmitterDesign={design != null}; comparisonExecuted=False",
			"Verified parity cannot be claimed until generated Java artifacts and live C# trace rows are compared deterministically.");
	}

	private static void Add(
		ICollection<FindGroupMutationPostRuntimeComparisonReadinessRow> rows,
		FindGroupMutationPostRuntimeComparisonReadinessBlocker blocker,
		FindGroupMutationPostRuntimeComparisonReadinessStatus status,
		bool blocks,
		string javaSource,
		string csharpTarget,
		string evidence,
		string notes)
	{
		rows.Add(new FindGroupMutationPostRuntimeComparisonReadinessRow(
			rows.Count + 1,
			blocker,
			status,
			blocks,
			javaSource,
			csharpTarget,
			evidence,
			notes));
	}
}
