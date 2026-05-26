using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerKnownListTwoWayMembershipAdapterServiceTests
{
	[Fact]
	public void Apply_DisabledDoesNotMutateMembership()
	{
		var membership = new PlayerKnownListMembershipService();
		var adapter = new PlayerKnownListTwoWayMembershipAdapterService(membership);
		var plan = CreateAddPlan();

		var result = adapter.Apply(new PlayerKnownListTwoWayMembershipAdapterRequest(plan));

		Assert.Equal(PlayerKnownListTwoWayMembershipAdapterStatus.Disabled, result.Status);
		Assert.False(result.MutatedMembership);
		Assert.False(result.IsLive);
		Assert.Empty(membership.GetKnownPlayerObjectIds(OwnerPlayerObjectId));
		Assert.Empty(membership.GetKnownPlayerObjectIds(CandidatePlayerObjectId));
	}

	[Fact]
	public void Apply_SkipsRejectedPlansWithoutMutation()
	{
		var membership = new PlayerKnownListMembershipService();
		var adapter = new PlayerKnownListTwoWayMembershipAdapterService(membership);
		var plan = new PlayerKnownListTwoWayOperationPlanService().PlanAdd(new PlayerKnownListTwoWayOperationState(
			OwnerPlayerObjectId,
			OwnerPlayerObjectId,
			OwnerKnowsCandidate: false,
			CandidateKnowsOwner: false));

		var result = adapter.Apply(new PlayerKnownListTwoWayMembershipAdapterRequest(
			plan,
			ExecuteMembershipMutation: true));

		Assert.Equal(PlayerKnownListTwoWayMembershipAdapterStatus.SkippedRejectedPlan, result.Status);
		Assert.False(result.MutatedMembership);
		Assert.Empty(result.MembershipSnapshots);
	}

	[Fact]
	public void Apply_AddPlanMutatesCandidateFirstThenOwnerMembership()
	{
		var membership = new PlayerKnownListMembershipService();
		var adapter = new PlayerKnownListTwoWayMembershipAdapterService(membership);
		var plan = CreateAddPlan(ownerSeesCandidate: true, candidateSeesOwner: true);

		var result = adapter.Apply(new PlayerKnownListTwoWayMembershipAdapterRequest(
			plan,
			ExecuteMembershipMutation: true));

		Assert.Equal(PlayerKnownListTwoWayMembershipAdapterStatus.Applied, result.Status);
		Assert.True(result.MutatedMembership);
		Assert.False(result.ExecutedControllerSideEffects);
		Assert.Equal(2, result.AppliedMembershipStepCount);
		Assert.Equal([OwnerPlayerObjectId], membership.GetKnownPlayerObjectIds(CandidatePlayerObjectId));
		Assert.Equal([CandidatePlayerObjectId], membership.GetKnownPlayerObjectIds(OwnerPlayerObjectId));
		Assert.Equal(CandidatePlayerObjectId, result.MembershipSnapshots[0].OwnerPlayerObjectId);
		Assert.Equal(OwnerPlayerObjectId, result.MembershipSnapshots[1].OwnerPlayerObjectId);
		Assert.All(result.MembershipSnapshots.SelectMany(snapshot => snapshot.Entries), entry =>
		{
			Assert.True(entry.IsVisibleToOwner);
			Assert.Equal(PlayerKnownListMembershipUpdateReason.TwoWayOperationPlan, entry.UpdateReason);
		});
		Assert.Equal(2, result.PreservedSideEffectSteps.Count);
	}

	[Fact]
	public void Apply_RemovePlanMutatesOwnerFirstThenCandidateMembershipAndPreservesSideEffectDescriptors()
	{
		var membership = new PlayerKnownListMembershipService();
		var adapter = new PlayerKnownListTwoWayMembershipAdapterService(membership);
		adapter.Apply(new PlayerKnownListTwoWayMembershipAdapterRequest(
			CreateAddPlan(ownerSeesCandidate: true, candidateSeesOwner: true),
			ExecuteMembershipMutation: true));
		var removePlan = new PlayerKnownListTwoWayOperationPlanService().PlanRemove(new PlayerKnownListTwoWayOperationState(
			OwnerPlayerObjectId,
			CandidatePlayerObjectId,
			OwnerKnowsCandidate: true,
			CandidateKnowsOwner: true,
			OwnerSeesCandidate: true,
			CandidateSeesOwner: true));

		var result = adapter.Apply(new PlayerKnownListTwoWayMembershipAdapterRequest(
			removePlan,
			ExecuteMembershipMutation: true));

		Assert.Equal(PlayerKnownListTwoWayMembershipAdapterStatus.Applied, result.Status);
		Assert.Equal(2, result.AppliedMembershipStepCount);
		Assert.Empty(membership.GetKnownPlayerObjectIds(OwnerPlayerObjectId));
		Assert.Empty(membership.GetKnownPlayerObjectIds(CandidatePlayerObjectId));
		Assert.Equal(OwnerPlayerObjectId, result.MembershipSnapshots[0].OwnerPlayerObjectId);
		Assert.Equal(CandidatePlayerObjectId, result.MembershipSnapshots[1].OwnerPlayerObjectId);
		Assert.Equal(
			[
				PlayerKnownListTwoWayOperationStepKind.OwnerNotSeesCandidate,
				PlayerKnownListTwoWayOperationStepKind.OwnerNotKnowsCandidate,
				PlayerKnownListTwoWayOperationStepKind.CandidateNotSeesOwner,
				PlayerKnownListTwoWayOperationStepKind.CandidateNotKnowsOwner,
			],
			result.PreservedSideEffectSteps.Select(step => step.Kind));
		Assert.False(result.ExecutedControllerSideEffects);
	}

	private static PlayerKnownListTwoWayOperationPlan CreateAddPlan(
		bool ownerSeesCandidate = false,
		bool candidateSeesOwner = false) =>
		new PlayerKnownListTwoWayOperationPlanService().PlanAdd(new PlayerKnownListTwoWayOperationState(
			OwnerPlayerObjectId,
			CandidatePlayerObjectId,
			OwnerKnowsCandidate: false,
			CandidateKnowsOwner: false,
			OwnerAwareOfCandidate: true,
			CandidateAwareOfOwner: true,
			OwnerSeesCandidate: ownerSeesCandidate,
			CandidateSeesOwner: candidateSeesOwner));

	private const int OwnerPlayerObjectId = 9001;
	private const int CandidatePlayerObjectId = 9002;
}
