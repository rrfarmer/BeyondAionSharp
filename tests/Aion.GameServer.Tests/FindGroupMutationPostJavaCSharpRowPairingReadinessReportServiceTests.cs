using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostJavaCSharpRowPairingReadinessReportServiceTests
{
	[Fact]
	public void Create_DefaultReportBlocksOnMissingJavaArtifactsFirst()
	{
		var report = FindGroupMutationPostJavaCSharpRowPairingReadinessReportService.Create();

		Assert.Equal(FindGroupMutationPostJavaCSharpRowPairingReadinessReportStatus.BlockedJavaArtifactsMissingOrInvalid, report.Status);
		Assert.False(report.IsLive);
		Assert.False(report.HasShapeValidJavaArtifacts);
		Assert.False(report.HasAcceptedCSharpBoundaryRows);
		Assert.False(report.HasAllActionMutationPairs);
		Assert.False(report.CanFeedValueProjection);
		Assert.False(report.CanRunRuntimeComparison);
		Assert.False(report.CanClaimVerifiedParity);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", report.TraceName);
		Assert.Contains("addRecruitment/addApplication", report.JavaSource, StringComparison.Ordinal);
		Assert.Equal([2, 6], report.Rows.Select(row => row.Action));
		Assert.All(report.Rows, row =>
		{
			Assert.False(row.HasShapeValidJavaArtifact);
			Assert.False(row.CanFeedValueProjection);
			Assert.Equal(FindGroupMutationPostJavaCSharpRowPairingReadinessRowStatus.BlockedMissingShapeValidJavaArtifact, row.Status);
			Assert.Contains("csharpHandoffStatus=BlockedMissingAcceptedBoundaryRows", row.CurrentEvidence, StringComparison.Ordinal);
			Assert.Contains("requiredBoundaryFields=action,mutationKind,boundaryAccepted", row.CurrentEvidence, StringComparison.Ordinal);
		});
		Assert.Contains("explicit-root Java action 2/6 artifacts", report.ExecutionDecision, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_ShapeValidJavaArtifactsBlockUntilCSharpAcceptedRowsExist()
	{
		using var root = JavaArtifactRoot();
		var javaSummary = FindGroupMutationPostExplicitRootJavaPostCaptureValidatorSummaryService.Create(root.Path);

		var report = FindGroupMutationPostJavaCSharpRowPairingReadinessReportService.Create(javaSummary);

		Assert.Equal(FindGroupMutationPostJavaCSharpRowPairingReadinessReportStatus.BlockedCSharpBoundaryRowsMissing, report.Status);
		Assert.True(report.HasShapeValidJavaArtifacts);
		Assert.False(report.HasAcceptedCSharpBoundaryRows);
		Assert.False(report.HasAllActionMutationPairs);
		Assert.All(report.Rows, row =>
		{
			Assert.True(row.HasShapeValidJavaArtifact);
			Assert.False(row.HasAcceptedCSharpBoundaryRow);
			Assert.Equal(FindGroupMutationPostJavaCSharpRowPairingReadinessRowStatus.BlockedMissingAcceptedCSharpBoundaryRow, row.Status);
			Assert.Contains("csharpHandoffCanFeedJavaArtifactPairing=False", row.CurrentEvidence, StringComparison.Ordinal);
		});
		Assert.Contains("accepted-boundary-row handoff", report.ExecutionDecision, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_ShapeValidJavaAndAcceptedCSharpRowsCanFeedValueProjectionButNotParity()
	{
		using var root = JavaArtifactRoot();
		var javaSummary = FindGroupMutationPostExplicitRootJavaPostCaptureValidatorSummaryService.Create(root.Path);
		var csharpHandoff = AcceptedCSharpHandoff(LiveRow(2), LiveRow(6));

		var report = FindGroupMutationPostJavaCSharpRowPairingReadinessReportService.Create(javaSummary, csharpHandoff);

		Assert.Equal(FindGroupMutationPostJavaCSharpRowPairingReadinessReportStatus.ReadyForValueProjectionRuntimeComparisonBlocked, report.Status);
		Assert.True(report.HasShapeValidJavaArtifacts);
		Assert.True(report.HasAcceptedCSharpBoundaryRows);
		Assert.True(report.HasActionTwoPair);
		Assert.True(report.HasActionSixPair);
		Assert.True(report.HasAllActionMutationPairs);
		Assert.True(report.CanFeedValueProjection);
		Assert.False(report.CanRunRuntimeComparison);
		Assert.False(report.CanClaimVerifiedParity);
		Assert.Contains(report.Rows, row =>
			row.Action == 2
			&& row.ExpectedMutationKind == FindGroupDirectPacketMutationPostTraceMutationKind.Recruitment
			&& row.Status == FindGroupMutationPostJavaCSharpRowPairingReadinessRowStatus.ReadyForValueProjection
			&& row.CurrentEvidence.Contains("csharpHandoffStatus=ReadyForJavaArtifactPairingRuntimeComparisonBlocked", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.Action == 6
			&& row.ExpectedMutationKind == FindGroupDirectPacketMutationPostTraceMutationKind.Application
			&& row.Status == FindGroupMutationPostJavaCSharpRowPairingReadinessRowStatus.ReadyForValueProjection);
		Assert.Contains("value projection", report.ExecutionDecision, StringComparison.Ordinal);
		Assert.Contains("verified parity remain blocked", report.ExecutionDecision, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_MissingOneCSharpAcceptedActionBlocksThatPair()
	{
		using var root = JavaArtifactRoot();
		var javaSummary = FindGroupMutationPostExplicitRootJavaPostCaptureValidatorSummaryService.Create(root.Path);
		var csharpHandoff = AcceptedCSharpHandoff(LiveRow(2));

		var report = FindGroupMutationPostJavaCSharpRowPairingReadinessReportService.Create(javaSummary, csharpHandoff);

		Assert.Equal(FindGroupMutationPostJavaCSharpRowPairingReadinessReportStatus.BlockedCSharpBoundaryRowsMissing, report.Status);
		Assert.False(report.HasAcceptedCSharpBoundaryRows);
		Assert.False(report.HasActionTwoPair);
		Assert.False(report.HasActionSixPair);
		Assert.Contains(report.Rows, row =>
			row.Action == 2
			&& row.HasAcceptedCSharpBoundaryRow
			&& !row.HasActionMutationPairingIdentity
			&& row.Status == FindGroupMutationPostJavaCSharpRowPairingReadinessRowStatus.BlockedMissingPairingIdentity);
		Assert.Contains(report.Rows, row =>
			row.Action == 6
			&& row.HasShapeValidJavaArtifact
			&& !row.HasAcceptedCSharpBoundaryRow
			&& row.Status == FindGroupMutationPostJavaCSharpRowPairingReadinessRowStatus.BlockedMissingAcceptedCSharpBoundaryRow);
	}

	[Fact]
	public void Create_InvalidJavaArtifactBlocksEvenWhenCSharpRowsAreAccepted()
	{
		using var root = JavaArtifactRoot(actionSixJson: ActionSixArtifactJson.Replace("\"mutationKind\": \"Application\"", "\"mutationKind\": \"Recruitment\"", StringComparison.Ordinal));
		var javaSummary = FindGroupMutationPostExplicitRootJavaPostCaptureValidatorSummaryService.Create(root.Path);
		var csharpHandoff = AcceptedCSharpHandoff(LiveRow(2), LiveRow(6));

		var report = FindGroupMutationPostJavaCSharpRowPairingReadinessReportService.Create(javaSummary, csharpHandoff);

		Assert.Equal(FindGroupMutationPostJavaCSharpRowPairingReadinessReportStatus.BlockedJavaArtifactsMissingOrInvalid, report.Status);
		Assert.False(report.HasShapeValidJavaArtifacts);
		Assert.True(report.HasAcceptedCSharpBoundaryRows);
		Assert.False(report.HasAllActionMutationPairs);
		Assert.Contains(report.Rows, row =>
			row.Action == 6
			&& row.JavaFileStatus == FindGroupMutationPostJavaTraceArtifactDirectoryFileStatus.InvalidArtifact
			&& !row.HasShapeValidJavaArtifact
			&& row.Status == FindGroupMutationPostJavaCSharpRowPairingReadinessRowStatus.BlockedMissingShapeValidJavaArtifact);
	}

	private static FindGroupMutationPostCSharpAcceptedBoundaryRowHandoffReport AcceptedCSharpHandoff(
		params FindGroupDirectPacketMutationPostBoundaryTraceExport[] rows)
	{
		var guardedResult = FindGroupMutationPostGuardedFixtureResultContractService.Create(candidateRows: rows);
		var intake = FindGroupMutationPostCSharpLiveBoundaryRowIntakePreflightService.Create(guardedResult);
		return FindGroupMutationPostCSharpAcceptedBoundaryRowHandoffReportService.Create(intake);
	}

	private static FindGroupDirectPacketMutationPostBoundaryTraceExport LiveRow(int action) =>
		FindGroupDirectPacketMutationPostBoundaryTraceSchemaService.CreateSampleExport(action) with
		{
			BoundaryAccepted = true,
			ActivePlayerObjectId = action == 2 ? 1001 : 1002,
			ActivePlayerRace = "ELYOS",
			ServerEpochSeconds = 1700000000,
			MutatedEntryObjectId = action == 2 ? 2001 : 2002,
			StateMutationRecordedBeforeDirectPackets = true,
			PostedSystemMessageRecipientObjectId = action == 2 ? 1001 : 1002,
			RefreshedListRecipientObjectId = action == 2 ? 1001 : 1002,
			VisibleEntryObjectIdsAfterMutation = [action == 2 ? 2001 : 2002],
			ExecutorInvokedFromBoundary = true,
			RegistrySendsObservedInOrder = true,
			WorldBroadcastCount = 0,
			InviteDispatchCount = 0,
		};

	private static TempArtifactRoot JavaArtifactRoot(string? actionTwoJson = null, string? actionSixJson = null)
	{
		var root = new TempArtifactRoot();
		File.WriteAllText(Path.Combine(root.Path, FindGroupMutationPostJavaTraceArtifactFileReportService.FileNameForAction(2)), actionTwoJson ?? ActionTwoArtifactJson);
		File.WriteAllText(Path.Combine(root.Path, FindGroupMutationPostJavaTraceArtifactFileReportService.FileNameForAction(6)), actionSixJson ?? ActionSixArtifactJson);
		return root;
	}

	private sealed class TempArtifactRoot : IDisposable
	{
		public TempArtifactRoot()
		{
			Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"find-group-row-pairing-{Guid.NewGuid():N}");
			Directory.CreateDirectory(Path);
		}

		public string Path { get; }

		public void Dispose()
		{
			Directory.Delete(Path, recursive: true);
		}
	}

	private const string ActionTwoArtifactJson =
		"""
		{
		  "schemaVersion": 1,
		  "traceName": "cm-find-group-direct-mutation-post-boundary",
		  "traces": [
		    {
		      "schemaVersion": 1,
		      "traceName": "cm-find-group-direct-mutation-post-boundary",
		      "traceSource": "Java",
		      "action": 2,
		      "boundaryAccepted": true,
		      "activePlayerObjectId": 2002,
		      "activePlayerRace": "ELYOS",
		      "serverEpochSeconds": 1700000000,
		      "mutationKind": "Recruitment",
		      "mutatedEntryObjectId": 2002,
		      "stateMutationRecordedBeforeDirectPackets": true,
		      "postedSystemMessageRecipientObjectId": 2002,
		      "postedSystemMessageType": "SmSystemMessage",
		      "postedSystemMessageId": 1400392,
		      "refreshedListRecipientObjectId": 2002,
		      "refreshedListPacketType": "SmFindGroup",
		      "refreshedListAction": 0,
		      "visibleEntryObjectIdsAfterMutation": [2002, 3003],
		      "executorInvokedFromBoundary": false,
		      "registrySendsObservedInOrder": false,
		      "worldBroadcastCount": 0,
		      "inviteDispatchCount": 0
		    }
		  ]
		}
		""";

	private const string ActionSixArtifactJson =
		"""
		{
		  "schemaVersion": 1,
		  "traceName": "cm-find-group-direct-mutation-post-boundary",
		  "traces": [
		    {
		      "schemaVersion": 1,
		      "traceName": "cm-find-group-direct-mutation-post-boundary",
		      "traceSource": "Java",
		      "action": 6,
		      "boundaryAccepted": true,
		      "activePlayerObjectId": 4004,
		      "activePlayerRace": "ASMODIANS",
		      "serverEpochSeconds": 1700000000,
		      "mutationKind": "Application",
		      "mutatedEntryObjectId": 4004,
		      "stateMutationRecordedBeforeDirectPackets": true,
		      "postedSystemMessageRecipientObjectId": 4004,
		      "postedSystemMessageType": "SmSystemMessage",
		      "postedSystemMessageId": 1400393,
		      "refreshedListRecipientObjectId": 4004,
		      "refreshedListPacketType": "SmFindGroup",
		      "refreshedListAction": 4,
		      "visibleEntryObjectIdsAfterMutation": [4004],
		      "executorInvokedFromBoundary": false,
		      "registrySendsObservedInOrder": false,
		      "worldBroadcastCount": 0,
		      "inviteDispatchCount": 0
		    }
		  ]
		}
		""";
}
