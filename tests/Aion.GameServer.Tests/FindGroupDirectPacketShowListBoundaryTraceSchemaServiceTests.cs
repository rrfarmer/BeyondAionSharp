using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupDirectPacketShowListBoundaryTraceSchemaServiceTests
{
	[Fact]
	public void CreateSchema_DefinesStableVersionAndJavaShowListMappings()
	{
		var schema = FindGroupDirectPacketShowListBoundaryTraceSchemaService.CreateSchema();

		Assert.Equal(1, schema.SchemaVersion);
		Assert.Equal("cm-find-group-direct-show-list-boundary", schema.TraceName);
		Assert.Contains("Non-live schema only", schema.BoundaryNote, StringComparison.Ordinal);
		Assert.Contains("showRecruitments/showApplications", schema.JavaSource, StringComparison.Ordinal);
		Assert.Equal(
			[0, 4],
			schema.SupportedActions.Select(action => action.Action));
		Assert.Contains(
			schema.SupportedActions,
			action => action.Action == 0
				&& action.ListKind == FindGroupDirectPacketShowListTraceListKind.Recruitments
				&& action.JavaMethod.Contains("showRecruitments(player)", StringComparison.Ordinal)
				&& action.JavaPacket.Contains("SM_FIND_GROUP action 0", StringComparison.Ordinal));
		Assert.Contains(
			schema.SupportedActions,
			action => action.Action == 4
				&& action.ListKind == FindGroupDirectPacketShowListTraceListKind.Applications
				&& action.JavaMethod.Contains("showApplications(player)", StringComparison.Ordinal)
				&& action.JavaPacket.Contains("SM_FIND_GROUP action 4", StringComparison.Ordinal));
	}

	[Fact]
	public void CreateSchema_RequiresStableTraceFieldOrder()
	{
		var schema = FindGroupDirectPacketShowListBoundaryTraceSchemaService.CreateSchema();

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
				"listKind",
				"visibleEntryObjectIds",
				"directPacketRecipientObjectId",
				"directPacketType",
				"directPacketAction",
				"executorInvokedFromBoundary",
				"registrySendObserved",
				"worldBroadcastCount",
				"inviteDispatchCount",
			],
			schema.RequiredFields.Select(field => field.Name));
		Assert.Contains(
			schema.RequiredFields,
			field => field.Name == "visibleEntryObjectIds"
				&& field.Requirement.Contains("materialized packet order", StringComparison.Ordinal));
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
	[InlineData(0, FindGroupDirectPacketShowListTraceListKind.Recruitments)]
	[InlineData(4, FindGroupDirectPacketShowListTraceListKind.Applications)]
	public void CreateSampleExport_ProjectsActionSpecificComparisonShape(
		int action,
		FindGroupDirectPacketShowListTraceListKind expectedListKind)
	{
		var export = FindGroupDirectPacketShowListBoundaryTraceSchemaService.CreateSampleExport(action);

		Assert.Equal(1, export.SchemaVersion);
		Assert.Equal("cm-find-group-direct-show-list-boundary", export.TraceName);
		Assert.Equal(FindGroupDirectPacketShowListTraceSource.CSharp, export.TraceSource);
		Assert.Equal(action, export.Action);
		Assert.Equal(expectedListKind, export.ListKind);
		Assert.Equal("SmFindGroup", export.DirectPacketType);
		Assert.Equal(action, export.DirectPacketAction);
		Assert.False(export.BoundaryAccepted);
		Assert.False(export.ExecutorInvokedFromBoundary);
		Assert.False(export.RegistrySendObserved);
		Assert.Equal(0, export.WorldBroadcastCount);
		Assert.Equal(0, export.InviteDispatchCount);
	}
}
