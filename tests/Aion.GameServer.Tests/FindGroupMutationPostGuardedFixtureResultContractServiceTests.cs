using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostGuardedFixtureResultContractServiceTests
{
	[Fact]
	public void Create_DefaultContractIsNonLiveAndDoesNotSendPacketsByDefault()
	{
		var contract = FindGroupMutationPostGuardedFixtureResultContractService.Create();

		Assert.Equal(FindGroupMutationPostGuardedFixtureResultContractStatus.BlockedMissingGuardedFixtureRows, contract.Status);
		Assert.False(contract.IsLive);
		Assert.False(contract.IsProductionCmFindGroupDispatchEnabled);
		Assert.False(contract.ShouldSendPacketsByDefault);
		Assert.True(contract.RequiresExplicitTraceGuard);
		Assert.Equal("AION_FIND_GROUP_MUTATION_POST_TRACE_GUARD", contract.TraceGuardName);
		Assert.Equal("GameServerConnectionFindGroupMutationPostGuardedLiveBoundaryFixture", contract.FixtureClassName);
		Assert.False(contract.ReadyForComparisonHandoff);
	}

	[Fact]
	public void Create_DefaultRequirementsBlockOnMissingActionTwoAndSixRows()
	{
		var contract = FindGroupMutationPostGuardedFixtureResultContractService.Create();

		Assert.Equal(8, contract.Requirements.Count);
		Assert.Equal(Enumerable.Range(1, contract.Requirements.Count), contract.Requirements.Select(requirement => requirement.Order));
		Assert.Contains(contract.Requirements, requirement =>
			requirement.Kind == FindGroupMutationPostGuardedFixtureResultRequirementKind.ExplicitTraceGuard
			&& requirement.Status == FindGroupMutationPostGuardedFixtureResultRequirementStatus.SatisfiedByContract
			&& !requirement.BlocksComparisonHandoff);
		Assert.Contains(contract.Requirements, requirement =>
			requirement.Kind == FindGroupMutationPostGuardedFixtureResultRequirementKind.ProductionDispatchGuard
			&& requirement.Evidence.Contains("productionDispatch=False", StringComparison.Ordinal)
			&& requirement.Evidence.Contains("sendsPacketsByDefault=False", StringComparison.Ordinal));
		Assert.Contains(contract.Requirements, requirement =>
			requirement.Kind == FindGroupMutationPostGuardedFixtureResultRequirementKind.ActionTwoLiveBoundaryRow
			&& requirement.Action == 2
			&& requirement.Status == FindGroupMutationPostGuardedFixtureResultRequirementStatus.BlockedMissingLiveBoundaryRow);
		Assert.Contains(contract.Requirements, requirement =>
			requirement.Kind == FindGroupMutationPostGuardedFixtureResultRequirementKind.ActionSixLiveBoundaryRow
			&& requirement.Action == 6
			&& requirement.Status == FindGroupMutationPostGuardedFixtureResultRequirementStatus.BlockedMissingLiveBoundaryRow);
	}

	[Fact]
	public void Create_RejectsDisabledShapeRowsWithoutMarkingThemLive()
	{
		var contract = FindGroupMutationPostGuardedFixtureResultContractService.Create(
			candidateRows:
			[
				FindGroupDirectPacketMutationPostBoundaryTraceSchemaService.CreateSampleExport(2),
				FindGroupDirectPacketMutationPostBoundaryTraceSchemaService.CreateSampleExport(6),
			]);

		Assert.Equal(FindGroupMutationPostGuardedFixtureResultContractStatus.BlockedMissingGuardedFixtureRows, contract.Status);
		Assert.Equal(2, contract.CandidateRows.Count);
		Assert.Empty(contract.AcceptedLiveRows);
		Assert.All(contract.CandidateRows, row =>
		{
			Assert.Equal(FindGroupMutationPostGuardedFixtureCandidateRowStatus.RejectedMissingBoundaryAcceptance, row.Status);
			Assert.True(row.IsShapeValid);
			Assert.False(row.IsLiveBoundaryEvidence);
			Assert.False(row.BoundaryAccepted);
		});
	}

	[Fact]
	public void Create_AcceptsActionTwoAndSixLiveRowsForComparisonHandoff()
	{
		var contract = FindGroupMutationPostGuardedFixtureResultContractService.Create(
			candidateRows:
			[
				LiveRow(2),
				LiveRow(6),
			]);

		Assert.Equal(FindGroupMutationPostGuardedFixtureResultContractStatus.ReadyForComparisonHandoff, contract.Status);
		Assert.True(contract.HasActionTwoLiveRow);
		Assert.True(contract.HasActionSixLiveRow);
		Assert.True(contract.ReadyForComparisonHandoff);
		Assert.Equal(2, contract.AcceptedLiveRows.Count);
		Assert.All(contract.AcceptedLiveRows, row =>
		{
			Assert.Equal(FindGroupMutationPostGuardedFixtureCandidateRowStatus.AcceptedLiveBoundaryRow, row.Status);
			Assert.True(row.IsLiveBoundaryEvidence);
			Assert.True(row.ExecutorInvokedFromBoundary);
			Assert.True(row.RegistrySendsObservedInOrder);
		});
		Assert.Contains(contract.Requirements, requirement =>
			requirement.Kind == FindGroupMutationPostGuardedFixtureResultRequirementKind.ComparisonHandoff
			&& requirement.Status == FindGroupMutationPostGuardedFixtureResultRequirementStatus.SatisfiedByLiveBoundaryRow
			&& !requirement.BlocksComparisonHandoff);
	}

	[Fact]
	public void Create_RejectsRowsWithUnexpectedSideEffectsOrPacketShape()
	{
		var badShape = LiveRow(2) with { PostedSystemMessageId = 1400393 };
		var sideEffect = LiveRow(6) with { WorldBroadcastCount = 1 };

		var contract = FindGroupMutationPostGuardedFixtureResultContractService.Create(
			candidateRows:
			[
				badShape,
				sideEffect,
			]);

		Assert.Equal(FindGroupMutationPostGuardedFixtureResultContractStatus.BlockedMissingGuardedFixtureRows, contract.Status);
		Assert.Contains(contract.CandidateRows, row =>
			row.Action == 2
			&& row.Status == FindGroupMutationPostGuardedFixtureCandidateRowStatus.RejectedUnexpectedPacketShape
			&& !row.IsShapeValid);
		Assert.Contains(contract.CandidateRows, row =>
			row.Action == 6
			&& row.Status == FindGroupMutationPostGuardedFixtureCandidateRowStatus.RejectedUnexpectedSideEffects
			&& row.IsShapeValid
			&& !row.IsLiveBoundaryEvidence);
		Assert.Empty(contract.AcceptedLiveRows);
	}

	private static FindGroupDirectPacketMutationPostBoundaryTraceExport LiveRow(int action) =>
		FindGroupDirectPacketMutationPostBoundaryTraceSchemaService.CreateSampleExport(action) with
		{
			BoundaryAccepted = true,
			ActivePlayerObjectId = action == 2 ? 1001 : 1002,
			ActivePlayerRace = "ELYOS",
			ServerEpochSeconds = 123,
			MutatedEntryObjectId = action == 2 ? 2001 : 2002,
			StateMutationRecordedBeforeDirectPackets = true,
			PostedSystemMessageRecipientObjectId = action == 2 ? 1001 : 1002,
			RefreshedListRecipientObjectId = action == 2 ? 1001 : 1002,
			VisibleEntryObjectIdsAfterMutation = [action == 2 ? 2001 : 2002],
			ExecutorInvokedFromBoundary = true,
			RegistrySendsObservedInOrder = true,
		};
}
