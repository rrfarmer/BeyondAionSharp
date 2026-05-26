using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class PlayerKnownListOperationSideEffectPacketConstructionServiceTests
{
	[Fact]
	public void Construct_AddPlanBuildsDirectionalPacketConstructionPlansInOperationOrder()
	{
		var attachmentPlan = CreateAddAttachmentPlan();
		var service = new PlayerKnownListOperationSideEffectPacketConstructionService();

		var plan = service.Construct(new PlayerKnownListOperationSideEffectPacketConstructionRequest(
			attachmentPlan,
			new Dictionary<int, PlayerKnownListOperationSideEffectPacketConstructionFacts>
			{
				[OwnerPlayerObjectId] = CreateFacts(OwnerPlayerObjectId, "Owner", stance: true),
				[CandidatePlayerObjectId] = CreateFacts(CandidatePlayerObjectId, "Candidate"),
			}));

		Assert.Equal(PlayerKnownListOperationSideEffectPacketConstructionStatus.Constructed, plan.Status);
		Assert.False(plan.ExecutesLivePackets);
		Assert.False(plan.IsLive);
		Assert.False(plan.IsJavaControllerParity);
		Assert.Equal(
			[PlayerKnownListTwoWayOperationStepKind.CandidateSeesOwner, PlayerKnownListTwoWayOperationStepKind.OwnerSeesCandidate],
			plan.Results.Select(result => result.AttachedSideEffect.OperationStep.Kind));

		var candidateSeesOwner = plan.Results[0].PacketConstructionPlan!;
		Assert.Equal(PlayerKnownListPlayerSideEffectPacketConstructionStatus.Constructed, candidateSeesOwner.Status);
		Assert.Equal(
			[typeof(SmPlayerInfo), typeof(SmMotion), typeof(SmPlayerStance)],
			candidateSeesOwner.Results.Select(result => result.Packet!.GetType()));

		var ownerSeesCandidate = plan.Results[1].PacketConstructionPlan!;
		Assert.Equal(PlayerKnownListPlayerSideEffectPacketConstructionStatus.Constructed, ownerSeesCandidate.Status);
		Assert.Equal(
			[typeof(SmPlayerInfo), typeof(SmMotion), typeof(SmEmotion)],
			ownerSeesCandidate.Results.Select(result => result.Packet!.GetType()));
	}

	[Fact]
	public void Construct_MissingSubjectFactsBlocksOnlyThatDirectionalAttachment()
	{
		var attachmentPlan = CreateAddAttachmentPlan();
		var service = new PlayerKnownListOperationSideEffectPacketConstructionService();

		var plan = service.Construct(new PlayerKnownListOperationSideEffectPacketConstructionRequest(
			attachmentPlan,
			new Dictionary<int, PlayerKnownListOperationSideEffectPacketConstructionFacts>
			{
				[OwnerPlayerObjectId] = CreateFacts(OwnerPlayerObjectId, "Owner", stance: true),
			}));

		Assert.Equal(PlayerKnownListOperationSideEffectPacketConstructionStatus.PartiallyConstructed, plan.Status);
		Assert.Equal(PlayerKnownListOperationSideEffectPacketConstructionResultStatus.Constructed, plan.Results[0].Status);
		Assert.Equal(
			PlayerKnownListOperationSideEffectPacketConstructionResultStatus.BlockedMissingSubjectFacts,
			plan.Results[1].Status);
		Assert.Null(plan.Results[1].PacketConstructionPlan);
		Assert.Contains("subject player", plan.Results[1].Notes);
	}

	[Fact]
	public void Construct_PropagatesPartialPacketConstructionForMissingAbnormalFacts()
	{
		var operationPlanner = new PlayerKnownListTwoWayOperationPlanService();
		var attachmentService = new PlayerKnownListOperationSideEffectAttachmentService();
		var service = new PlayerKnownListOperationSideEffectPacketConstructionService();
		var operationPlan = operationPlanner.PlanAdd(new PlayerKnownListTwoWayOperationState(
			OwnerPlayerObjectId,
			CandidatePlayerObjectId,
			OwnerKnowsCandidate: false,
			CandidateKnowsOwner: false,
			OwnerSeesCandidate: true));
		var attachmentPlan = attachmentService.Attach(new PlayerKnownListOperationSideEffectAttachmentRequest(
			operationPlan,
			OwnerViewingCandidate: new PlayerKnownListOperationSideEffectDirectionFacts(SubjectHasAbnormalEffects: true),
			CandidateViewingOwner: new PlayerKnownListOperationSideEffectDirectionFacts()));

		var plan = service.Construct(new PlayerKnownListOperationSideEffectPacketConstructionRequest(
			attachmentPlan,
			new Dictionary<int, PlayerKnownListOperationSideEffectPacketConstructionFacts>
			{
				[CandidatePlayerObjectId] = CreateFacts(CandidatePlayerObjectId, "Candidate"),
			}));

		var result = Assert.Single(plan.Results);
		Assert.Equal(PlayerKnownListOperationSideEffectPacketConstructionStatus.PartiallyConstructed, plan.Status);
		Assert.Equal(PlayerKnownListOperationSideEffectPacketConstructionResultStatus.PartiallyConstructed, result.Status);
		Assert.Equal(PlayerKnownListPlayerSideEffectPacketConstructionStatus.PartiallyConstructed, result.PacketConstructionPlan!.Status);
		Assert.Contains(
			result.PacketConstructionPlan.Results,
			packet => packet.Status == PlayerKnownListPlayerSideEffectPacketConstructionResultStatus.BlockedMissingAbnormalEffectFacts);
	}

	[Fact]
	public void Construct_RemovePlanBuildsDeletePacketsForBothDirections()
	{
		var operationPlanner = new PlayerKnownListTwoWayOperationPlanService();
		var attachmentService = new PlayerKnownListOperationSideEffectAttachmentService();
		var service = new PlayerKnownListOperationSideEffectPacketConstructionService();
		var operationPlan = operationPlanner.PlanRemove(new PlayerKnownListTwoWayOperationState(
			OwnerPlayerObjectId,
			CandidatePlayerObjectId,
			OwnerKnowsCandidate: true,
			CandidateKnowsOwner: true,
			OwnerSeesCandidate: true,
			CandidateSeesOwner: true));
		var attachmentPlan = attachmentService.Attach(new PlayerKnownListOperationSideEffectAttachmentRequest(
			operationPlan,
			OwnerViewingCandidate: new PlayerKnownListOperationSideEffectDirectionFacts(NotSeeAnimation: ObjectDeleteAnimation.None),
			CandidateViewingOwner: new PlayerKnownListOperationSideEffectDirectionFacts(NotSeeAnimation: ObjectDeleteAnimation.JumpIn)));

		var plan = service.Construct(new PlayerKnownListOperationSideEffectPacketConstructionRequest(
			attachmentPlan,
			new Dictionary<int, PlayerKnownListOperationSideEffectPacketConstructionFacts>
			{
				[OwnerPlayerObjectId] = CreateFacts(OwnerPlayerObjectId, "Owner"),
				[CandidatePlayerObjectId] = CreateFacts(CandidatePlayerObjectId, "Candidate"),
			}));

		Assert.Equal(PlayerKnownListOperationSideEffectPacketConstructionStatus.Constructed, plan.Status);
		Assert.Equal(
			[PlayerKnownListTwoWayOperationStepKind.OwnerNotSeesCandidate, PlayerKnownListTwoWayOperationStepKind.CandidateNotSeesOwner],
			plan.Results.Select(result => result.AttachedSideEffect.OperationStep.Kind));
		Assert.All(plan.Results, result =>
		{
			var packet = Assert.Single(result.PacketConstructionPlan!.Results);
			Assert.IsType<SmDelete>(packet.Packet);
		});
	}

	private static PlayerKnownListOperationSideEffectAttachmentPlan CreateAddAttachmentPlan()
	{
		var operationPlanner = new PlayerKnownListTwoWayOperationPlanService();
		var attachmentService = new PlayerKnownListOperationSideEffectAttachmentService();
		var operationPlan = operationPlanner.PlanAdd(new PlayerKnownListTwoWayOperationState(
			OwnerPlayerObjectId,
			CandidatePlayerObjectId,
			OwnerKnowsCandidate: false,
			CandidateKnowsOwner: false,
			OwnerSeesCandidate: true,
			CandidateSeesOwner: true));

		return attachmentService.Attach(new PlayerKnownListOperationSideEffectAttachmentRequest(
			operationPlan,
			OwnerViewingCandidate: new PlayerKnownListOperationSideEffectDirectionFacts(
				ViewerAggroIconToSubject: true,
				SubjectIsInRideMode: true,
				SubjectRideNpcId: RideNpcId),
			CandidateViewingOwner: new PlayerKnownListOperationSideEffectDirectionFacts(
				SubjectIsUnderStance: true)));
	}

	private static PlayerKnownListOperationSideEffectPacketConstructionFacts CreateFacts(
		int objectId,
		string name,
		bool stance = false) =>
		new(
			new Player
			{
				ObjectId = objectId,
				Name = name,
				Race = "ELYOS",
				Gender = "MALE",
				PlayerClass = "GLADIATOR",
				Position = new WorldPosition(210010000, 1, 2, 3, 4),
			},
			ActiveMotions: stance ? [new PlayerMotion(11, 1010, true)] : []);

	private const int OwnerPlayerObjectId = 9001;
	private const int CandidatePlayerObjectId = 9002;
	private const int RideNpcId = 730001;
}
