namespace Aion.GameServer.Services;

public static class FindGroupMutationPostJavaTraceArtifactSchemaReportService
{
	public static FindGroupMutationPostJavaTraceArtifactSchemaReport Create()
	{
		var comparisonSchema = FindGroupDirectPacketMutationPostBoundaryTraceSchemaService.CreateSchema();
		var fields = comparisonSchema.RequiredFields
			.Select((field, index) => new FindGroupMutationPostJavaTraceArtifactFieldRow(
				index + 1,
				field.Name,
				JsonPath: $"$.traces[*].{field.Name}",
				FindGroupMutationPostJavaTraceArtifactStatus.BlockedMissingTraceSerializer,
				FieldType(field.Name),
				field.Requirement))
			.ToArray();
		var actions = comparisonSchema.SupportedActions
			.Select((action, index) => new FindGroupMutationPostJavaTraceArtifactActionRow(
				index + 1,
				action.Action,
				action.MutationKind,
				action.JavaMethod,
				action.JavaPostedSystemMessage,
				action.PostedSystemMessageId,
				action.RefreshedShowListAction,
				FindGroupMutationPostJavaTraceArtifactStatus.BlockedMissingJavaInstrumentation))
			.ToArray();

		return new FindGroupMutationPostJavaTraceArtifactSchemaReport(
			comparisonSchema.SchemaVersion,
			comparisonSchema.TraceName,
			fields,
			actions,
			[
				new FindGroupMutationPostJavaTraceArtifactInstrumentationCaveat(
					"Do not add synchronization around FindGroupService maps for trace emission.",
					"FindGroupService.addRecruitment/addApplication",
					"Extra synchronization would hide Java ConcurrentHashMap timing and multi-caller interleaving behavior."),
				new FindGroupMutationPostJavaTraceArtifactInstrumentationCaveat(
					"Record trace rows after mutation and before PacketSendUtility.sendPacket calls without changing send ordering.",
					"FindGroupService.addRecruitment/addApplication",
					"Action 2 and 6 parity depends on mutation-before-posted-message-before-refreshed-list ordering."),
				new FindGroupMutationPostJavaTraceArtifactInstrumentationCaveat(
					"Treat timestamps as diagnostics only; use trace order and explicit state fields as parity keys.",
					"CM_FIND_GROUP.runImpl",
					"Java and C# clock sources are not parity evidence."),
			],
			HasRequiredActionMappings: actions.Select(action => action.Action).SequenceEqual([2, 6]),
			ReusesMutationPostBoundaryTraceSchema: fields.Select(field => field.Name).SequenceEqual(comparisonSchema.RequiredFields.Select(field => field.Name)),
			RequiresJavaInstrumentation: true,
			RequiresTraceSerializer: true,
			ReadyForRuntimeComparison: false,
			"Java trace artifact schema target only; no Java artifacts have been generated or validated.",
			"Java sources reviewed: CM_FIND_GROUP.runImpl actions 2 and 6; FindGroupService.addRecruitment/addApplication.");
	}

	private static string FieldType(string fieldName)
	{
		return fieldName switch
		{
			"traceName" or "traceSource" or "activePlayerRace" or "mutationKind" or "postedSystemMessageType" or "refreshedListPacketType" => "string",
			"boundaryAccepted" or "stateMutationRecordedBeforeDirectPackets" or "executorInvokedFromBoundary" or "registrySendsObservedInOrder" => "boolean",
			"visibleEntryObjectIdsAfterMutation" => "integer array",
			_ => "integer",
		};
	}
}

public enum FindGroupMutationPostJavaTraceArtifactStatus
{
	BlockedMissingJavaInstrumentation,
	BlockedMissingTraceSerializer,
	ReadyForJavaImplementationDesignOnly,
}

public sealed record FindGroupMutationPostJavaTraceArtifactSchemaReport(
	int SchemaVersion,
	string TraceName,
	IReadOnlyList<FindGroupMutationPostJavaTraceArtifactFieldRow> Fields,
	IReadOnlyList<FindGroupMutationPostJavaTraceArtifactActionRow> Actions,
	IReadOnlyList<FindGroupMutationPostJavaTraceArtifactInstrumentationCaveat> InstrumentationCaveats,
	bool HasRequiredActionMappings,
	bool ReusesMutationPostBoundaryTraceSchema,
	bool RequiresJavaInstrumentation,
	bool RequiresTraceSerializer,
	bool ReadyForRuntimeComparison,
	string BoundaryNote,
	string JavaSource);

public sealed record FindGroupMutationPostJavaTraceArtifactFieldRow(
	int Order,
	string Name,
	string JsonPath,
	FindGroupMutationPostJavaTraceArtifactStatus Status,
	string FieldType,
	string Requirement);

public sealed record FindGroupMutationPostJavaTraceArtifactActionRow(
	int Order,
	int Action,
	FindGroupDirectPacketMutationPostTraceMutationKind MutationKind,
	string JavaMethod,
	string JavaPostedSystemMessage,
	int PostedSystemMessageId,
	int RefreshedShowListAction,
	FindGroupMutationPostJavaTraceArtifactStatus Status);

public sealed record FindGroupMutationPostJavaTraceArtifactInstrumentationCaveat(
	string Caveat,
	string JavaSource,
	string Risk);
