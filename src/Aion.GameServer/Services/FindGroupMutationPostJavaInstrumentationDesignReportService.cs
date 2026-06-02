namespace Aion.GameServer.Services;

public enum FindGroupMutationPostJavaInstrumentationDesignStatus
{
	ReadyForDesignOnly,
	BlockedMissingJavaInstrumentation,
	BlockedMissingTraceSerializer,
}

public enum FindGroupMutationPostJavaInstrumentationPointKind
{
	ClientPacketPayloadParsed,
	ClientPacketRunImplEntered,
	RecruitmentStateMutationRecorded,
	RecruitmentPostedMessageSendObserved,
	RecruitmentRefreshedListSendObserved,
	ApplicationStateMutationRecorded,
	ApplicationPostedMessageSendObserved,
	ApplicationRefreshedListSendObserved,
	TraceArtifactRowSerialized,
}

public sealed record FindGroupMutationPostJavaInstrumentationPoint(
	int Order,
	FindGroupMutationPostJavaInstrumentationPointKind Kind,
	FindGroupMutationPostJavaInstrumentationDesignStatus Status,
	int Action,
	string JavaSource,
	string ExpectedTraceEvent,
	string RequiredFields,
	string Notes);

public sealed record FindGroupMutationPostJavaInstrumentationCaveat(
	string Caveat,
	string JavaSource,
	string Risk);

