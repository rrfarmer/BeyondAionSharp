using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostProjectedRowValueComparisonExecutorServiceTests
{
	[Fact]
	public void Compare_ShapeValidJavaArtifactsAndAcceptedCSharpRowsMatchesConcreteFieldsWithoutClaimingParity()
	{
		var report = FindGroupMutationPostProjectedRowValueComparisonExecutorService.Compare(
			ShapeValidJavaArtifacts(),
			AcceptedCSharpRows());

		Assert.Equal(FindGroupMutationPostProjectedRowValueComparisonStatus.Compared, report.Status);
		Assert.True(report.IsLive);
		Assert.True(report.HasActionTwoJavaRow);
		Assert.True(report.HasActionSixJavaRow);
		Assert.True(report.HasActionTwoAcceptedCSharpRow);
		Assert.True(report.HasActionSixAcceptedCSharpRow);
		Assert.True(report.AllComparedFieldsMatched);
		Assert.False(report.CanClaimVerifiedParity);
		Assert.Equal(
			[
				"action",
				"mutationKind",
				"activePlayerObjectId",
				"mutatedEntryObjectId",
				"postedSystemMessageId",
				"refreshedListAction",
				"visibleEntryObjectIdsAfterMutation",
				"worldBroadcastCount",
				"inviteDispatchCount",
			],
			report.ComparedFields);
		Assert.Equal(18, report.Rows.Count);
		Assert.All(report.Rows, row => Assert.Equal(FindGroupMutationPostProjectedRowValueComparisonResultKind.Matched, row.ResultKind));
		Assert.Contains(report.Rows, row =>
			row.Action == 2
			&& row.MutationKind == FindGroupDirectPacketMutationPostTraceMutationKind.Recruitment
			&& row.FieldName == "postedSystemMessageId"
			&& row.JavaValue == "1400392"
			&& row.CSharpValue == "1400392"
			&& row.DifferenceKind == FindGroupMutationPostComparisonDifferenceKind.DirectPacketMismatch
			&& row.JavaSource.Contains("STR_PARTY_MATCH_OFFER_PARTY_POSTED", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.Action == 6
			&& row.MutationKind == FindGroupDirectPacketMutationPostTraceMutationKind.Application
			&& row.FieldName == "visibleEntryObjectIdsAfterMutation"
			&& row.JavaValue == "[4004]"
			&& row.CSharpValue == "[4004]"
			&& row.DifferenceKind == FindGroupMutationPostComparisonDifferenceKind.MutationStateMismatch);
	}

	[Fact]
	public void Compare_VisibleEntryMismatchEmitsFieldMismatch()
	{
		var rows = AcceptedCSharpRows();
		rows[0] = rows[0] with { VisibleEntryObjectIdsAfterMutation = [2002] };

		var report = FindGroupMutationPostProjectedRowValueComparisonExecutorService.Compare(
			ShapeValidJavaArtifacts(),
			rows);

		Assert.Equal(FindGroupMutationPostProjectedRowValueComparisonStatus.Compared, report.Status);
		Assert.False(report.AllComparedFieldsMatched);
		Assert.Contains(report.Rows, row =>
			row.Action == 2
			&& row.FieldName == "visibleEntryObjectIdsAfterMutation"
			&& row.ResultKind == FindGroupMutationPostProjectedRowValueComparisonResultKind.FieldMismatch
			&& row.JavaValue == "[2002,3003]"
			&& row.CSharpValue == "[2002]"
			&& row.DifferenceKind == FindGroupMutationPostComparisonDifferenceKind.MutationStateMismatch);
		Assert.Contains(report.Rows, row =>
			row.Action == 2
			&& row.FieldName == "postedSystemMessageId"
			&& row.ResultKind == FindGroupMutationPostProjectedRowValueComparisonResultKind.Matched);
	}

	[Fact]
	public void Compare_MissingAcceptedCSharpActionBlocksComparison()
	{
		var report = FindGroupMutationPostProjectedRowValueComparisonExecutorService.Compare(
			ShapeValidJavaArtifacts(),
			[AcceptedCSharpRow(2)]);

		Assert.Equal(FindGroupMutationPostProjectedRowValueComparisonStatus.BlockedMissingAcceptedCSharpRows, report.Status);
		Assert.False(report.IsLive);
		Assert.True(report.HasActionTwoAcceptedCSharpRow);
		Assert.False(report.HasActionSixAcceptedCSharpRow);
		Assert.False(report.AllComparedFieldsMatched);
		Assert.Contains(report.Rows, row =>
			row.Action == 6
			&& row.FieldName == "row"
			&& row.ResultKind == FindGroupMutationPostProjectedRowValueComparisonResultKind.MissingCSharpRow);
	}

	private static FindGroupMutationPostJavaTraceArtifactDirectoryReport ShapeValidJavaArtifacts() =>
		new(
			FindGroupMutationPostJavaTraceArtifactDirectoryStatus.AllExpectedArtifactsShapeValid,
			FindGroupMutationPostJavaTraceArtifactFileReportService.DefaultArtifactRoot,
			[
				ShapeValidFile(2),
				ShapeValidFile(6),
			],
			HasGeneratedJavaArtifacts: true,
			HasAllExpectedFiles: true,
			HasOnlyShapeValidArtifacts: true,
			ReadyForRuntimeComparison: false,
			"shape-valid Java artifacts from FindGroupMutationPostTraceCaptureTest");

	private static FindGroupMutationPostJavaTraceArtifactDirectoryFileRow ShapeValidFile(int action) =>
		new(
			action,
			FindGroupMutationPostJavaTraceArtifactFileReportService.FileNameForAction(action),
			FindGroupMutationPostJavaTraceArtifactDirectoryFileStatus.ShapeValid,
			new FindGroupMutationPostJavaTraceArtifactValidationReport(
				[],
				IsValid: true,
				new FindGroupMutationPostJavaTraceArtifactMetadata(
					SchemaVersion: 1,
					TraceName: "cm-find-group-direct-mutation-post-boundary",
					[JavaRow(action)])),
			"shape-valid Java artifact row");

	private static FindGroupMutationPostJavaTraceArtifactValidationTraceRow JavaRow(int action) =>
		action == 2
			? new FindGroupMutationPostJavaTraceArtifactValidationTraceRow(
				SchemaVersion: 1,
				TraceName: "cm-find-group-direct-mutation-post-boundary",
				TraceSource: "Java",
				action,
				MutationKind: "Recruitment",
				PostedSystemMessageId: 1400392,
				RefreshedListAction: 0,
				BoundaryAccepted: true,
				ActivePlayerObjectId: 2002,
				ActivePlayerRace: "ELYOS",
				ServerEpochSeconds: 1700000000,
				MutatedEntryObjectId: 2002,
				StateMutationRecordedBeforeDirectPackets: true,
				PostedSystemMessageRecipientObjectId: 2002,
				PostedSystemMessageType: "SmSystemMessage",
				RefreshedListRecipientObjectId: 2002,
				RefreshedListPacketType: "SmFindGroup",
				VisibleEntryObjectIdsAfterMutation: [2002, 3003],
				ExecutorInvokedFromBoundary: false,
				RegistrySendsObservedInOrder: false,
				WorldBroadcastCount: 0,
				InviteDispatchCount: 0)
			: new FindGroupMutationPostJavaTraceArtifactValidationTraceRow(
				SchemaVersion: 1,
				TraceName: "cm-find-group-direct-mutation-post-boundary",
				TraceSource: "Java",
				action,
				MutationKind: "Application",
				PostedSystemMessageId: 1400393,
				RefreshedListAction: 4,
				BoundaryAccepted: true,
				ActivePlayerObjectId: 4004,
				ActivePlayerRace: "ASMODIANS",
				ServerEpochSeconds: 1700000000,
				MutatedEntryObjectId: 4004,
				StateMutationRecordedBeforeDirectPackets: true,
				PostedSystemMessageRecipientObjectId: 4004,
				PostedSystemMessageType: "SmSystemMessage",
				RefreshedListRecipientObjectId: 4004,
				RefreshedListPacketType: "SmFindGroup",
				VisibleEntryObjectIdsAfterMutation: [4004],
				ExecutorInvokedFromBoundary: false,
				RegistrySendsObservedInOrder: false,
				WorldBroadcastCount: 0,
				InviteDispatchCount: 0);

	private static FindGroupDirectPacketMutationPostBoundaryTraceExport[] AcceptedCSharpRows() =>
		[
			AcceptedCSharpRow(2),
			AcceptedCSharpRow(6),
		];

	private static FindGroupDirectPacketMutationPostBoundaryTraceExport AcceptedCSharpRow(int action)
	{
		return FindGroupDirectPacketMutationPostBoundaryTraceSchemaService.CreateSampleExport(action) with
		{
			BoundaryAccepted = true,
			ActivePlayerObjectId = action == 2 ? 2002 : 4004,
			ActivePlayerRace = action == 2 ? "ELYOS" : "ASMODIANS",
			ServerEpochSeconds = 1700000000,
			MutatedEntryObjectId = action == 2 ? 2002 : 4004,
			StateMutationRecordedBeforeDirectPackets = true,
			PostedSystemMessageRecipientObjectId = action == 2 ? 2002 : 4004,
			RefreshedListRecipientObjectId = action == 2 ? 2002 : 4004,
			VisibleEntryObjectIdsAfterMutation = action == 2 ? [2002, 3003] : [4004],
			ExecutorInvokedFromBoundary = true,
			RegistrySendsObservedInOrder = true,
			WorldBroadcastCount = 0,
			InviteDispatchCount = 0,
		};
	}
}
