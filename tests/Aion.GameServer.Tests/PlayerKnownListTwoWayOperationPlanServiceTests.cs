using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerKnownListTwoWayOperationPlanServiceTests
{
	[Fact]
	public void PlanAdd_SchedulesCandidateKnownListAddBeforeOwnerAdd()
	{
		var service = new PlayerKnownListTwoWayOperationPlanService();

		var plan = service.PlanAdd(CreateState(ownerKnowsCandidate: false, candidateKnowsOwner: false));

		Assert.Equal(PlayerKnownListTwoWayOperationKind.Add, plan.Kind);
		Assert.Equal(PlayerKnownListTwoWayOperationStatus.Planned, plan.Status);
		Assert.False(plan.IsLive);
		Assert.False(plan.MutatesLiveMembership);
		Assert.False(plan.IsJavaRegionKnownListParity);
		Assert.True(plan.RequiresCandidateAddBeforeOwnerAdd);
		Assert.Equal(
			[PlayerKnownListTwoWayOperationStepKind.CandidateAddsOwner, PlayerKnownListTwoWayOperationStepKind.OwnerAddsCandidate],
			plan.Steps.Select(step => step.Kind));
	}

	[Fact]
	public void PlanAdd_DefaultsNewKnownObjectVisibilityToFalseAndDoesNotPlanSeeSideEffects()
	{
		var service = new PlayerKnownListTwoWayOperationPlanService();

		var plan = service.PlanAdd(CreateState(ownerKnowsCandidate: false, candidateKnowsOwner: false));

		Assert.DoesNotContain(plan.Steps, step => step.Kind is PlayerKnownListTwoWayOperationStepKind.OwnerSeesCandidate or PlayerKnownListTwoWayOperationStepKind.CandidateSeesOwner);
	}

	[Fact]
	public void PlanAdd_WhenVisibleTruePlansSeeSideEffectsAfterEachMembershipAdd()
	{
		var service = new PlayerKnownListTwoWayOperationPlanService();

		var plan = service.PlanAdd(new PlayerKnownListTwoWayOperationState(
			OwnerPlayerObjectId,
			CandidatePlayerObjectId,
			OwnerKnowsCandidate: false,
			CandidateKnowsOwner: false,
			OwnerAwareOfCandidate: true,
			CandidateAwareOfOwner: true,
			OwnerSeesCandidate: true,
			CandidateSeesOwner: true));

		Assert.Equal(
			[
				PlayerKnownListTwoWayOperationStepKind.CandidateAddsOwner,
				PlayerKnownListTwoWayOperationStepKind.CandidateSeesOwner,
				PlayerKnownListTwoWayOperationStepKind.OwnerAddsCandidate,
				PlayerKnownListTwoWayOperationStepKind.OwnerSeesCandidate,
			],
			plan.Steps.Select(step => step.Kind));
	}

	[Fact]
	public void PlanAdd_RejectsSelfAndStopsBeforeCandidateAdd()
	{
		var service = new PlayerKnownListTwoWayOperationPlanService();

		var plan = service.PlanAdd(new PlayerKnownListTwoWayOperationState(
			OwnerPlayerObjectId,
			OwnerPlayerObjectId,
			OwnerKnowsCandidate: false,
			CandidateKnowsOwner: false));

		Assert.Equal(PlayerKnownListTwoWayOperationStatus.RejectedSelf, plan.Status);
		Assert.Empty(plan.Steps);
	}

	[Theory]
	[InlineData(true, false, true, true, PlayerKnownListTwoWayOperationStatus.OwnerAlreadyKnowsCandidate)]
	[InlineData(false, true, true, true, PlayerKnownListTwoWayOperationStatus.CandidateAlreadyKnowsOwner)]
	[InlineData(false, false, false, true, PlayerKnownListTwoWayOperationStatus.OwnerAwarenessRejected)]
	[InlineData(false, false, true, false, PlayerKnownListTwoWayOperationStatus.CandidateAwarenessRejected)]
	public void PlanAdd_StopsWhenJavaAddPreconditionsWouldFail(
		bool ownerKnowsCandidate,
		bool candidateKnowsOwner,
		bool ownerAwareOfCandidate,
		bool candidateAwareOfOwner,
		PlayerKnownListTwoWayOperationStatus expectedStatus)
	{
		var service = new PlayerKnownListTwoWayOperationPlanService();

		var plan = service.PlanAdd(new PlayerKnownListTwoWayOperationState(
			OwnerPlayerObjectId,
			CandidatePlayerObjectId,
			ownerKnowsCandidate,
			candidateKnowsOwner,
			ownerAwareOfCandidate,
			candidateAwareOfOwner));

		Assert.Equal(expectedStatus, plan.Status);
		Assert.Empty(plan.Steps);
	}

	[Fact]
	public void PlanRemove_SchedulesOwnerRemovalBeforeCandidateRemoval()
	{
		var service = new PlayerKnownListTwoWayOperationPlanService();

		var plan = service.PlanRemove(CreateState(ownerKnowsCandidate: true, candidateKnowsOwner: true));

		Assert.Equal(PlayerKnownListTwoWayOperationKind.Remove, plan.Kind);
		Assert.Equal(PlayerKnownListTwoWayOperationStatus.Planned, plan.Status);
		Assert.True(plan.RequiresOwnerRemoveBeforeCandidateRemove);
		Assert.Equal(
			[
				PlayerKnownListTwoWayOperationStepKind.OwnerRemovesCandidate,
				PlayerKnownListTwoWayOperationStepKind.OwnerNotKnowsCandidate,
				PlayerKnownListTwoWayOperationStepKind.CandidateRemovesOwner,
				PlayerKnownListTwoWayOperationStepKind.CandidateNotKnowsOwner,
			],
			plan.Steps.Select(step => step.Kind));
	}

	[Fact]
	public void PlanRemove_WhenVisiblePlansRemoveThenNotSeeThenNotKnowPerSide()
	{
		var service = new PlayerKnownListTwoWayOperationPlanService();

		var plan = service.PlanRemove(new PlayerKnownListTwoWayOperationState(
			OwnerPlayerObjectId,
			CandidatePlayerObjectId,
			OwnerKnowsCandidate: true,
			CandidateKnowsOwner: true,
			OwnerSeesCandidate: true,
			CandidateSeesOwner: true));

		Assert.Equal(
			[
				PlayerKnownListTwoWayOperationStepKind.OwnerRemovesCandidate,
				PlayerKnownListTwoWayOperationStepKind.OwnerNotSeesCandidate,
				PlayerKnownListTwoWayOperationStepKind.OwnerNotKnowsCandidate,
				PlayerKnownListTwoWayOperationStepKind.CandidateRemovesOwner,
				PlayerKnownListTwoWayOperationStepKind.CandidateNotSeesOwner,
				PlayerKnownListTwoWayOperationStepKind.CandidateNotKnowsOwner,
			],
			plan.Steps.Select(step => step.Kind));
	}

	[Fact]
	public void PlanClearPair_UsesClearSpecificJavaSourceAndOwnerFirstOrdering()
	{
		var service = new PlayerKnownListTwoWayOperationPlanService();

		var plan = service.PlanClearPair(CreateState(ownerKnowsCandidate: true, candidateKnowsOwner: true));

		Assert.Equal(PlayerKnownListTwoWayOperationKind.Clear, plan.Kind);
		Assert.Equal(PlayerKnownListTwoWayOperationStatus.Planned, plan.Status);
		Assert.Equal(
			[
				PlayerKnownListTwoWayOperationStepKind.OwnerRemovesCandidate,
				PlayerKnownListTwoWayOperationStepKind.OwnerNotKnowsCandidate,
				PlayerKnownListTwoWayOperationStepKind.CandidateRemovesOwner,
				PlayerKnownListTwoWayOperationStepKind.CandidateNotKnowsOwner,
			],
			plan.Steps.Select(step => step.Kind));
		Assert.Contains("KnownList.clear", plan.Steps[0].JavaSource);
		Assert.Contains("KnownList.clear", plan.Steps[1].JavaSource);
	}

	[Fact]
	public void PlanRemove_ReturnsNothingToRemoveWhenNeitherSideKnowsTheOther()
	{
		var service = new PlayerKnownListTwoWayOperationPlanService();

		var plan = service.PlanRemove(CreateState(ownerKnowsCandidate: false, candidateKnowsOwner: false));

		Assert.Equal(PlayerKnownListTwoWayOperationStatus.NothingToRemove, plan.Status);
		Assert.Empty(plan.Steps);
	}

	private static PlayerKnownListTwoWayOperationState CreateState(
		bool ownerKnowsCandidate,
		bool candidateKnowsOwner) =>
		new(
			OwnerPlayerObjectId,
			CandidatePlayerObjectId,
			ownerKnowsCandidate,
			candidateKnowsOwner);

	private const int OwnerPlayerObjectId = 9001;
	private const int CandidatePlayerObjectId = 9002;
}
