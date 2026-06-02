using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupDirectPacketMutationPostBoundaryTraceSchemaServiceTests
{
	[Fact]
	public void CreateSchema_DefinesStableVersionAndJavaMutationPostMappings()
	{
		var schema = FindGroupDirectPacketMutationPostBoundaryTraceSchemaService.CreateSchema();

		Assert.Equal(1, schema.SchemaVersion);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", schema.TraceName);
		Assert.Contains("Non-live schema only", schema.BoundaryNote, StringComparison.Ordinal);
		Assert.Contains("addRecruitment/addApplication", schema.JavaSource, StringComparison.Ordinal);
		Assert.Equal([2, 6], schema.SupportedActions.Select(action => action.Action));
		Assert.Contains(
			schema.SupportedActions,
			action => action.Action == 2
				&& action.MutationKind == FindGroupDirectPacketMutationPostTraceMutationKind.Recruitment
				&& action.JavaMethod.Contains("addRecruitment(player, message, groupType)", StringComparison.Ordinal)
				&& action.JavaPostedSystemMessage.Contains("STR_PARTY_MATCH_OFFER_PARTY_POSTED", StringComparison.Ordinal)
				&& action.PostedSystemMessageId == 1400392
				&& action.RefreshedShowListAction == 0);
		Assert.Contains(
			schema.SupportedActions,
			action => action.Action == 6
				&& action.MutationKind == FindGroupDirectPacketMutationPostTraceMutationKind.Application
				&& action.JavaMethod.Contains("addApplication(player, message, groupType, classId, level)", StringComparison.Ordinal)
				&& action.JavaPostedSystemMessage.Contains("STR_PARTY_MATCH_SEEK_PARTY_POSTED", StringComparison.Ordinal)
				&& action.PostedSystemMessageId == 1400393
				&& action.RefreshedShowListAction == 4);
	}

	[Fact]
	public void CreateSchema_RequiresStableTraceFieldOrder()
	{
		var schema = FindGroupDirectPacketMutationPostBoundaryTraceSchemaService.CreateSchema();

		Assert.Equal(
			[
				"schemaVersion",
				"traceName",
				"traceSource",
				"action",
				"boundaryAccepted",
				"activePlayerObjectId",
				"activePlayerRace",
				"serverEpochSeconds",
				"mutationKind",
				"mutatedEntryObjectId",
				"stateMutationRecordedBeforeDirectPackets",
				"postedSystemMessageRecipientObjectId",
				"postedSystemMessageType",
				"postedSystemMessageId",
				"refreshedListRecipientObjectId",
				"refreshedListPacketType",
				"refreshedListAction",
				"visibleEntryObjectIdsAfterMutation",
				"executorInvokedFromBoundary",
				"registrySendsObservedInOrder",
				"worldBroadcastCount",
				"inviteDispatchCount",
			],
			schema.RequiredFields.Select(field => field.Name));
		Assert.Contains(
			schema.RequiredFields,
			field => field.Name == "stateMutationRecordedBeforeDirectPackets"
				&& field.Requirement.Contains("before posted message", StringComparison.Ordinal));
		Assert.Contains(
			schema.RequiredFields,
			field => field.Name == "registrySendsObservedInOrder"
				&& field.Requirement.Contains("posted system message before refreshed show-list", StringComparison.Ordinal));
		Assert.Contains(
			schema.RequiredFields,
			field => field.Name == "worldBroadcastCount"
				&& field.Requirement.Contains("Must remain 0", StringComparison.Ordinal));
		Assert.Contains(
			schema.RequiredFields,
			field => field.Name == "inviteDispatchCount"
				&& field.Requirement.Contains("Must remain 0", StringComparison.Ordinal));
	}

	[Theory]
	[InlineData(2, FindGroupDirectPacketMutationPostTraceMutationKind.Recruitment, 1400392, 0)]
	[InlineData(6, FindGroupDirectPacketMutationPostTraceMutationKind.Application, 1400393, 4)]
	public void CreateSampleExport_ProjectsActionSpecificComparisonShape(
		int action,
		FindGroupDirectPacketMutationPostTraceMutationKind expectedMutationKind,
		int expectedPostedSystemMessageId,
		int expectedRefreshedShowListAction)
	{
		var export = FindGroupDirectPacketMutationPostBoundaryTraceSchemaService.CreateSampleExport(action);

		Assert.Equal(1, export.SchemaVersion);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", export.TraceName);
		Assert.Equal(FindGroupDirectPacketMutationPostTraceSource.CSharp, export.TraceSource);
		Assert.Equal(action, export.Action);
		Assert.Equal(expectedMutationKind, export.MutationKind);
		Assert.Equal("SmSystemMessage", export.PostedSystemMessageType);
		Assert.Equal(expectedPostedSystemMessageId, export.PostedSystemMessageId);
		Assert.Equal("SmFindGroup", export.RefreshedListPacketType);
		Assert.Equal(expectedRefreshedShowListAction, export.RefreshedListAction);
		Assert.False(export.BoundaryAccepted);
		Assert.False(export.StateMutationRecordedBeforeDirectPackets);
		Assert.False(export.ExecutorInvokedFromBoundary);
		Assert.False(export.RegistrySendsObservedInOrder);
		Assert.Equal(0, export.WorldBroadcastCount);
		Assert.Equal(0, export.InviteDispatchCount);
	}
}
