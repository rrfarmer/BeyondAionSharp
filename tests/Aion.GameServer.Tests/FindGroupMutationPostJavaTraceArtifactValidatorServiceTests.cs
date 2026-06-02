using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostJavaTraceArtifactValidatorServiceTests
{
	[Fact]
	public void Validate_AcceptsRepresentativeActionTwoAndSixArtifact()
	{
		var report = FindGroupMutationPostJavaTraceArtifactValidatorService.Validate(RepresentativeArtifactJson);

		Assert.True(report.IsValid);
		Assert.Empty(report.Issues);
		Assert.NotNull(report.Metadata);
		Assert.Equal(1, report.Metadata.SchemaVersion);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", report.Metadata.TraceName);
		Assert.Equal([2, 6], report.Metadata.TraceRows.Select(row => row.Action));
		Assert.Contains(report.Metadata.TraceRows, row =>
			row.Action == 2
			&& row.TraceSource == "Java"
			&& row.MutationKind == "Recruitment"
			&& row.PostedSystemMessageId == 1400392
			&& row.RefreshedListAction == 0);
		Assert.Contains(report.Metadata.TraceRows, row =>
			row.Action == 6
			&& row.TraceSource == "Java"
			&& row.MutationKind == "Application"
			&& row.PostedSystemMessageId == 1400393
			&& row.RefreshedListAction == 4);
	}

	[Fact]
	public void Validate_RejectsUnsupportedSchemaVersion()
	{
		var report = FindGroupMutationPostJavaTraceArtifactValidatorService.Validate(
			RepresentativeArtifactJson.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2", StringComparison.Ordinal));

		Assert.False(report.IsValid);
		Assert.Contains(report.Issues, issue =>
			issue.Code == FindGroupMutationPostJavaTraceArtifactValidationIssueCode.UnsupportedSchemaVersion
			&& issue.Path == "$.schemaVersion");
	}

	[Fact]
	public void Validate_RejectsMissingTraceRows()
	{
		var report = FindGroupMutationPostJavaTraceArtifactValidatorService.Validate(
			"""
			{
			  "schemaVersion": 1,
			  "traceName": "cm-find-group-direct-mutation-post-boundary",
			  "traces": []
			}
			""");

		Assert.False(report.IsValid);
		Assert.Contains(report.Issues, issue =>
			issue.Code == FindGroupMutationPostJavaTraceArtifactValidationIssueCode.MissingTraceRows
			&& issue.Path == "$.traces");
	}

	[Fact]
	public void Validate_RejectsMissingRequiredField()
	{
		var report = FindGroupMutationPostJavaTraceArtifactValidatorService.Validate(
			RepresentativeArtifactJson.Replace("\"postedSystemMessageId\": 1400392,", string.Empty, StringComparison.Ordinal));

		Assert.False(report.IsValid);
		Assert.Contains(report.Issues, issue =>
			issue.Code == FindGroupMutationPostJavaTraceArtifactValidationIssueCode.MissingField
			&& issue.Path == "$.traces[0].postedSystemMessageId");
	}

	[Fact]
	public void Validate_RejectsUnsupportedAction()
	{
		var report = FindGroupMutationPostJavaTraceArtifactValidatorService.Validate(
			RepresentativeArtifactJson.Replace("\"action\": 2", "\"action\": 4", StringComparison.Ordinal));

		Assert.False(report.IsValid);
		Assert.Contains(report.Issues, issue =>
			issue.Code == FindGroupMutationPostJavaTraceArtifactValidationIssueCode.UnsupportedAction
			&& issue.Path == "$.traces[0].action");
	}

	[Fact]
	public void Validate_RejectsActionMappingMismatch()
	{
		var report = FindGroupMutationPostJavaTraceArtifactValidatorService.Validate(
			RepresentativeArtifactJson.Replace("\"postedSystemMessageId\": 1400393", "\"postedSystemMessageId\": 1400392", StringComparison.Ordinal));

		Assert.False(report.IsValid);
		Assert.Contains(report.Issues, issue =>
			issue.Code == FindGroupMutationPostJavaTraceArtifactValidationIssueCode.ActionMappingMismatch
			&& issue.Path == "$.traces[1]");
	}

	[Fact]
	public void Validate_RejectsNonJavaTraceSource()
	{
		var report = FindGroupMutationPostJavaTraceArtifactValidatorService.Validate(
			RepresentativeArtifactJson.Replace("\"traceSource\": \"Java\"", "\"traceSource\": \"CSharp\"", StringComparison.Ordinal));

		Assert.False(report.IsValid);
		Assert.Contains(report.Issues, issue =>
			issue.Code == FindGroupMutationPostJavaTraceArtifactValidationIssueCode.UnexpectedTraceSource
			&& issue.Path == "$.traces[0].traceSource");
	}

	private const string RepresentativeArtifactJson =
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
		      "activePlayerObjectId": 16909060,
		      "activePlayerRace": "ELYOS",
		      "serverEpochSeconds": 200,
		      "mutationKind": "Recruitment",
		      "mutatedEntryObjectId": 16909060,
		      "stateMutationRecordedBeforeDirectPackets": true,
		      "postedSystemMessageRecipientObjectId": 16909060,
		      "postedSystemMessageType": "SmSystemMessage",
		      "postedSystemMessageId": 1400392,
		      "refreshedListRecipientObjectId": 16909060,
		      "refreshedListPacketType": "SmFindGroup",
		      "refreshedListAction": 0,
		      "visibleEntryObjectIdsAfterMutation": [16909060],
		      "executorInvokedFromBoundary": true,
		      "registrySendsObservedInOrder": true,
		      "worldBroadcastCount": 0,
		      "inviteDispatchCount": 0
		    },
		    {
		      "schemaVersion": 1,
		      "traceName": "cm-find-group-direct-mutation-post-boundary",
		      "traceSource": "Java",
		      "action": 6,
		      "boundaryAccepted": true,
		      "activePlayerObjectId": 16909061,
		      "activePlayerRace": "ASMODIANS",
		      "serverEpochSeconds": 201,
		      "mutationKind": "Application",
		      "mutatedEntryObjectId": 16909061,
		      "stateMutationRecordedBeforeDirectPackets": true,
		      "postedSystemMessageRecipientObjectId": 16909061,
		      "postedSystemMessageType": "SmSystemMessage",
		      "postedSystemMessageId": 1400393,
		      "refreshedListRecipientObjectId": 16909061,
		      "refreshedListPacketType": "SmFindGroup",
		      "refreshedListAction": 4,
		      "visibleEntryObjectIdsAfterMutation": [16909061],
		      "executorInvokedFromBoundary": true,
		      "registrySendsObservedInOrder": true,
		      "worldBroadcastCount": 0,
		      "inviteDispatchCount": 0
		    }
		  ]
		}
		""";
}
