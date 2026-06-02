using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostComparisonKeyProjectionMetadataServiceTests
{
	[Fact]
	public void Create_KeepsProjectionBlockedAndNonLive()
	{
		var metadata = FindGroupMutationPostComparisonKeyProjectionMetadataService.Create();

		Assert.Equal(FindGroupMutationPostComparisonKeyProjectionStatus.BlockedPendingTraceRows, metadata.Status);
		Assert.False(metadata.IsLive);
		Assert.True(metadata.RequiresGeneratedJavaTraceRows);
		Assert.True(metadata.RequiresLiveCSharpTraceRows);
		Assert.True(metadata.RequiresRegistryObservation);
		Assert.False(metadata.ReadyForRuntimeComparison);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", metadata.TraceName);
		Assert.Contains("addRecruitment/addApplication", metadata.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_CoversActionTwoAndSixOnlyWithStableOrdering()
	{
		var metadata = FindGroupMutationPostComparisonKeyProjectionMetadataService.Create();

		Assert.Equal([2, 6], metadata.Actions);
		Assert.Equal(42, metadata.Fields.Count);
		Assert.Equal(Enumerable.Range(1, metadata.Fields.Count), metadata.Fields.Select(field => field.Order));
		Assert.All(metadata.Fields, field => Assert.Contains(field.Action, new[] { 2, 6 }));
		Assert.Contains(metadata.Fields, field =>
			field.Action == 2
			&& field.MutationKind == FindGroupDirectPacketMutationPostTraceMutationKind.Recruitment);
		Assert.Contains(metadata.Fields, field =>
			field.Action == 6
			&& field.MutationKind == FindGroupDirectPacketMutationPostTraceMutationKind.Application);
	}

	[Fact]
	public void Create_DefinesCompatibilityGatesAndRowIdentity()
	{
		var metadata = FindGroupMutationPostComparisonKeyProjectionMetadataService.Create();

		Assert.Equal(["schemaVersion", "traceName"], metadata.CompatibilityGateFields);
		Assert.Equal(["action", "mutationKind", "activePlayerObjectId", "mutatedEntryObjectId"], metadata.RowIdentityFields);
		Assert.Contains(metadata.Fields, field =>
			field.FieldName == "schemaVersion"
			&& field.Role == FindGroupMutationPostComparisonKeyFieldRole.CompatibilityGate
			&& field.ProjectionRule.Contains("schemaVersion == 1", StringComparison.Ordinal));
		Assert.Contains(metadata.Fields, field =>
			field.FieldName == "activePlayerObjectId"
			&& field.Role == FindGroupMutationPostComparisonKeyFieldRole.RowIdentity
			&& field.Notes.Contains("matching player identities", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_PreservesJavaActionSpecificDirectPacketKeys()
	{
		var metadata = FindGroupMutationPostComparisonKeyProjectionMetadataService.Create();

		Assert.Contains(metadata.Fields, field =>
			field.Action == 2
			&& field.FieldName == "postedSystemMessageId"
			&& field.ProjectionRule.Contains("1400392", StringComparison.Ordinal)
			&& field.JavaSource.Contains("STR_PARTY_MATCH_OFFER_PARTY_POSTED", StringComparison.Ordinal));
		Assert.Contains(metadata.Fields, field =>
			field.Action == 2
			&& field.FieldName == "refreshedListAction"
			&& field.ProjectionRule.Contains("action 0", StringComparison.Ordinal)
			&& field.JavaSource.Contains("SM_FIND_GROUP(0", StringComparison.Ordinal));
		Assert.Contains(metadata.Fields, field =>
			field.Action == 6
			&& field.FieldName == "postedSystemMessageId"
			&& field.ProjectionRule.Contains("1400393", StringComparison.Ordinal)
			&& field.JavaSource.Contains("STR_PARTY_MATCH_SEEK_PARTY_POSTED", StringComparison.Ordinal));
		Assert.Contains(metadata.Fields, field =>
			field.Action == 6
			&& field.FieldName == "refreshedListAction"
			&& field.ProjectionRule.Contains("action 4", StringComparison.Ordinal)
			&& field.JavaSource.Contains("SM_FIND_GROUP(4", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_RequiresMutationOrderingRegistryObservationAndSideEffectGuards()
	{
		var metadata = FindGroupMutationPostComparisonKeyProjectionMetadataService.Create();

		foreach (var action in metadata.Actions)
		{
			Assert.Contains(metadata.Fields, field =>
				field.Action == action
				&& field.FieldName == "stateMutationRecordedBeforeDirectPackets"
				&& field.Role == FindGroupMutationPostComparisonKeyFieldRole.MutationState
				&& field.ProjectionRule.Contains("Require true", StringComparison.Ordinal));
			Assert.Contains(metadata.Fields, field =>
				field.Action == action
				&& field.FieldName == "registrySendsObservedInOrder"
				&& field.Role == FindGroupMutationPostComparisonKeyFieldRole.RegistryObservation
				&& field.Notes.Contains("not inferred", StringComparison.Ordinal));
			Assert.Contains(metadata.Fields, field =>
				field.Action == action
				&& field.FieldName == "worldBroadcastCount"
				&& field.Role == FindGroupMutationPostComparisonKeyFieldRole.SideEffectGuard
				&& field.ProjectionRule.Contains("0", StringComparison.Ordinal));
			Assert.Contains(metadata.Fields, field =>
				field.Action == action
				&& field.FieldName == "inviteDispatchCount"
				&& field.Role == FindGroupMutationPostComparisonKeyFieldRole.SideEffectGuard
				&& field.ProjectionRule.Contains("0", StringComparison.Ordinal));
		}
	}

	[Fact]
	public void Create_ExcludesSourceAndRawClockFromEqualityProjection()
	{
		var metadata = FindGroupMutationPostComparisonKeyProjectionMetadataService.Create();

		Assert.Equal(["traceSource", "serverEpochSeconds"], metadata.IgnoredRuntimeFields);
		Assert.DoesNotContain("traceSource", metadata.EqualityProjectionFields);
		Assert.DoesNotContain("serverEpochSeconds", metadata.EqualityProjectionFields);
		Assert.Contains(metadata.Fields, field =>
			field.FieldName == "traceSource"
			&& field.Status == FindGroupMutationPostComparisonKeyFieldStatus.IgnoredForEquality
			&& field.Notes.Contains("must differ", StringComparison.Ordinal));
		Assert.Contains(metadata.Fields, field =>
			field.FieldName == "serverEpochSeconds"
			&& field.Status == FindGroupMutationPostComparisonKeyFieldStatus.IgnoredForEquality
			&& field.Notes.Contains("Wall-clock seconds", StringComparison.Ordinal));
	}
}
