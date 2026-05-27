using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerKnownListVisibilityRangePlanServiceTests
{
	[Fact]
	public void Plan_UsesStrictJavaRangeComparisonAtBoundary()
	{
		var service = new PlayerKnownListVisibilityRangePlanService();

		var plan = service.Plan(
			CreateObject(OwnerPlayerObjectId, x: 0, visibleDistance: 95),
			CreateObject(CandidatePlayerObjectId, x: 95, visibleDistance: 95));

		Assert.True(plan.UsesStrictLessThanRange);
		Assert.False(plan.IsInJavaRange);
		Assert.Equal(PlayerKnownListTwoWayOperationKind.Remove, plan.OperationPlan.Kind);
		Assert.Equal(PlayerKnownListTwoWayOperationStatus.NothingToRemove, plan.OperationPlan.Status);
	}

	[Fact]
	public void Plan_UsesMaximumVisibleDistanceAcrossBothKnownLists()
	{
		var service = new PlayerKnownListVisibilityRangePlanService();

		var plan = service.Plan(
			CreateObject(OwnerPlayerObjectId, x: 0, visibleDistance: 95),
			CreateObject(CandidatePlayerObjectId, x: 119, visibleDistance: 120));

		Assert.True(plan.UsesMaxVisibleDistanceRule);
		Assert.Equal(120, plan.DetectionDistance);
		Assert.True(plan.IsInJavaRange);
		Assert.Equal(PlayerKnownListTwoWayOperationKind.Add, plan.OperationPlan.Kind);
		Assert.Equal(PlayerKnownListTwoWayOperationStatus.Planned, plan.OperationPlan.Status);
	}

	[Fact]
	public void Plan_DifferentInstanceIsOutOfRangeEvenWhenCoordinatesOverlap()
	{
		var service = new PlayerKnownListVisibilityRangePlanService();

		var plan = service.Plan(
			CreateObject(OwnerPlayerObjectId, x: 0, instanceId: 1),
			CreateObject(CandidatePlayerObjectId, x: 0, instanceId: 2));

		Assert.False(plan.SameWorldAndInstance);
		Assert.False(plan.IsInJavaRange);
		Assert.Equal(PlayerKnownListTwoWayOperationKind.Remove, plan.OperationPlan.Kind);
	}

	[Fact]
	public void Plan_InRangeUsesCallerSuppliedCanSeeResultsForSeeDescriptors()
	{
		var service = new PlayerKnownListVisibilityRangePlanService();

		var plan = service.Plan(
			CreateObject(OwnerPlayerObjectId, x: 0, canSeeOther: true),
			CreateObject(CandidatePlayerObjectId, x: 10, canSeeOther: false));

		Assert.True(plan.IsInJavaRange);
		Assert.True(plan.OwnerCanSeeCandidate);
		Assert.False(plan.CandidateCanSeeOwner);
		Assert.Equal(
			[PlayerKnownListTwoWayOperationStepKind.CandidateAddsOwner, PlayerKnownListTwoWayOperationStepKind.OwnerAddsCandidate, PlayerKnownListTwoWayOperationStepKind.OwnerSeesCandidate],
			plan.OperationPlan.Steps.Select(step => step.Kind));
	}

	[Fact]
	public void Plan_OutOfRangeExistingVisibleMembershipPlansRemoveSideEffects()
	{
		var service = new PlayerKnownListVisibilityRangePlanService();

		var plan = service.Plan(
			CreateObject(OwnerPlayerObjectId, x: 0, knowsOther: true, canSeeOther: true),
			CreateObject(CandidatePlayerObjectId, x: 200, knowsOther: true, canSeeOther: true));

		Assert.False(plan.IsInJavaRange);
		Assert.Equal(PlayerKnownListTwoWayOperationKind.Remove, plan.OperationPlan.Kind);
		Assert.Equal(
			[
				PlayerKnownListTwoWayOperationStepKind.OwnerRemovesCandidate,
				PlayerKnownListTwoWayOperationStepKind.OwnerNotSeesCandidate,
				PlayerKnownListTwoWayOperationStepKind.OwnerNotKnowsCandidate,
				PlayerKnownListTwoWayOperationStepKind.CandidateRemovesOwner,
				PlayerKnownListTwoWayOperationStepKind.CandidateNotSeesOwner,
				PlayerKnownListTwoWayOperationStepKind.CandidateNotKnowsOwner,
			],
			plan.OperationPlan.Steps.Select(step => step.Kind));
	}

	[Fact]
	public void Plan_OutOfRangeExistingInvisibleMembershipSkipsNotSeeSideEffects()
	{
		var service = new PlayerKnownListVisibilityRangePlanService();

		var plan = service.Plan(
			CreateObject(OwnerPlayerObjectId, x: 0, knowsOther: true, canSeeOther: false),
			CreateObject(CandidatePlayerObjectId, x: 200, knowsOther: true, canSeeOther: false));

		Assert.False(plan.IsInJavaRange);
		Assert.Equal(PlayerKnownListTwoWayOperationKind.Remove, plan.OperationPlan.Kind);
		Assert.Equal(
			[
				PlayerKnownListTwoWayOperationStepKind.OwnerRemovesCandidate,
				PlayerKnownListTwoWayOperationStepKind.OwnerNotKnowsCandidate,
				PlayerKnownListTwoWayOperationStepKind.CandidateRemovesOwner,
				PlayerKnownListTwoWayOperationStepKind.CandidateNotKnowsOwner,
			],
			plan.OperationPlan.Steps.Select(step => step.Kind));
		Assert.DoesNotContain(plan.OperationPlan.Steps, step => step.Kind is
			PlayerKnownListTwoWayOperationStepKind.OwnerNotSeesCandidate or
			PlayerKnownListTwoWayOperationStepKind.CandidateNotSeesOwner);
	}

	private static PlayerKnownListVisibilityRangeObject CreateObject(
		int playerObjectId,
		float x,
		float visibleDistance = WorldVisibility.DefaultVisibleDistance,
		int worldId = 210010000,
		int instanceId = 1,
		bool isAwareOfOther = true,
		bool canSeeOther = true,
		bool knowsOther = false) =>
		new(
			playerObjectId,
			worldId,
			instanceId,
			x,
			Y: 0,
			Z: 0,
			visibleDistance,
			isAwareOfOther,
			canSeeOther,
			knowsOther);

	private const int OwnerPlayerObjectId = 9001;
	private const int CandidatePlayerObjectId = 9002;
}
