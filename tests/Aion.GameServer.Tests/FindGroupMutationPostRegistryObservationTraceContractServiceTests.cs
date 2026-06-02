using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostRegistryObservationTraceContractServiceTests
{
	[Fact]
	public void Create_KeepsRegistryObservationContractBlockedAndNonLive()
	{
		var contract = FindGroupMutationPostRegistryObservationTraceContractService.Create();

		Assert.Equal(FindGroupMutationPostRegistryObservationTraceContractStatus.BlockedPendingLiveBoundaryTrace, contract.Status);
		Assert.False(contract.IsLive);
		Assert.False(contract.ReadyForRuntimeComparison);
		Assert.True(contract.RequiresExecutorInvokedFromBoundary);
		Assert.True(contract.RequiresRegistrySendsObservedInOrder);
		Assert.True(contract.RequiresTwoDirectSendsPerAction);
		Assert.True(contract.RequiresZeroWorldBroadcasts);
		Assert.True(contract.RequiresZeroInviteDispatches);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", contract.TraceName);
		Assert.Contains("addRecruitment/addApplication", contract.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_CoversActionTwoAndSixOnly()
	{
		var contract = FindGroupMutationPostRegistryObservationTraceContractService.Create();

		Assert.Equal([2, 6], contract.Actions);
		Assert.Equal(12, contract.Requirements.Count);
		Assert.Equal(Enumerable.Range(1, contract.Requirements.Count), contract.Requirements.Select(row => row.Order));
		Assert.All(contract.Requirements, row => Assert.Contains(row.Action, new[] { 2, 6 }));
		Assert.Contains(contract.Requirements, row =>
			row.Action == 2
			&& row.MutationKind == FindGroupDirectPacketMutationPostTraceMutationKind.Recruitment);
		Assert.Contains(contract.Requirements, row =>
			row.Action == 6
			&& row.MutationKind == FindGroupDirectPacketMutationPostTraceMutationKind.Application);
	}

	[Fact]
	public void Create_RequiresPostedMessageThenRefreshedListRegistryObservationPerAction()
	{
		var contract = FindGroupMutationPostRegistryObservationTraceContractService.Create();

		Assert.Contains(contract.Requirements, row =>
			row.Action == 2
			&& row.Kind == FindGroupMutationPostRegistryObservationRequirementKind.PostedSystemMessageSend
			&& row.Status == FindGroupMutationPostRegistryObservationRequirementStatus.BlockedPendingLiveBoundary
			&& row.RequiredObservation.Contains("send #1", StringComparison.Ordinal)
			&& row.RequiredObservation.Contains("SmSystemMessage id 1400392", StringComparison.Ordinal)
			&& row.TraceFields.Contains("postedSystemMessageRecipientObjectId=activePlayerObjectId", StringComparison.Ordinal));
		Assert.Contains(contract.Requirements, row =>
			row.Action == 2
			&& row.Kind == FindGroupMutationPostRegistryObservationRequirementKind.RefreshedShowListSend
			&& row.RequiredObservation.Contains("send #2", StringComparison.Ordinal)
			&& row.RequiredObservation.Contains("SmFindGroup action 0", StringComparison.Ordinal));
		Assert.Contains(contract.Requirements, row =>
			row.Action == 6
			&& row.Kind == FindGroupMutationPostRegistryObservationRequirementKind.PostedSystemMessageSend
			&& row.RequiredObservation.Contains("SmSystemMessage id 1400393", StringComparison.Ordinal));
		Assert.Contains(contract.Requirements, row =>
			row.Action == 6
			&& row.Kind == FindGroupMutationPostRegistryObservationRequirementKind.RefreshedShowListSend
			&& row.RequiredObservation.Contains("SmFindGroup action 4", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_RequiresExecutorRegistryOrderingAndNoUnexpectedSideEffects()
	{
		var contract = FindGroupMutationPostRegistryObservationTraceContractService.Create();

		foreach (var action in contract.Actions)
		{
			Assert.Contains(contract.Requirements, row =>
				row.Action == action
				&& row.Kind == FindGroupMutationPostRegistryObservationRequirementKind.BoundaryExecutorInvocation
				&& row.TraceFields.Contains("executorInvokedFromBoundary=true", StringComparison.Ordinal)
				&& row.Notes.Contains("Disabled opt-in executor evidence is not sufficient", StringComparison.Ordinal));
			Assert.Contains(contract.Requirements, row =>
				row.Action == action
				&& row.Kind == FindGroupMutationPostRegistryObservationRequirementKind.RegistrySendOrdering
				&& row.TraceFields.Contains("registrySendsObservedInOrder=true", StringComparison.Ordinal)
				&& row.Notes.Contains("not inferred only from intent list order", StringComparison.Ordinal));
			Assert.Contains(contract.Requirements, row =>
				row.Action == action
				&& row.Kind == FindGroupMutationPostRegistryObservationRequirementKind.NoUnexpectedSideEffects
				&& row.TraceFields.Contains("worldBroadcastCount=0", StringComparison.Ordinal)
				&& row.TraceFields.Contains("inviteDispatchCount=0", StringComparison.Ordinal));
		}
	}

	[Fact]
	public void Create_RuntimeTraceFieldsAreOnlyNonLiveSchemaMetadata()
	{
		var contract = FindGroupMutationPostRegistryObservationTraceContractService.Create();

		Assert.Contains(contract.Requirements, row =>
			row.Action == 2
			&& row.Kind == FindGroupMutationPostRegistryObservationRequirementKind.RuntimeTraceFields
			&& row.Status == FindGroupMutationPostRegistryObservationRequirementStatus.NonLiveSchemaAvailable
			&& row.TraceFields.Contains("traceSource=CSharp", StringComparison.Ordinal)
			&& row.TraceFields.Contains("stateMutationRecordedBeforeDirectPackets", StringComparison.Ordinal)
			&& row.Notes.Contains("runtime values still require live capture", StringComparison.Ordinal));
		Assert.Contains(contract.Requirements, row =>
			row.Action == 6
			&& row.Kind == FindGroupMutationPostRegistryObservationRequirementKind.RuntimeTraceFields
			&& row.Status == FindGroupMutationPostRegistryObservationRequirementStatus.NonLiveSchemaAvailable);
	}
}
