using Aion.GameServer.Model;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerKnownListOperationSideEffectAttachmentServiceTests
{
	[Fact]
	public void Attach_AddPlanMapsCandidateAndOwnerSeeStepsToCorrectViewerDirections()
	{
		var operationPlanner = new PlayerKnownListTwoWayOperationPlanService();
		var service = new PlayerKnownListOperationSideEffectAttachmentService();
		var operationPlan = operationPlanner.PlanAdd(new PlayerKnownListTwoWayOperationState(
			OwnerPlayerObjectId,
			CandidatePlayerObjectId,
			OwnerKnowsCandidate: false,
			CandidateKnowsOwner: false,
			OwnerSeesCandidate: true,
			CandidateSeesOwner: true));

		var plan = service.Attach(new PlayerKnownListOperationSideEffectAttachmentRequest(
			operationPlan,
			OwnerViewingCandidate: new PlayerKnownListOperationSideEffectDirectionFacts(
				ViewerAggroIconToSubject: true,
				SubjectIsInRideMode: true,
				SubjectRideNpcId: RideNpcId),
			CandidateViewingOwner: new PlayerKnownListOperationSideEffectDirectionFacts(
				SubjectIsUnderStance: true)));

		Assert.Equal(PlayerKnownListOperationSideEffectAttachmentStatus.Attached, plan.Status);
		Assert.False(plan.ExecutesLivePackets);
		Assert.False(plan.IsLive);
		Assert.False(plan.IsJavaControllerParity);
		Assert.Equal(
			[PlayerKnownListTwoWayOperationStepKind.CandidateSeesOwner, PlayerKnownListTwoWayOperationStepKind.OwnerSeesCandidate],
			plan.AttachedSideEffects.Select(attachment => attachment.OperationStep.Kind));

		var candidateSeesOwner = plan.AttachedSideEffects[0].SideEffectPlan;
		Assert.Equal(CandidatePlayerObjectId, candidateSeesOwner.ViewerPlayerObjectId);
		Assert.Equal(OwnerPlayerObjectId, candidateSeesOwner.SubjectPlayerObjectId);
		Assert.Equal(
			[PlayerKnownListPlayerSideEffectKind.SmPlayerInfo, PlayerKnownListPlayerSideEffectKind.SmMotion, PlayerKnownListPlayerSideEffectKind.SmPlayerStance],
			candidateSeesOwner.Descriptors.Select(descriptor => descriptor.Kind));

		var ownerSeesCandidate = plan.AttachedSideEffects[1].SideEffectPlan;
		Assert.Equal(OwnerPlayerObjectId, ownerSeesCandidate.ViewerPlayerObjectId);
		Assert.Equal(CandidatePlayerObjectId, ownerSeesCandidate.SubjectPlayerObjectId);
		Assert.True(ownerSeesCandidate.Descriptors[0].AggroIcon);
		Assert.Equal(RideNpcId, ownerSeesCandidate.Descriptors[2].RideNpcId);
	}

	[Fact]
	public void Attach_RemovePlanMapsNotSeeStepsToDeleteDescriptorsWithDirectionAnimations()
	{
		var operationPlanner = new PlayerKnownListTwoWayOperationPlanService();
		var service = new PlayerKnownListOperationSideEffectAttachmentService();
		var operationPlan = operationPlanner.PlanRemove(new PlayerKnownListTwoWayOperationState(
			OwnerPlayerObjectId,
			CandidatePlayerObjectId,
			OwnerKnowsCandidate: true,
			CandidateKnowsOwner: true,
			OwnerSeesCandidate: true,
			CandidateSeesOwner: true));

		var plan = service.Attach(new PlayerKnownListOperationSideEffectAttachmentRequest(
			operationPlan,
			OwnerViewingCandidate: new PlayerKnownListOperationSideEffectDirectionFacts(
				NotSeeAnimation: ObjectDeleteAnimation.None),
			CandidateViewingOwner: new PlayerKnownListOperationSideEffectDirectionFacts(
				NotSeeAnimation: ObjectDeleteAnimation.JumpIn)));

		Assert.Equal(PlayerKnownListOperationSideEffectAttachmentStatus.Attached, plan.Status);
		Assert.Equal(
			[PlayerKnownListTwoWayOperationStepKind.OwnerNotSeesCandidate, PlayerKnownListTwoWayOperationStepKind.CandidateNotSeesOwner],
			plan.AttachedSideEffects.Select(attachment => attachment.OperationStep.Kind));

		var ownerDelete = Assert.Single(plan.AttachedSideEffects[0].SideEffectPlan.Descriptors);
		Assert.Equal(OwnerPlayerObjectId, ownerDelete.ViewerPlayerObjectId);
		Assert.Equal(CandidatePlayerObjectId, ownerDelete.SubjectPlayerObjectId);
		Assert.Equal(ObjectDeleteAnimation.None, ownerDelete.DeleteAnimation);

		var candidateDelete = Assert.Single(plan.AttachedSideEffects[1].SideEffectPlan.Descriptors);
		Assert.Equal(CandidatePlayerObjectId, candidateDelete.ViewerPlayerObjectId);
		Assert.Equal(OwnerPlayerObjectId, candidateDelete.SubjectPlayerObjectId);
		Assert.Equal(ObjectDeleteAnimation.JumpIn, candidateDelete.DeleteAnimation);
	}

	[Fact]
	public void Attach_RemovePlanKeepsUnspawnedViewerNotSeeAsSkippedSideEffect()
	{
		var operationPlanner = new PlayerKnownListTwoWayOperationPlanService();
		var service = new PlayerKnownListOperationSideEffectAttachmentService();
		var operationPlan = operationPlanner.PlanRemove(new PlayerKnownListTwoWayOperationState(
			OwnerPlayerObjectId,
			CandidatePlayerObjectId,
			OwnerKnowsCandidate: true,
			CandidateKnowsOwner: false,
			OwnerSeesCandidate: true));

		var plan = service.Attach(new PlayerKnownListOperationSideEffectAttachmentRequest(
			operationPlan,
			OwnerViewingCandidate: new PlayerKnownListOperationSideEffectDirectionFacts(ViewerIsSpawned: false),
			CandidateViewingOwner: new PlayerKnownListOperationSideEffectDirectionFacts()));

		var attachment = Assert.Single(plan.AttachedSideEffects);
		Assert.Equal(PlayerKnownListPlayerSideEffectStatus.SkippedViewerNotSpawned, attachment.SideEffectPlan.Status);
		Assert.Empty(attachment.SideEffectPlan.Descriptors);
	}

	[Fact]
	public void Attach_RejectedPlanDoesNotAttachDescriptors()
	{
		var operationPlanner = new PlayerKnownListTwoWayOperationPlanService();
		var service = new PlayerKnownListOperationSideEffectAttachmentService();
		var rejected = operationPlanner.PlanAdd(new PlayerKnownListTwoWayOperationState(
			OwnerPlayerObjectId,
			OwnerPlayerObjectId,
			OwnerKnowsCandidate: false,
			CandidateKnowsOwner: false));

		var plan = service.Attach(new PlayerKnownListOperationSideEffectAttachmentRequest(
			rejected,
			new PlayerKnownListOperationSideEffectDirectionFacts(),
			new PlayerKnownListOperationSideEffectDirectionFacts()));

		Assert.Equal(PlayerKnownListOperationSideEffectAttachmentStatus.SkippedRejectedPlan, plan.Status);
		Assert.Empty(plan.AttachedSideEffects);
	}

	[Fact]
	public void Attach_PlanWithoutSeeOrNotSeeStepsReportsNoSideEffectSteps()
	{
		var operationPlanner = new PlayerKnownListTwoWayOperationPlanService();
		var service = new PlayerKnownListOperationSideEffectAttachmentService();
		var operationPlan = operationPlanner.PlanAdd(new PlayerKnownListTwoWayOperationState(
			OwnerPlayerObjectId,
			CandidatePlayerObjectId,
			OwnerKnowsCandidate: false,
			CandidateKnowsOwner: false,
			OwnerSeesCandidate: false,
			CandidateSeesOwner: false));

		var plan = service.Attach(new PlayerKnownListOperationSideEffectAttachmentRequest(
			operationPlan,
			new PlayerKnownListOperationSideEffectDirectionFacts(),
			new PlayerKnownListOperationSideEffectDirectionFacts()));

		Assert.Equal(PlayerKnownListOperationSideEffectAttachmentStatus.NoSideEffectSteps, plan.Status);
		Assert.Empty(plan.AttachedSideEffects);
	}

	private const int OwnerPlayerObjectId = 9001;
	private const int CandidatePlayerObjectId = 9002;
	private const int RideNpcId = 730001;
}
