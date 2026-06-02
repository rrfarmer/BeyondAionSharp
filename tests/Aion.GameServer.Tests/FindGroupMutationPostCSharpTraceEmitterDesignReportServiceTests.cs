using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostCSharpTraceEmitterDesignReportServiceTests
{
	[Fact]
	public void Create_ListsNonLiveEmitterHookSites()
	{
		var report = FindGroupMutationPostCSharpTraceEmitterDesignReportService.Create();

		Assert.False(report.IsLive);
		Assert.True(report.HasBoundaryHookSite);
		Assert.True(report.HasMutationProjectionHookSite);
		Assert.True(report.HasDirectPacketHookSites);
		Assert.True(report.HasRuntimeRowSerializationPlan);
		Assert.True(report.ReusesMutationPostTraceSchema);
		Assert.True(report.RequiresLiveBoundaryCapture);
		Assert.True(report.RequiresLiveEmitter);
		Assert.False(report.ReadyForRuntimeComparison);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", report.TraceName);
		Assert.Equal(Enumerable.Range(1, report.Rows.Count), report.Rows.Select(row => row.Order));
	}

	[Fact]
	public void Create_ArtifactBoundaryReusesMutationPostSchemaFields()
	{
		var report = FindGroupMutationPostCSharpTraceEmitterDesignReportService.Create();

		Assert.Contains(report.Rows, row =>
			row.HookSite == FindGroupMutationPostCSharpTraceEmitterHookSite.ArtifactShapeValidationBoundary
			&& row.Status == FindGroupMutationPostCSharpTraceEmitterDesignStatus.ReadyForDesignOnly
			&& row.CSharpTarget.Contains("FindGroupMutationPostJavaTraceArtifactValidatorService", StringComparison.Ordinal)
			&& row.RequiredTraceFields.Contains("visibleEntryObjectIdsAfterMutation", StringComparison.Ordinal)
			&& row.RequiredTraceFields.Contains("registrySendsObservedInOrder", StringComparison.Ordinal)
			&& row.Notes.Contains("does not prove parity", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_BoundaryAndExecutorRowsRemainBlockedUntilLiveCaptureExists()
	{
		var report = FindGroupMutationPostCSharpTraceEmitterDesignReportService.Create();

		Assert.Contains(report.Rows, row =>
			row.HookSite == FindGroupMutationPostCSharpTraceEmitterHookSite.ConnectionBoundaryAccepted
			&& row.Status == FindGroupMutationPostCSharpTraceEmitterDesignStatus.BlockedMissingLiveBoundaryCapture
			&& row.CSharpTarget.Contains("GameServerConnection.ProcessPacketAsync", StringComparison.Ordinal)
			&& row.RequiredTraceFields.Contains("traceSource=CSharp", StringComparison.Ordinal)
			&& row.RequiredTraceFields.Contains("boundaryAccepted", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.HookSite == FindGroupMutationPostCSharpTraceEmitterHookSite.BoundaryExecutorInvocation
			&& row.Status == FindGroupMutationPostCSharpTraceEmitterDesignStatus.BlockedMissingLiveBoundaryCapture
			&& row.RequiredTraceFields.Contains("executorInvokedFromBoundary", StringComparison.Ordinal)
			&& row.Notes.Contains("Disabled executor evidence is not enough", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_MutationAndDirectPacketRowsPreserveJavaActionTwoAndSixOrdering()
	{
		var report = FindGroupMutationPostCSharpTraceEmitterDesignReportService.Create();

		Assert.Contains(report.Rows, row =>
			row.HookSite == FindGroupMutationPostCSharpTraceEmitterHookSite.SingletonMutationProjection
			&& row.JavaSource.Contains("FindGroupService.addRecruitment/addApplication", StringComparison.Ordinal)
			&& row.CSharpTarget.Contains("FindGroupRecruitmentPlanService", StringComparison.Ordinal)
			&& row.RequiredTraceFields.Contains("stateMutationRecordedBeforeDirectPackets", StringComparison.Ordinal)
			&& row.RequiredTraceFields.Contains("visibleEntryObjectIdsAfterMutation", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.HookSite == FindGroupMutationPostCSharpTraceEmitterHookSite.DirectPacketIntentMaterialized
			&& row.RequiredTraceFields.Contains("postedSystemMessageId", StringComparison.Ordinal)
			&& row.RequiredTraceFields.Contains("refreshedListAction", StringComparison.Ordinal)
			&& row.Notes.Contains("posted-system-message-before-refreshed-list ordering", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_RegistryAndSerializationRowsKeepRuntimeComparisonBlocked()
	{
		var report = FindGroupMutationPostCSharpTraceEmitterDesignReportService.Create();

		Assert.Contains(report.Rows, row =>
			row.HookSite == FindGroupMutationPostCSharpTraceEmitterHookSite.RegistrySendObservation
			&& row.Status == FindGroupMutationPostCSharpTraceEmitterDesignStatus.BlockedMissingLiveBoundaryCapture
			&& row.RequiredTraceFields.Contains("registrySendsObservedInOrder", StringComparison.Ordinal)
			&& row.Notes.Contains("synthetic or disabled intent ordering is only partial evidence", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.HookSite == FindGroupMutationPostCSharpTraceEmitterHookSite.RuntimeTraceRowSerialized
			&& row.Status == FindGroupMutationPostCSharpTraceEmitterDesignStatus.BlockedMissingLiveEmitter
			&& row.RequiredTraceFields.Contains("traceName=cm-find-group-direct-mutation-post-boundary", StringComparison.Ordinal)
			&& row.RequiredTraceFields.Contains("supportedActions=2/6", StringComparison.Ordinal)
			&& row.Notes.Contains("runtime comparison remains blocked", StringComparison.Ordinal));
	}
}
