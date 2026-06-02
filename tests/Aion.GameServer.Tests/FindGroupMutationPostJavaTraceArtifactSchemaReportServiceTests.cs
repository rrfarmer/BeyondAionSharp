using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostJavaTraceArtifactSchemaReportServiceTests
{
	[Fact]
	public void Create_ReusesMutationPostBoundaryTraceSchemaFieldOrder()
	{
		var report = FindGroupMutationPostJavaTraceArtifactSchemaReportService.Create();
		var comparisonSchema = FindGroupDirectPacketMutationPostBoundaryTraceSchemaService.CreateSchema();

		Assert.Equal(1, report.SchemaVersion);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", report.TraceName);
		Assert.True(report.ReusesMutationPostBoundaryTraceSchema);
		Assert.Equal(comparisonSchema.RequiredFields.Select(field => field.Name), report.Fields.Select(field => field.Name));
		Assert.Contains(report.Fields, field =>
			field.Name == "stateMutationRecordedBeforeDirectPackets"
			&& field.JsonPath == "$.traces[*].stateMutationRecordedBeforeDirectPackets"
			&& field.FieldType == "boolean"
			&& field.Requirement.Contains("before posted message", StringComparison.Ordinal));
		Assert.Contains(report.Fields, field =>
			field.Name == "visibleEntryObjectIdsAfterMutation"
			&& field.FieldType == "integer array");
	}

	[Fact]
	public void Create_DefinesJavaActionMappingsForMutationPostTraceArtifacts()
	{
		var report = FindGroupMutationPostJavaTraceArtifactSchemaReportService.Create();

		Assert.True(report.HasRequiredActionMappings);
		Assert.Equal([2, 6], report.Actions.Select(action => action.Action));
		Assert.Contains(report.Actions, action =>
			action.Action == 2
			&& action.MutationKind == FindGroupDirectPacketMutationPostTraceMutationKind.Recruitment
			&& action.JavaMethod.Contains("addRecruitment", StringComparison.Ordinal)
			&& action.JavaPostedSystemMessage.Contains("STR_PARTY_MATCH_OFFER_PARTY_POSTED", StringComparison.Ordinal)
			&& action.PostedSystemMessageId == 1400392
			&& action.RefreshedShowListAction == 0);
		Assert.Contains(report.Actions, action =>
			action.Action == 6
			&& action.MutationKind == FindGroupDirectPacketMutationPostTraceMutationKind.Application
			&& action.JavaMethod.Contains("addApplication", StringComparison.Ordinal)
			&& action.JavaPostedSystemMessage.Contains("STR_PARTY_MATCH_SEEK_PARTY_POSTED", StringComparison.Ordinal)
			&& action.PostedSystemMessageId == 1400393
			&& action.RefreshedShowListAction == 4);
		Assert.All(report.Actions, action =>
			Assert.Equal(FindGroupMutationPostJavaTraceArtifactStatus.BlockedMissingJavaInstrumentation, action.Status));
	}

	[Fact]
	public void Create_RemainsBlockedUntilJavaInstrumentationAndSerializerExist()
	{
		var report = FindGroupMutationPostJavaTraceArtifactSchemaReportService.Create();

		Assert.True(report.RequiresJavaInstrumentation);
		Assert.True(report.RequiresTraceSerializer);
		Assert.False(report.ReadyForRuntimeComparison);
		Assert.Contains("no Java artifacts", report.BoundaryNote, StringComparison.Ordinal);
		Assert.Contains("CM_FIND_GROUP.runImpl actions 2 and 6", report.JavaSource, StringComparison.Ordinal);
		Assert.All(report.Fields, field =>
			Assert.Equal(FindGroupMutationPostJavaTraceArtifactStatus.BlockedMissingTraceSerializer, field.Status));
		Assert.Contains(report.InstrumentationCaveats, caveat =>
			caveat.Caveat.Contains("Do not add synchronization", StringComparison.Ordinal));
		Assert.Contains(report.InstrumentationCaveats, caveat =>
			caveat.Risk.Contains("mutation-before-posted-message-before-refreshed-list", StringComparison.Ordinal));
	}
}
