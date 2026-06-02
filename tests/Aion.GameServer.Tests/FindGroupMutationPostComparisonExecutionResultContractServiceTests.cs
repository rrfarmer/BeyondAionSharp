using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostComparisonExecutionResultContractServiceTests
{
	[Fact]
	public void Create_DefaultContractBlocksOnMissingTraceRowsAndIsNonLive()
	{
		var contract = FindGroupMutationPostComparisonExecutionResultContractService.Create();

		Assert.Equal(FindGroupMutationPostComparisonExecutionResultContractStatus.BlockedMissingTraceRows, contract.Status);
		Assert.False(contract.IsLive);
		Assert.True(contract.RequiresGeneratedJavaTraceRows);
		Assert.True(contract.RequiresLiveCSharpTraceRows);
		Assert.True(contract.RequiresRegistryObservation);
		Assert.True(contract.RequiresPreflightReady);
		Assert.False(contract.ReadyForComparisonExecution);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", contract.TraceName);
		Assert.Contains("addRecruitment/addApplication", contract.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_CoversActionTwoAndSixWithJavaPacketExpectations()
	{
		var contract = FindGroupMutationPostComparisonExecutionResultContractService.Create();

		Assert.Equal([2, 6], contract.Actions.Select(action => action.Action));
		Assert.Contains(contract.Actions, action =>
			action.Action == 2
			&& action.MutationKind == FindGroupDirectPacketMutationPostTraceMutationKind.Recruitment
			&& action.ExpectedPostedSystemMessageId == 1400392
			&& action.ExpectedRefreshedListAction == 0
			&& action.JavaMethod.Contains("addRecruitment", StringComparison.Ordinal));
		Assert.Contains(contract.Actions, action =>
			action.Action == 6
			&& action.MutationKind == FindGroupDirectPacketMutationPostTraceMutationKind.Application
			&& action.ExpectedPostedSystemMessageId == 1400393
			&& action.ExpectedRefreshedListAction == 4
			&& action.JavaMethod.Contains("addApplication", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_MapsProjectionRolesToDifferenceKinds()
	{
		var contract = FindGroupMutationPostComparisonExecutionResultContractService.Create();

		Assert.Equal(42, contract.Fields.Count);
		Assert.Equal(Enumerable.Range(1, contract.Fields.Count), contract.Fields.Select(field => field.Order));
		Assert.Contains(contract.Fields, field =>
			field.FieldName == "schemaVersion"
			&& field.DifferenceKind == FindGroupMutationPostComparisonDifferenceKind.CompatibilityGateMismatch);
		Assert.Contains(contract.Fields, field =>
			field.FieldName == "mutatedEntryObjectId"
			&& field.DifferenceKind == FindGroupMutationPostComparisonDifferenceKind.RowIdentityMismatch);
		Assert.Contains(contract.Fields, field =>
			field.FieldName == "postedSystemMessageId"
			&& field.DifferenceKind == FindGroupMutationPostComparisonDifferenceKind.DirectPacketMismatch);
		Assert.Contains(contract.Fields, field =>
			field.FieldName == "registrySendsObservedInOrder"
			&& field.DifferenceKind == FindGroupMutationPostComparisonDifferenceKind.RegistryObservationMismatch);
		Assert.Contains(contract.Fields, field =>
			field.FieldName == "worldBroadcastCount"
			&& field.DifferenceKind == FindGroupMutationPostComparisonDifferenceKind.SideEffectGuardMismatch);
	}

	[Fact]
	public void Create_IgnoresRuntimeOnlySourceAndClockFieldsForEquality()
	{
		var contract = FindGroupMutationPostComparisonExecutionResultContractService.Create();

		Assert.Equal(["traceSource", "serverEpochSeconds"], contract.IgnoredRuntimeFields);
		Assert.DoesNotContain("traceSource", contract.EqualityProjectionFields);
		Assert.DoesNotContain("serverEpochSeconds", contract.EqualityProjectionFields);
		Assert.Contains(contract.Fields, field =>
			field.FieldName == "traceSource"
			&& field.Status == FindGroupMutationPostComparisonDifferenceFieldStatus.IgnoredForEquality
			&& field.DifferenceKind == FindGroupMutationPostComparisonDifferenceKind.RuntimeOnlyIgnored
			&& field.DifferenceReportRule.Contains("Do not compare", StringComparison.Ordinal));
		Assert.Contains(contract.Fields, field =>
			field.FieldName == "serverEpochSeconds"
			&& field.Status == FindGroupMutationPostComparisonDifferenceFieldStatus.IgnoredForEquality
			&& field.DifferenceKind == FindGroupMutationPostComparisonDifferenceKind.RuntimeOnlyIgnored);
	}

	[Fact]
	public void Create_RequiredFieldRulesNameJavaAndCSharpValuesWithoutExecutingComparison()
	{
		var contract = FindGroupMutationPostComparisonExecutionResultContractService.Create();

		Assert.Contains(contract.Fields, field =>
			field.Action == 2
			&& field.FieldName == "postedSystemMessageId"
			&& field.Status == FindGroupMutationPostComparisonDifferenceFieldStatus.RequiredForDifferenceReport
			&& field.DifferenceReportRule.Contains("javaValue", StringComparison.Ordinal)
			&& field.DifferenceReportRule.Contains("csharpValue", StringComparison.Ordinal)
			&& field.DifferenceReportRule.Contains("Java source evidence", StringComparison.Ordinal));
		Assert.Contains(contract.Fields, field =>
			field.Action == 6
			&& field.FieldName == "visibleEntryObjectIdsAfterMutation"
			&& field.DifferenceKind == FindGroupMutationPostComparisonDifferenceKind.MutationStateMismatch
			&& field.JavaSource.Contains("applications.values().stream().filter", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_ReadinessWithoutPreflightStillBlocksPreflightReadinessSeparately()
	{
		var keyProjection = FindGroupMutationPostComparisonKeyProjectionMetadataService.Create();
		var readiness = new FindGroupMutationPostTraceRowReadinessAggregate(
			FindGroupMutationPostTraceRowReadinessStatus.BlockedMissingRegistryObservation,
			[],
			HasJavaCaptureRunbook: true,
			HasCSharpLiveTraceRowFixturePlan: true,
			HasRegistryObservationContract: true,
			HasArtifactComparisonPreflight: true,
			NeedsJavaFixture: false,
			NeedsJavaInstrumentation: false,
			NeedsGeneratedJavaArtifacts: false,
			NeedsCSharpLiveRows: false,
			NeedsRegistryObservation: true,
			NeedsComparisonExecution: true,
			ReadyForRuntimeComparison: false,
			keyProjection.TraceName,
			keyProjection.JavaSource,
			IsLive: false);

		var contract = FindGroupMutationPostComparisonExecutionResultContractService.Create(keyProjection, readiness);

		Assert.Equal(FindGroupMutationPostComparisonExecutionResultContractStatus.BlockedMissingPreflightReadiness, contract.Status);
		Assert.True(contract.RequiresGeneratedJavaTraceRows);
		Assert.True(contract.RequiresLiveCSharpTraceRows);
		Assert.True(contract.RequiresRegistryObservation);
		Assert.True(contract.RequiresPreflightReady);
	}
}
