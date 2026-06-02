namespace Aion.GameServer.Services;

public enum FindGroupMutationPostCSharpTraceEmitterHookSite
{
	ArtifactShapeValidationBoundary,
	ConnectionBoundaryAccepted,
	SingletonMutationProjection,
	DirectPacketIntentMaterialized,
	BoundaryExecutorInvocation,
	RegistrySendObservation,
	RuntimeTraceRowSerialized,
}

public enum FindGroupMutationPostCSharpTraceEmitterDesignStatus
{
	BlockedMissingLiveBoundaryCapture,
	BlockedMissingLiveEmitter,
	ReadyForDesignOnly,
}

public sealed record FindGroupMutationPostCSharpTraceEmitterDesignRow(
	int Order,
	FindGroupMutationPostCSharpTraceEmitterHookSite HookSite,
	FindGroupMutationPostCSharpTraceEmitterDesignStatus Status,
	string JavaSource,
	string CSharpTarget,
	string RequiredTraceFields,
	string Notes);

public sealed record FindGroupMutationPostCSharpTraceEmitterDesignReport(
	IReadOnlyList<FindGroupMutationPostCSharpTraceEmitterDesignRow> Rows,
	bool HasBoundaryHookSite,
	bool HasMutationProjectionHookSite,
	bool HasDirectPacketHookSites,
	bool HasRuntimeRowSerializationPlan,
	bool ReusesMutationPostTraceSchema,
	bool RequiresLiveBoundaryCapture,
	bool RequiresLiveEmitter,
	bool ReadyForRuntimeComparison,
	string TraceName,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: non-live design for future C# CM_FIND_GROUP action 2/6 trace
/// emitters that must match generated Java mutation-post artifacts.
/// </summary>
public static class FindGroupMutationPostCSharpTraceEmitterDesignReportService
{
	public static FindGroupMutationPostCSharpTraceEmitterDesignReport Create()
	{
		var schema = FindGroupDirectPacketMutationPostBoundaryTraceSchemaService.CreateSchema();
		var rows = new List<FindGroupMutationPostCSharpTraceEmitterDesignRow>();

		Add(rows,
			FindGroupMutationPostCSharpTraceEmitterHookSite.ArtifactShapeValidationBoundary,
			FindGroupMutationPostCSharpTraceEmitterDesignStatus.ReadyForDesignOnly,
			schema.JavaSource,
			"FindGroupMutationPostJavaTraceArtifactValidatorService / future C# mutation-post row adapter",
			string.Join(", ", schema.RequiredFields.Select(field => field.Name)),
			"Future C# rows must satisfy the same schema-v1 field order and action mapping as generated Java artifacts; this design does not prove parity.");

		Add(rows,
			FindGroupMutationPostCSharpTraceEmitterHookSite.ConnectionBoundaryAccepted,
			FindGroupMutationPostCSharpTraceEmitterDesignStatus.BlockedMissingLiveBoundaryCapture,
			"CM_FIND_GROUP.runImpl action 2/6 service dispatch",
			"GameServerConnection.ProcessPacketAsync live CmFindGroup branch",
			"schemaVersion, traceName, traceSource=CSharp, action, boundaryAccepted, activePlayerObjectId, activePlayerRace",
			"C# must record the accepted triggering client packet and active player before invoking the shared FindGroup planner/executor.");

		Add(rows,
			FindGroupMutationPostCSharpTraceEmitterHookSite.SingletonMutationProjection,
			FindGroupMutationPostCSharpTraceEmitterDesignStatus.BlockedMissingLiveEmitter,
			"FindGroupService.addRecruitment/addApplication after map mutation",
			"FindGroupRecruitmentPlanService action 2/6 mutation plans",
			"serverEpochSeconds, mutationKind, mutatedEntryObjectId, stateMutationRecordedBeforeDirectPackets, visibleEntryObjectIdsAfterMutation",
			"Trace rows must observe the same singleton state mutation and race-filtered visible ids that the disabled C# projection currently models.");

		Add(rows,
			FindGroupMutationPostCSharpTraceEmitterHookSite.DirectPacketIntentMaterialized,
			FindGroupMutationPostCSharpTraceEmitterDesignStatus.BlockedMissingLiveEmitter,
			"FindGroupService posted SM_SYSTEM_MESSAGE then refreshed SM_FIND_GROUP",
			"FindGroupConnectionBoundarySideEffectCompositionEvidenceService direct packet intents",
			"postedSystemMessageRecipientObjectId, postedSystemMessageType, postedSystemMessageId, refreshedListRecipientObjectId, refreshedListPacketType, refreshedListAction",
			"Direct packet intent rows must preserve Java posted-system-message-before-refreshed-list ordering for action 2 and 6.");

		Add(rows,
			FindGroupMutationPostCSharpTraceEmitterHookSite.BoundaryExecutorInvocation,
			FindGroupMutationPostCSharpTraceEmitterDesignStatus.BlockedMissingLiveBoundaryCapture,
			"AionClientPacket.run synchronously invokes CM_FIND_GROUP.runImpl",
			"FindGroupSideEffectDispatchExecutorService invoked from GameServerConnection.ProcessPacketAsync",
			"executorInvokedFromBoundary, worldBroadcastCount=0, inviteDispatchCount=0",
			"Disabled executor evidence is not enough; this row requires the live connection boundary to invoke the executor for CmFindGroup.");

		Add(rows,
			FindGroupMutationPostCSharpTraceEmitterHookSite.RegistrySendObservation,
			FindGroupMutationPostCSharpTraceEmitterDesignStatus.BlockedMissingLiveBoundaryCapture,
			"PacketSendUtility.sendPacket posted message then refreshed list",
			"IGameClientConnectionRegistry send observation for live direct packets",
			"registrySendsObservedInOrder, postedSystemMessageType=SmSystemMessage, refreshedListPacketType=SmFindGroup",
			"Live registry observations must prove posted message send before refreshed list send; synthetic or disabled intent ordering is only partial evidence.");

		Add(rows,
			FindGroupMutationPostCSharpTraceEmitterHookSite.RuntimeTraceRowSerialized,
			FindGroupMutationPostCSharpTraceEmitterDesignStatus.BlockedMissingLiveEmitter,
			"generated Java action 2/6 mutation-post artifact rows",
			"future C# mutation-post runtime trace export",
			$"traceName={schema.TraceName}, supportedActions={string.Join("/", schema.SupportedActions.Select(action => action.Action))}",
			"Serialize one C# row per live action trace after boundary/executor/registry evidence exists; runtime comparison remains blocked until Java and C# rows are compared.");

		var rowArray = rows.ToArray();

		return new FindGroupMutationPostCSharpTraceEmitterDesignReport(
			rowArray,
			HasBoundaryHookSite: rowArray.Any(row => row.HookSite == FindGroupMutationPostCSharpTraceEmitterHookSite.ConnectionBoundaryAccepted),
			HasMutationProjectionHookSite: rowArray.Any(row => row.HookSite == FindGroupMutationPostCSharpTraceEmitterHookSite.SingletonMutationProjection),
			HasDirectPacketHookSites: rowArray.Any(row => row.HookSite == FindGroupMutationPostCSharpTraceEmitterHookSite.DirectPacketIntentMaterialized)
				&& rowArray.Any(row => row.HookSite == FindGroupMutationPostCSharpTraceEmitterHookSite.RegistrySendObservation),
			HasRuntimeRowSerializationPlan: rowArray.Any(row => row.HookSite == FindGroupMutationPostCSharpTraceEmitterHookSite.RuntimeTraceRowSerialized),
			ReusesMutationPostTraceSchema: true,
			RequiresLiveBoundaryCapture: true,
			RequiresLiveEmitter: true,
			ReadyForRuntimeComparison: false,
			schema.TraceName,
			schema.JavaSource,
			IsLive: false);
	}

	private static void Add(
		ICollection<FindGroupMutationPostCSharpTraceEmitterDesignRow> rows,
		FindGroupMutationPostCSharpTraceEmitterHookSite hookSite,
		FindGroupMutationPostCSharpTraceEmitterDesignStatus status,
		string javaSource,
		string csharpTarget,
		string requiredTraceFields,
		string notes)
	{
		rows.Add(new FindGroupMutationPostCSharpTraceEmitterDesignRow(
			rows.Count + 1,
			hookSite,
			status,
			javaSource,
			csharpTarget,
			requiredTraceFields,
			notes));
	}
}