public sealed record FindGroupMutationPostJavaInstrumentationDesignReport(
	IReadOnlyList<FindGroupMutationPostJavaInstrumentationPoint> Points,
	IReadOnlyList<FindGroupMutationPostJavaInstrumentationCaveat> Caveats,
	bool CoversActionsTwoAndSix,
	bool HasRecruitmentMutationOrdering,
	bool HasApplicationMutationOrdering,
	bool PreservesJavaSendOrdering,
	bool ReusesTraceArtifactValidator,
	bool RequiresJavaInstrumentation,
	bool RequiresTraceSerializer,
	bool ReadyForRuntimeComparison,
	string TraceName,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: non-live design for future Java CM_FIND_GROUP action 2/6
/// instrumentation. It documents hook placement only; no Java source or runtime behavior changes.
/// </summary>
public static class FindGroupMutationPostJavaInstrumentationDesignReportService
{
	public static FindGroupMutationPostJavaInstrumentationDesignReport Create()
	{
		var schemaReport = FindGroupMutationPostJavaTraceArtifactSchemaReportService.Create();
		var points = new List<FindGroupMutationPostJavaInstrumentationPoint>();

		AddPayloadParsed(points);
		AddRunImplEntered(points);
		AddRecruitmentMutation(points);
		AddRecruitmentPostedMessage(points);
		AddRecruitmentRefreshedList(points);
		AddApplicationMutation(points);
		AddApplicationPostedMessage(points);
		AddApplicationRefreshedList(points);
		AddTraceArtifactSerializer(points, schemaReport);

		var pointArray = points.ToArray();

		return new FindGroupMutationPostJavaInstrumentationDesignReport(
			pointArray,
			CreateCaveats(),
			CoversActionsTwoAndSix: pointArray.Any(point => point.Action == 2) && pointArray.Any(point => point.Action == 6),
			HasRecruitmentMutationOrdering: HasOrdered(pointArray,
				FindGroupMutationPostJavaInstrumentationPointKind.RecruitmentStateMutationRecorded,
				FindGroupMutationPostJavaInstrumentationPointKind.RecruitmentPostedMessageSendObserved,
				FindGroupMutationPostJavaInstrumentationPointKind.RecruitmentRefreshedListSendObserved),
			HasApplicationMutationOrdering: HasOrdered(pointArray,
				FindGroupMutationPostJavaInstrumentationPointKind.ApplicationStateMutationRecorded,
				FindGroupMutationPostJavaInstrumentationPointKind.ApplicationPostedMessageSendObserved,
				FindGroupMutationPostJavaInstrumentationPointKind.ApplicationRefreshedListSendObserved),
			PreservesJavaSendOrdering: true,
			ReusesTraceArtifactValidator: true,
			RequiresJavaInstrumentation: true,
			RequiresTraceSerializer: true,
			ReadyForRuntimeComparison: false,
			schemaReport.TraceName,
			"Java sources reviewed: CM_FIND_GROUP.readImpl/runImpl actions 2 and 6; FindGroupService.addRecruitment/addApplication/showRecruitments/showApplications.",
			IsLive: false);
	}

	private static void AddPayloadParsed(ICollection<FindGroupMutationPostJavaInstrumentationPoint> points) =>
		Add(points,
			FindGroupMutationPostJavaInstrumentationPointKind.ClientPacketPayloadParsed,
			FindGroupMutationPostJavaInstrumentationDesignStatus.ReadyForDesignOnly,
			action: 0,
			"CM_FIND_GROUP.readImpl",
			"client_packet_payload_parsed",
			"action, playerOrTeamId, message, groupType, classId, level",
			"Capture action-specific parsed payload after Java readImpl has consumed the fields; action 2 omits classId/level and action 6 includes them.");

	private static void AddRunImplEntered(ICollection<FindGroupMutationPostJavaInstrumentationPoint> points) =>
		Add(points,
			FindGroupMutationPostJavaInstrumentationPointKind.ClientPacketRunImplEntered,
			FindGroupMutationPostJavaInstrumentationDesignStatus.ReadyForDesignOnly,
			action: 0,
			"CM_FIND_GROUP.runImpl",
			"client_packet_run_impl_entered",
			"action, activePlayerObjectId, activePlayerRace, boundaryAccepted",
			"Record the active player facts immediately before the switch calls FindGroupService for action 2 or 6.");

	private static void AddRecruitmentMutation(ICollection<FindGroupMutationPostJavaInstrumentationPoint> points) =>
		Add(points,
			FindGroupMutationPostJavaInstrumentationPointKind.RecruitmentStateMutationRecorded,
			FindGroupMutationPostJavaInstrumentationDesignStatus.BlockedMissingJavaInstrumentation,
			action: 2,
			"FindGroupService.addRecruitment after recruitments.put(...)",
			"recruitment_state_mutation_recorded",
			"mutationKind=Recruitment, mutatedEntryObjectId, stateMutationRecordedBeforeDirectPackets=true",
			"Emit after resolving current team/player and writing the ConcurrentHashMap entry, before the posted system message send.");

	private static void AddRecruitmentPostedMessage(ICollection<FindGroupMutationPostJavaInstrumentationPoint> points) =>
		Add(points,
			FindGroupMutationPostJavaInstrumentationPointKind.RecruitmentPostedMessageSendObserved,
			FindGroupMutationPostJavaInstrumentationDesignStatus.BlockedMissingJavaInstrumentation,
			action: 2,
			"FindGroupService.addRecruitment before PacketSendUtility.sendPacket(... STR_PARTY_MATCH_OFFER_PARTY_POSTED)",
			"recruitment_posted_message_send_observed",
			"postedSystemMessageRecipientObjectId, postedSystemMessageType=SmSystemMessage, postedSystemMessageId=1400392",
			"Observe the direct packet intent before Java sends it, while leaving PacketSendUtility ordering unchanged.");

	private static void AddRecruitmentRefreshedList(ICollection<FindGroupMutationPostJavaInstrumentationPoint> points) =>
		Add(points,
			FindGroupMutationPostJavaInstrumentationPointKind.RecruitmentRefreshedListSendObserved,
			FindGroupMutationPostJavaInstrumentationDesignStatus.BlockedMissingJavaInstrumentation,
			action: 2,
			"FindGroupService.showRecruitments before PacketSendUtility.sendPacket(... new SM_FIND_GROUP(0, recruitments))",
			"recruitment_refreshed_list_send_observed",
			"refreshedListRecipientObjectId, refreshedListPacketType=SmFindGroup, refreshedListAction=0, visibleEntryObjectIdsAfterMutation",
			"Capture ids after Java values().stream().filter(...).toList() materializes the race-filtered snapshot.");

	private static void AddApplicationMutation(ICollection<FindGroupMutationPostJavaInstrumentationPoint> points) =>
		Add(points,
			FindGroupMutationPostJavaInstrumentationPointKind.ApplicationStateMutationRecorded,
			FindGroupMutationPostJavaInstrumentationDesignStatus.BlockedMissingJavaInstrumentation,
			action: 6,
			"FindGroupService.addApplication after applications.put(...)",
			"application_state_mutation_recorded",
			"mutationKind=Application, mutatedEntryObjectId, stateMutationRecordedBeforeDirectPackets=true",
			"Emit after writing the player object id keyed ConcurrentHashMap entry, before the posted system message send.");

	private static void AddApplicationPostedMessage(ICollection<FindGroupMutationPostJavaInstrumentationPoint> points) =>
		Add(points,
			FindGroupMutationPostJavaInstrumentationPointKind.ApplicationPostedMessageSendObserved,
			FindGroupMutationPostJavaInstrumentationDesignStatus.BlockedMissingJavaInstrumentation,
			action: 6,
			"FindGroupService.addApplication before PacketSendUtility.sendPacket(... STR_PARTY_MATCH_SEEK_PARTY_POSTED)",
			"application_posted_message_send_observed",
			"postedSystemMessageRecipientObjectId, postedSystemMessageType=SmSystemMessage, postedSystemMessageId=1400393",
			"Observe the direct packet intent before Java sends it, while leaving PacketSendUtility ordering unchanged.");

	private static void AddApplicationRefreshedList(ICollection<FindGroupMutationPostJavaInstrumentationPoint> points) =>
		Add(points,
			FindGroupMutationPostJavaInstrumentationPointKind.ApplicationRefreshedListSendObserved,
			FindGroupMutationPostJavaInstrumentationDesignStatus.BlockedMissingJavaInstrumentation,
			action: 6,
			"FindGroupService.showApplications before PacketSendUtility.sendPacket(... new SM_FIND_GROUP(4, applications))",
			"application_refreshed_list_send_observed",
			"refreshedListRecipientObjectId, refreshedListPacketType=SmFindGroup, refreshedListAction=4, visibleEntryObjectIdsAfterMutation",
			"Capture ids after Java values().stream().filter(...).toList() materializes the race-filtered snapshot.");

	private static void AddTraceArtifactSerializer(
		ICollection<FindGroupMutationPostJavaInstrumentationPoint> points,
		FindGroupMutationPostJavaTraceArtifactSchemaReport schemaReport) =>
		Add(points,
			FindGroupMutationPostJavaInstrumentationPointKind.TraceArtifactRowSerialized,
			FindGroupMutationPostJavaInstrumentationDesignStatus.BlockedMissingTraceSerializer,
			action: 0,
			"future Java trace serializer and FindGroupMutationPostJavaTraceArtifactValidatorService",
			"trace_artifact_row_serialized",
			string.Join(", ", schemaReport.Fields.Select(field => field.Name)),
			$"Serialize one row per action in trace field order for traceName={schemaReport.TraceName}; validate with FindGroupMutationPostJavaTraceArtifactValidatorService before comparison.");

	private static IReadOnlyList<FindGroupMutationPostJavaInstrumentationCaveat> CreateCaveats() =>
	[
		new FindGroupMutationPostJavaInstrumentationCaveat(
			"Do not add synchronization around recruitments or applications for trace emission.",
			"FindGroupService.addRecruitment/addApplication",
			"Extra synchronization would mask Java ConcurrentHashMap timing and caller interleavings."),
		new FindGroupMutationPostJavaInstrumentationCaveat(
			"Do not perform blocking IO in CM_FIND_GROUP.runImpl or FindGroupService packet paths.",
			"CM_FIND_GROUP.runImpl; FindGroupService.addRecruitment/addApplication",
			"Blocking work could change gameplay latency and packet ordering."),
		new FindGroupMutationPostJavaInstrumentationCaveat(
			"Do not alter PacketSendUtility send ordering.",
			"FindGroupService.addRecruitment/addApplication/showRecruitments/showApplications",
			"Action 2 and 6 parity depends on mutation-before-posted-message-before-refreshed-list ordering."),
		new FindGroupMutationPostJavaInstrumentationCaveat(
			"Do not materialize visible lists before Java showRecruitments/showApplications toList() points.",
			"FindGroupService.showRecruitments/showApplications",
			"Earlier list materialization could hide post-mutation Java snapshot timing."),
		new FindGroupMutationPostJavaInstrumentationCaveat(
			"Treat timestamps as diagnostics only, never as parity keys.",
			"future Java trace serializer",
			"Java and C# clock sources are not objective parity evidence."),
	];

	private static bool HasOrdered(
		IReadOnlyList<FindGroupMutationPostJavaInstrumentationPoint> points,
		FindGroupMutationPostJavaInstrumentationPointKind first,
		FindGroupMutationPostJavaInstrumentationPointKind second,
		FindGroupMutationPostJavaInstrumentationPointKind third)
	{
		var firstOrder = points.Single(point => point.Kind == first).Order;
		var secondOrder = points.Single(point => point.Kind == second).Order;
		var thirdOrder = points.Single(point => point.Kind == third).Order;
		return firstOrder < secondOrder && secondOrder < thirdOrder;
	}

	private static void Add(
		ICollection<FindGroupMutationPostJavaInstrumentationPoint> points,
		FindGroupMutationPostJavaInstrumentationPointKind kind,
		FindGroupMutationPostJavaInstrumentationDesignStatus status,
		int action,
		string javaSource,
		string expectedTraceEvent,
		string requiredFields,
		string notes)
	{
		points.Add(new FindGroupMutationPostJavaInstrumentationPoint(
			points.Count + 1,
			kind,
			status,
			action,
			javaSource,
			expectedTraceEvent,
			requiredFields,
			notes));
	}
}
