using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostJavaInstrumentationDesignReportServiceTests
{
	[Fact]
	public void Create_ListsNonLiveJavaInstrumentationDesignPoints()
	{
		var report = FindGroupMutationPostJavaInstrumentationDesignReportService.Create();

		Assert.False(report.IsLive);
		Assert.True(report.CoversActionsTwoAndSix);
		Assert.True(report.HasRecruitmentMutationOrdering);
		Assert.True(report.HasApplicationMutationOrdering);
		Assert.True(report.PreservesJavaSendOrdering);
		Assert.True(report.ReusesTraceArtifactValidator);
		Assert.True(report.RequiresJavaInstrumentation);
		Assert.True(report.RequiresTraceSerializer);
		Assert.False(report.ReadyForRuntimeComparison);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", report.TraceName);
		Assert.Equal(Enumerable.Range(1, report.Points.Count), report.Points.Select(point => point.Order));
	}

	[Fact]
	public void Create_DocumentsClientPacketPayloadAndRunImplHookSources()
	{
		var report = FindGroupMutationPostJavaInstrumentationDesignReportService.Create();

		Assert.Contains(report.Points, point =>
			point.Kind == FindGroupMutationPostJavaInstrumentationPointKind.ClientPacketPayloadParsed
			&& point.JavaSource.Contains("CM_FIND_GROUP.readImpl", StringComparison.Ordinal)
			&& point.RequiredFields.Contains("classId", StringComparison.Ordinal)
			&& point.Notes.Contains("action 2 omits", StringComparison.Ordinal)
			&& point.Notes.Contains("action 6 includes", StringComparison.Ordinal));
		Assert.Contains(report.Points, point =>
			point.Kind == FindGroupMutationPostJavaInstrumentationPointKind.ClientPacketRunImplEntered
			&& point.JavaSource.Contains("CM_FIND_GROUP.runImpl", StringComparison.Ordinal)
			&& point.RequiredFields.Contains("activePlayerRace", StringComparison.Ordinal)
			&& point.RequiredFields.Contains("boundaryAccepted", StringComparison.Ordinal));
		Assert.Contains("CM_FIND_GROUP.readImpl/runImpl actions 2 and 6", report.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_RecordsRecruitmentMutationBeforePostedMessageBeforeRefreshedList()
	{
		var report = FindGroupMutationPostJavaInstrumentationDesignReportService.Create();

		var mutation = Single(report, FindGroupMutationPostJavaInstrumentationPointKind.RecruitmentStateMutationRecorded);
		var posted = Single(report, FindGroupMutationPostJavaInstrumentationPointKind.RecruitmentPostedMessageSendObserved);
		var refreshed = Single(report, FindGroupMutationPostJavaInstrumentationPointKind.RecruitmentRefreshedListSendObserved);

		Assert.Equal(2, mutation.Action);
		Assert.Equal(2, posted.Action);
		Assert.Equal(2, refreshed.Action);
		Assert.True(mutation.Order < posted.Order);
		Assert.True(posted.Order < refreshed.Order);
		Assert.Contains("recruitments.put", mutation.JavaSource, StringComparison.Ordinal);
		Assert.Contains("stateMutationRecordedBeforeDirectPackets=true", mutation.RequiredFields, StringComparison.Ordinal);
		Assert.Contains("postedSystemMessageId=1400392", posted.RequiredFields, StringComparison.Ordinal);
		Assert.Contains("refreshedListAction=0", refreshed.RequiredFields, StringComparison.Ordinal);
		Assert.Contains("toList()", refreshed.Notes, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_RecordsApplicationMutationBeforePostedMessageBeforeRefreshedList()
	{
		var report = FindGroupMutationPostJavaInstrumentationDesignReportService.Create();

		var mutation = Single(report, FindGroupMutationPostJavaInstrumentationPointKind.ApplicationStateMutationRecorded);
		var posted = Single(report, FindGroupMutationPostJavaInstrumentationPointKind.ApplicationPostedMessageSendObserved);
		var refreshed = Single(report, FindGroupMutationPostJavaInstrumentationPointKind.ApplicationRefreshedListSendObserved);

		Assert.Equal(6, mutation.Action);
		Assert.Equal(6, posted.Action);
		Assert.Equal(6, refreshed.Action);
		Assert.True(mutation.Order < posted.Order);
		Assert.True(posted.Order < refreshed.Order);
		Assert.Contains("applications.put", mutation.JavaSource, StringComparison.Ordinal);
		Assert.Contains("mutationKind=Application", mutation.RequiredFields, StringComparison.Ordinal);
		Assert.Contains("postedSystemMessageId=1400393", posted.RequiredFields, StringComparison.Ordinal);
		Assert.Contains("refreshedListAction=4", refreshed.RequiredFields, StringComparison.Ordinal);
		Assert.Contains("toList()", refreshed.Notes, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_TraceSerializerRowReusesSchemaAndValidator()
	{
		var report = FindGroupMutationPostJavaInstrumentationDesignReportService.Create();

		Assert.Contains(report.Points, point =>
			point.Kind == FindGroupMutationPostJavaInstrumentationPointKind.TraceArtifactRowSerialized
			&& point.Status == FindGroupMutationPostJavaInstrumentationDesignStatus.BlockedMissingTraceSerializer
			&& point.JavaSource.Contains("FindGroupMutationPostJavaTraceArtifactValidatorService", StringComparison.Ordinal)
			&& point.RequiredFields.Contains("visibleEntryObjectIdsAfterMutation", StringComparison.Ordinal)
			&& point.Notes.Contains("traceName=cm-find-group-direct-mutation-post-boundary", StringComparison.Ordinal)
			&& point.Notes.Contains("validate with FindGroupMutationPostJavaTraceArtifactValidatorService", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_CaveatsProtectJavaTimingAndNonParityFields()
	{
		var report = FindGroupMutationPostJavaInstrumentationDesignReportService.Create();

		Assert.Contains(report.Caveats, caveat =>
			caveat.Caveat.Contains("Do not add synchronization", StringComparison.Ordinal)
			&& caveat.Risk.Contains("ConcurrentHashMap timing", StringComparison.Ordinal));
		Assert.Contains(report.Caveats, caveat =>
			caveat.Caveat.Contains("Do not perform blocking IO", StringComparison.Ordinal)
			&& caveat.Risk.Contains("packet ordering", StringComparison.Ordinal));
		Assert.Contains(report.Caveats, caveat =>
			caveat.Caveat.Contains("Do not alter PacketSendUtility send ordering", StringComparison.Ordinal)
			&& caveat.Risk.Contains("mutation-before-posted-message-before-refreshed-list", StringComparison.Ordinal));
		Assert.Contains(report.Caveats, caveat =>
			caveat.Caveat.Contains("Do not materialize visible lists before", StringComparison.Ordinal)
			&& caveat.Risk.Contains("snapshot timing", StringComparison.Ordinal));
		Assert.Contains(report.Caveats, caveat =>
			caveat.Caveat.Contains("Treat timestamps as diagnostics only", StringComparison.Ordinal)
			&& caveat.Risk.Contains("not objective parity evidence", StringComparison.Ordinal));
	}

	private static FindGroupMutationPostJavaInstrumentationPoint Single(
		FindGroupMutationPostJavaInstrumentationDesignReport report,
		FindGroupMutationPostJavaInstrumentationPointKind kind) =>
		report.Points.Single(point => point.Kind == kind);
}
