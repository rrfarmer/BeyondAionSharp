using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class PlayerKnownListPopulationPlanServiceTests
{
	[Fact]
	public void Plan_DisabledComposesRegionCandidatesThroughRangePlansWithoutMutatingMembership()
	{
		var membership = new PlayerKnownListMembershipService();
		var service = CreateService(membership);
		var request = CreateRequest(
			[
				new PlayerKnownListPopulationCandidateFact(NearPlayerObjectId, X: 10, Y: 0, Z: 0),
				new PlayerKnownListPopulationCandidateFact(FarPlayerObjectId, X: 200, Y: 0, Z: 0),
			]);

		var plan = service.Plan(request);

		Assert.False(plan.IsLive);
		Assert.False(plan.IsJavaRegionKnownListParity);
		Assert.False(plan.MutatedMembership);
		Assert.False(plan.ExecutedControllerSideEffects);
		Assert.True(plan.AttachedControllerSideEffectDescriptors);
		Assert.False(plan.ConstructedControllerSideEffectPackets);
		Assert.Equal(2, plan.CandidatePlans.Count);
		Assert.True(plan.CandidatePlans[0].VisibilityRangePlan!.IsInJavaRange);
		Assert.False(plan.CandidatePlans[1].VisibilityRangePlan!.IsInJavaRange);
		Assert.All(plan.CandidatePlans, candidatePlan =>
		{
			Assert.Equal(PlayerKnownListTwoWayMembershipAdapterStatus.Disabled, candidatePlan.MembershipAdapterResult!.Status);
		});
		Assert.Empty(membership.GetKnownPlayerObjectIds(OwnerPlayerObjectId));
	}

	[Fact]
	public void Plan_EnabledAppliesInRangeMembershipAndPreservesOutOfRangeNoOp()
	{
		var membership = new PlayerKnownListMembershipService();
		var service = CreateService(membership);
		var request = CreateRequest(
			[
				new PlayerKnownListPopulationCandidateFact(NearPlayerObjectId, X: 10, Y: 0, Z: 0),
				new PlayerKnownListPopulationCandidateFact(FarPlayerObjectId, X: 200, Y: 0, Z: 0),
			],
			executeMembershipMutation: true);

		var plan = service.Plan(request);

		Assert.True(plan.ExecuteMembershipMutation);
		Assert.True(plan.MutatedMembership);
		Assert.Equal(PlayerKnownListTwoWayMembershipAdapterStatus.Applied, plan.CandidatePlans[0].MembershipAdapterResult!.Status);
		Assert.Equal(PlayerKnownListTwoWayMembershipAdapterStatus.SkippedRejectedPlan, plan.CandidatePlans[1].MembershipAdapterResult!.Status);
		Assert.Equal([NearPlayerObjectId], membership.GetKnownPlayerObjectIds(OwnerPlayerObjectId));
		Assert.Equal([OwnerPlayerObjectId], membership.GetKnownPlayerObjectIds(NearPlayerObjectId));
		Assert.Empty(membership.GetKnownPlayerObjectIds(FarPlayerObjectId));
	}

	[Fact]
	public void Plan_TracksMissingCandidateFactsWithoutCreatingRangePlan()
	{
		var membership = new PlayerKnownListMembershipService();
		var service = CreateService(membership);
		var request = CreateRequest([new PlayerKnownListPopulationCandidateFact(NearPlayerObjectId, X: 10, Y: 0, Z: 0)]);

		var plan = service.Plan(request with
		{
			RegionSnapshot = CreateRegionSnapshot([NearPlayerObjectId, MissingFactPlayerObjectId]),
		});

		Assert.Equal(1, plan.MissingCandidateFactCount);
		var missing = Assert.Single(plan.CandidatePlans, candidatePlan => candidatePlan.CandidatePlayerObjectId == MissingFactPlayerObjectId);
		Assert.Null(missing.VisibilityRangePlan);
		Assert.Null(missing.MembershipAdapterResult);
		Assert.Null(missing.SideEffectAttachmentPlan);
		Assert.Contains("omitted", missing.JavaSource);
	}

	[Fact]
	public void Plan_UsesCandidateFactsForVisibilityAndExistingKnownState()
	{
		var membership = new PlayerKnownListMembershipService();
		var service = CreateService(membership);
		var request = CreateRequest(
			[
				new PlayerKnownListPopulationCandidateFact(
					NearPlayerObjectId,
					X: 200,
					Y: 0,
					Z: 0,
					OwnerCanSeeCandidate: true,
					CandidateCanSeeOwner: true,
					OwnerKnowsCandidate: true,
					CandidateKnowsOwner: true),
			],
			executeMembershipMutation: true);
		membership.UpsertKnownPlayers(OwnerPlayerObjectId, [new PlayerKnownListMembershipCandidate(NearPlayerObjectId, IsVisibleToOwner: true)]);
		membership.UpsertKnownPlayers(NearPlayerObjectId, [new PlayerKnownListMembershipCandidate(OwnerPlayerObjectId, IsVisibleToOwner: true)]);

		var plan = service.Plan(request);

		var candidatePlan = Assert.Single(plan.CandidatePlans, candidatePlan => candidatePlan.CandidatePlayerObjectId == NearPlayerObjectId);
		Assert.False(candidatePlan.VisibilityRangePlan!.IsInJavaRange);
		Assert.Equal(PlayerKnownListTwoWayOperationKind.Remove, candidatePlan.VisibilityRangePlan.OperationPlan.Kind);
		Assert.Equal(PlayerKnownListTwoWayMembershipAdapterStatus.Applied, candidatePlan.MembershipAdapterResult!.Status);
		Assert.Empty(membership.GetKnownPlayerObjectIds(OwnerPlayerObjectId));
		Assert.Empty(membership.GetKnownPlayerObjectIds(NearPlayerObjectId));
		Assert.Equal(
			[
				PlayerKnownListTwoWayOperationStepKind.OwnerNotSeesCandidate,
				PlayerKnownListTwoWayOperationStepKind.OwnerNotKnowsCandidate,
				PlayerKnownListTwoWayOperationStepKind.CandidateNotSeesOwner,
				PlayerKnownListTwoWayOperationStepKind.CandidateNotKnowsOwner,
			],
			candidatePlan.MembershipAdapterResult.PreservedSideEffectSteps.Select(step => step.Kind));
	}

	[Fact]
	public void Plan_AttachesPlayerSideEffectDescriptorsToVisibleOperationPlansWithoutExecutingThem()
	{
		var membership = new PlayerKnownListMembershipService();
		var service = CreateService(membership);
		var request = CreateRequest(
			[
				new PlayerKnownListPopulationCandidateFact(
					NearPlayerObjectId,
					X: 10,
					Y: 0,
					Z: 0,
					OwnerCanSeeCandidate: true,
					CandidateCanSeeOwner: true,
					OwnerViewingCandidateSideEffectFacts: new PlayerKnownListOperationSideEffectDirectionFacts(
						ViewerAggroIconToSubject: true,
						SubjectIsInRideMode: true,
						SubjectRideNpcId: RideNpcId),
					CandidateViewingOwnerSideEffectFacts: new PlayerKnownListOperationSideEffectDirectionFacts(
						SubjectIsUnderStance: true)),
			]);

		var plan = service.Plan(request);

		Assert.True(plan.AttachedControllerSideEffectDescriptors);
		Assert.False(plan.ExecutedControllerSideEffects);
		var candidatePlan = Assert.Single(plan.CandidatePlans, candidatePlan => candidatePlan.CandidatePlayerObjectId == NearPlayerObjectId);
		var attachmentPlan = candidatePlan.SideEffectAttachmentPlan!;
		Assert.Equal(PlayerKnownListOperationSideEffectAttachmentStatus.Attached, attachmentPlan.Status);
		Assert.Equal(
			[PlayerKnownListTwoWayOperationStepKind.CandidateSeesOwner, PlayerKnownListTwoWayOperationStepKind.OwnerSeesCandidate],
			attachmentPlan.AttachedSideEffects.Select(attachment => attachment.OperationStep.Kind));
		Assert.Equal(NearPlayerObjectId, attachmentPlan.AttachedSideEffects[1].SideEffectPlan.SubjectPlayerObjectId);
		Assert.True(attachmentPlan.AttachedSideEffects[1].SideEffectPlan.Descriptors[0].AggroIcon);
		Assert.Equal(RideNpcId, attachmentPlan.AttachedSideEffects[1].SideEffectPlan.Descriptors[2].RideNpcId);
		Assert.Contains(
			attachmentPlan.AttachedSideEffects[0].SideEffectPlan.Descriptors,
			descriptor => descriptor.Kind == PlayerKnownListPlayerSideEffectKind.SmPlayerStance);
		Assert.Null(candidatePlan.SideEffectPacketConstructionPlan);
	}

	[Fact]
	public void Plan_AttachesSkippedNotSeeDescriptorForOutOfRangeUnspawnedViewer()
	{
		var membership = new PlayerKnownListMembershipService();
		var service = CreateService(membership);
		var request = CreateRequest(
			[
				new PlayerKnownListPopulationCandidateFact(
					NearPlayerObjectId,
					X: 200,
					Y: 0,
					Z: 0,
					OwnerKnowsCandidate: true,
					CandidateKnowsOwner: false,
					OwnerViewingCandidateSideEffectFacts: new PlayerKnownListOperationSideEffectDirectionFacts(ViewerIsSpawned: false)),
			]);

		var plan = service.Plan(request);

		Assert.True(plan.AttachedControllerSideEffectDescriptors);
		var candidatePlan = Assert.Single(plan.CandidatePlans, candidatePlan => candidatePlan.CandidatePlayerObjectId == NearPlayerObjectId);
		var attachment = Assert.Single(candidatePlan.SideEffectAttachmentPlan!.AttachedSideEffects);
		Assert.Equal(PlayerKnownListTwoWayOperationStepKind.OwnerNotSeesCandidate, attachment.OperationStep.Kind);
		Assert.Equal(PlayerKnownListPlayerSideEffectStatus.SkippedViewerNotSpawned, attachment.SideEffectPlan.Status);
		Assert.Empty(attachment.SideEffectPlan.Descriptors);
	}

	[Fact]
	public void Plan_ConstructsPopulationSideEffectPacketMetadataWhenSubjectFactsAreSupplied()
	{
		var membership = new PlayerKnownListMembershipService();
		var service = CreateService(membership);
		var request = CreateRequest(
			[
				new PlayerKnownListPopulationCandidateFact(
					NearPlayerObjectId,
					X: 10,
					Y: 0,
					Z: 0,
					OwnerCanSeeCandidate: true,
					CandidateCanSeeOwner: true,
					OwnerViewingCandidateSideEffectFacts: new PlayerKnownListOperationSideEffectDirectionFacts(
						ViewerAggroIconToSubject: true,
						SubjectIsInRideMode: true,
						SubjectRideNpcId: RideNpcId),
					CandidateViewingOwnerSideEffectFacts: new PlayerKnownListOperationSideEffectDirectionFacts(
						SubjectIsUnderStance: true)),
			],
			packetConstructionFactsByPlayerObjectId: new Dictionary<int, PlayerKnownListOperationSideEffectPacketConstructionFacts>
			{
				[OwnerPlayerObjectId] = CreatePacketFacts(OwnerPlayerObjectId, "Owner", stance: true),
				[NearPlayerObjectId] = CreatePacketFacts(NearPlayerObjectId, "Candidate", rideMovementSpeed: 6.25f),
			});

		var plan = service.Plan(request);

		Assert.True(plan.AttachedControllerSideEffectDescriptors);
		Assert.True(plan.ConstructedControllerSideEffectPackets);
		Assert.False(plan.ExecutedControllerSideEffects);
		var candidatePlan = Assert.Single(plan.CandidatePlans, candidatePlan => candidatePlan.CandidatePlayerObjectId == NearPlayerObjectId);
		var packetPlan = candidatePlan.SideEffectPacketConstructionPlan!;
		Assert.False(packetPlan.ExecutesLivePackets);
		Assert.False(packetPlan.IsLive);
		Assert.Equal(PlayerKnownListOperationSideEffectPacketConstructionStatus.Constructed, packetPlan.Status);
		Assert.Equal(
			[PlayerKnownListTwoWayOperationStepKind.CandidateSeesOwner, PlayerKnownListTwoWayOperationStepKind.OwnerSeesCandidate],
			packetPlan.Results.Select(result => result.AttachedSideEffect.OperationStep.Kind));
		Assert.Equal(
			[typeof(SmPlayerInfo), typeof(SmMotion), typeof(SmPlayerStance)],
			packetPlan.Results[0].PacketConstructionPlan!.Results.Select(result => result.Packet!.GetType()));
		Assert.Equal(
			[typeof(SmPlayerInfo), typeof(SmMotion), typeof(SmEmotion)],
			packetPlan.Results[1].PacketConstructionPlan!.Results.Select(result => result.Packet!.GetType()));
	}

	[Fact]
	public void Plan_RecordsPartialPopulationPacketMetadataWhenSubjectFactsAreMissing()
	{
		var membership = new PlayerKnownListMembershipService();
		var service = CreateService(membership);
		var request = CreateRequest(
			[
				new PlayerKnownListPopulationCandidateFact(
					NearPlayerObjectId,
					X: 10,
					Y: 0,
					Z: 0,
					OwnerCanSeeCandidate: true,
					CandidateCanSeeOwner: true,
					OwnerViewingCandidateSideEffectFacts: new PlayerKnownListOperationSideEffectDirectionFacts(
						SubjectIsInRideMode: true,
						SubjectRideNpcId: RideNpcId),
					CandidateViewingOwnerSideEffectFacts: new PlayerKnownListOperationSideEffectDirectionFacts(
						SubjectIsUnderStance: true)),
			],
			packetConstructionFactsByPlayerObjectId: new Dictionary<int, PlayerKnownListOperationSideEffectPacketConstructionFacts>
			{
				[OwnerPlayerObjectId] = CreatePacketFacts(OwnerPlayerObjectId, "Owner", stance: true),
			});

		var plan = service.Plan(request);

		Assert.True(plan.ConstructedControllerSideEffectPackets);
		var candidatePlan = Assert.Single(plan.CandidatePlans, candidatePlan => candidatePlan.CandidatePlayerObjectId == NearPlayerObjectId);
		var packetPlan = candidatePlan.SideEffectPacketConstructionPlan!;
		Assert.Equal(PlayerKnownListOperationSideEffectPacketConstructionStatus.PartiallyConstructed, packetPlan.Status);
		Assert.Equal(PlayerKnownListOperationSideEffectPacketConstructionResultStatus.Constructed, packetPlan.Results[0].Status);
		Assert.Equal(
			PlayerKnownListOperationSideEffectPacketConstructionResultStatus.BlockedMissingSubjectFacts,
			packetPlan.Results[1].Status);
		Assert.Null(packetPlan.Results[1].PacketConstructionPlan);
	}

	[Fact]
	public void Plan_ComposesFactPlansIntoPopulationPacketConstructionMetadata()
	{
		var membership = new PlayerKnownListMembershipService();
		var service = CreateService(membership);
		var owner = CreatePlayer(OwnerPlayerObjectId, "Owner", "ELYOS");
		owner.StanceSkillId = 1200;
		owner.Motions = [new PlayerMotion(11, 1010, true)];
		var candidate = CreatePlayer(NearPlayerObjectId, "Candidate", "ASMODIANS");
		candidate.MountRide(new PlayerRideInfo(RideNpcId, StartFp: 0, CostFp: null, SprintSpeed: 9.5f, FlySpeed: 10.5f, MoveSpeed: 7.25f));
		candidate.Motions = [new PlayerMotion(12, 1010, true)];
		var ownerViewingCandidate = new PlayerKnownListOperationSideEffectDirectionFacts(
			ViewerAggroIconToSubject: true,
			SubjectIsInRideMode: true,
			SubjectRideNpcId: RideNpcId);
		var candidateViewingOwner = new PlayerKnownListOperationSideEffectDirectionFacts(
			SubjectIsUnderStance: true);
		var request = CreateRequest(
			[
				new PlayerKnownListPopulationCandidateFact(
					NearPlayerObjectId,
					X: 10,
					Y: 0,
					Z: 0,
					OwnerCanSeeCandidate: true,
					CandidateCanSeeOwner: true,
					OwnerViewingCandidateSideEffectFacts: ownerViewingCandidate,
					CandidateViewingOwnerSideEffectFacts: candidateViewingOwner,
					OwnerViewingCandidatePacketFactPlanRequest: new PlayerKnownListPacketConstructionFactPlanRequest(
						owner,
						candidate,
						ownerViewingCandidate,
						ViewerIsEnemyToSubject: true,
						RideAttackSpeedFacts: new PlayerKnownListPacketConstructionAttackSpeedFacts(1400, 1200)),
					CandidateViewingOwnerPacketFactPlanRequest: new PlayerKnownListPacketConstructionFactPlanRequest(
						candidate,
						owner,
						candidateViewingOwner)),
			]);

		var plan = service.Plan(request);

		Assert.True(plan.ConstructedControllerSideEffectPackets);
		var candidatePlan = Assert.Single(plan.CandidatePlans, candidatePlan => candidatePlan.CandidatePlayerObjectId == NearPlayerObjectId);
		Assert.Equal(
			[
				PlayerKnownListPopulationPacketConstructionFactPlanDirection.OwnerViewingCandidate,
				PlayerKnownListPopulationPacketConstructionFactPlanDirection.CandidateViewingOwner,
			],
			candidatePlan.SideEffectFactPlans!.Select(factPlan => factPlan.Direction));
		Assert.All(candidatePlan.SideEffectFactPlans!, factPlan =>
			Assert.Equal(PlayerKnownListPacketConstructionFactPlanStatus.Complete, factPlan.Plan.Status));
		var packetPlan = candidatePlan.SideEffectPacketConstructionPlan!;
		Assert.Equal(PlayerKnownListOperationSideEffectPacketConstructionStatus.Constructed, packetPlan.Status);
		Assert.Equal(
			[typeof(SmPlayerInfo), typeof(SmMotion), typeof(SmPlayerStance)],
			packetPlan.Results[0].PacketConstructionPlan!.Results.Select(result => result.Packet!.GetType()));
		Assert.Equal(
			[typeof(SmPlayerInfo), typeof(SmMotion), typeof(SmEmotion)],
			packetPlan.Results[1].PacketConstructionPlan!.Results.Select(result => result.Packet!.GetType()));
	}

	[Fact]
	public void Plan_AttachesResolvedRideAttackSpeedToGeneratedFactPlanRequests()
	{
		var membership = new PlayerKnownListMembershipService();
		var service = CreateService(membership);
		var owner = CreatePlayer(OwnerPlayerObjectId, "Owner", "ELYOS");
		var candidate = CreatePlayer(NearPlayerObjectId, "Candidate", "ASMODIANS");
		candidate.MountRide(new PlayerRideInfo(RideNpcId, StartFp: 0, CostFp: null, SprintSpeed: 9.5f, FlySpeed: 10.5f, MoveSpeed: 7.25f));
		candidate.InventoryItems =
		[
			new InventoryItem { ObjectId = 1001, ItemId = MainHandSwordId, Location = 0, IsEquipped = true, Slot = MainHandSlot },
		];
		var ownerViewingCandidate = new PlayerKnownListOperationSideEffectDirectionFacts(
			SubjectIsInRideMode: true,
			SubjectRideNpcId: RideNpcId);
		var request = CreateRequest(
			[
				new PlayerKnownListPopulationCandidateFact(
					NearPlayerObjectId,
					X: 10,
					Y: 0,
					Z: 0,
					OwnerCanSeeCandidate: true,
					CandidateCanSeeOwner: false,
					OwnerViewingCandidateSideEffectFacts: ownerViewingCandidate,
					OwnerViewingCandidatePacketFactPlanRequest: new PlayerKnownListPacketConstructionFactPlanRequest(
						owner,
						candidate,
						ownerViewingCandidate)),
			],
			itemTemplates: CreateItemTemplates());

		var plan = service.Plan(request);

		var candidatePlan = Assert.Single(plan.CandidatePlans, candidatePlan => candidatePlan.CandidatePlayerObjectId == NearPlayerObjectId);
		var factPlan = Assert.Single(candidatePlan.SideEffectFactPlans!);
		Assert.NotNull(factPlan.Request.RideAttackSpeedResolution);
		Assert.Equal(PlayerKnownListAttackSpeedFactResolutionStatus.ResolvedApproximation, factPlan.Request.RideAttackSpeedResolution.Status);
		Assert.Equal(PlayerKnownListPacketConstructionAttackSpeedFactSource.ResolvedApproximation, factPlan.Plan.RideAttackSpeedFactSource);
		Assert.Equal(PlayerKnownListPacketConstructionFactPlanStatus.Complete, factPlan.Plan.Status);
		Assert.Equal(1400, factPlan.Plan.Facts!.RideBaseAttackSpeed);
		Assert.Equal(PlayerKnownListPopulationPacketConstructionFactSourceKind.GeneratedFactPlan, Assert.Single(candidatePlan.PacketConstructionFactSources!).Kind);
	}

	[Fact]
	public void Plan_PreservesBlockedFactPlanMetadataAndPartialPacketConstruction()
	{
		var membership = new PlayerKnownListMembershipService();
		var service = CreateService(membership);
		var owner = CreatePlayer(OwnerPlayerObjectId, "Owner", "ELYOS");
		owner.StanceSkillId = 1200;
		var candidate = CreatePlayer(NearPlayerObjectId, "Candidate", "ASMODIANS");
		candidate.IsInRideMode = true;
		var ownerViewingCandidate = new PlayerKnownListOperationSideEffectDirectionFacts(
			SubjectIsInRideMode: true,
			SubjectRideNpcId: RideNpcId);
		var candidateViewingOwner = new PlayerKnownListOperationSideEffectDirectionFacts(
			SubjectIsUnderStance: true);
		var request = CreateRequest(
			[
				new PlayerKnownListPopulationCandidateFact(
					NearPlayerObjectId,
					X: 10,
					Y: 0,
					Z: 0,
					OwnerCanSeeCandidate: true,
					CandidateCanSeeOwner: true,
					OwnerViewingCandidateSideEffectFacts: ownerViewingCandidate,
					CandidateViewingOwnerSideEffectFacts: candidateViewingOwner,
					OwnerViewingCandidatePacketFactPlanRequest: new PlayerKnownListPacketConstructionFactPlanRequest(
						owner,
						candidate,
						ownerViewingCandidate),
					CandidateViewingOwnerPacketFactPlanRequest: new PlayerKnownListPacketConstructionFactPlanRequest(
						candidate,
						owner,
						candidateViewingOwner)),
			]);

		var plan = service.Plan(request);

		Assert.True(plan.ConstructedControllerSideEffectPackets);
		var candidatePlan = Assert.Single(plan.CandidatePlans, candidatePlan => candidatePlan.CandidatePlayerObjectId == NearPlayerObjectId);
		var blockedFactPlan = Assert.Single(
			candidatePlan.SideEffectFactPlans!,
			factPlan => factPlan.Direction == PlayerKnownListPopulationPacketConstructionFactPlanDirection.OwnerViewingCandidate);
		Assert.Equal(PlayerKnownListPacketConstructionFactPlanStatus.Blocked, blockedFactPlan.Plan.Status);
		Assert.Contains(PlayerKnownListPacketConstructionFactBlocker.MissingRideInfo, blockedFactPlan.Plan.Blockers);
		Assert.Contains(PlayerKnownListPacketConstructionFactBlocker.MissingRideAttackSpeedFacts, blockedFactPlan.Plan.Blockers);
		var packetPlan = candidatePlan.SideEffectPacketConstructionPlan!;
		Assert.Equal(PlayerKnownListOperationSideEffectPacketConstructionStatus.PartiallyConstructed, packetPlan.Status);
		Assert.Equal(PlayerKnownListOperationSideEffectPacketConstructionResultStatus.Constructed, packetPlan.Results[0].Status);
		Assert.Equal(
			PlayerKnownListOperationSideEffectPacketConstructionResultStatus.BlockedMissingSubjectFacts,
			packetPlan.Results[1].Status);
	}

	[Fact]
	public void Plan_RequestLevelPacketFactsRemainAuthoritativeOverGeneratedFactPlans()
	{
		var membership = new PlayerKnownListMembershipService();
		var service = CreateService(membership);
		var owner = CreatePlayer(OwnerPlayerObjectId, "Owner", "ELYOS");
		var candidate = CreatePlayer(NearPlayerObjectId, "Candidate", "ASMODIANS");
		candidate.MountRide(new PlayerRideInfo(RideNpcId, StartFp: 0, CostFp: null, SprintSpeed: 9.5f, FlySpeed: 10.5f, MoveSpeed: 7.25f));
		var explicitCandidateFacts = CreatePacketFacts(NearPlayerObjectId, "ExplicitCandidate", rideMovementSpeed: 3.5f);
		var ownerViewingCandidate = new PlayerKnownListOperationSideEffectDirectionFacts(
			SubjectIsInRideMode: true,
			SubjectRideNpcId: RideNpcId);
		var request = CreateRequest(
			[
				new PlayerKnownListPopulationCandidateFact(
					NearPlayerObjectId,
					X: 10,
					Y: 0,
					Z: 0,
					OwnerCanSeeCandidate: true,
					CandidateCanSeeOwner: false,
					OwnerViewingCandidateSideEffectFacts: ownerViewingCandidate,
					OwnerViewingCandidatePacketFactPlanRequest: new PlayerKnownListPacketConstructionFactPlanRequest(
						owner,
						candidate,
						ownerViewingCandidate,
						RideAttackSpeedFacts: new PlayerKnownListPacketConstructionAttackSpeedFacts(1400, 1200),
						RideMovementSpeed: 8.75f)),
			],
			packetConstructionFactsByPlayerObjectId: new Dictionary<int, PlayerKnownListOperationSideEffectPacketConstructionFacts>
			{
				[NearPlayerObjectId] = explicitCandidateFacts,
			});

		var plan = service.Plan(request);

		var candidatePlan = Assert.Single(plan.CandidatePlans, candidatePlan => candidatePlan.CandidatePlayerObjectId == NearPlayerObjectId);
		Assert.Equal(PlayerKnownListPacketConstructionFactPlanStatus.Complete, Assert.Single(candidatePlan.SideEffectFactPlans!).Plan.Status);
		Assert.Equal(
			[
				PlayerKnownListPopulationPacketConstructionFactSourceKind.Request,
				PlayerKnownListPopulationPacketConstructionFactSourceKind.GeneratedFactPlanIgnoredByRequest,
			],
			candidatePlan.PacketConstructionFactSources!.Select(source => source.Kind));
		Assert.All(candidatePlan.PacketConstructionFactSources!, source => Assert.Equal(NearPlayerObjectId, source.SubjectPlayerObjectId));
		Assert.Null(candidatePlan.PacketConstructionFactSources![0].GeneratedFromDirection);
		Assert.Equal(
			PlayerKnownListPopulationPacketConstructionFactPlanDirection.OwnerViewingCandidate,
			candidatePlan.PacketConstructionFactSources![1].GeneratedFromDirection);
		var packetPlan = candidatePlan.SideEffectPacketConstructionPlan!;
		var rideResult = packetPlan.Results
			.SelectMany(result => result.PacketConstructionPlan!.Results)
			.Single(result => result.Descriptor.Kind == PlayerKnownListPlayerSideEffectKind.SmEmotionRide);
		var packet = Assert.IsType<SmEmotion>(rideResult.Packet);
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(NearPlayerObjectId, reader.ReadD());
		Assert.Equal((int)EmotionType.Ride, (int)reader.ReadC());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(3.5f, reader.ReadF());
	}

	private static PlayerKnownListPopulationPlanService CreateService(PlayerKnownListMembershipService membership) =>
		new(
			new PlayerKnownListVisibilityRangePlanService(),
			new PlayerKnownListTwoWayMembershipAdapterService(membership),
			new PlayerKnownListOperationSideEffectAttachmentService());

	private static PlayerKnownListPopulationPlanRequest CreateRequest(
		IReadOnlyList<PlayerKnownListPopulationCandidateFact> candidateFacts,
		bool executeMembershipMutation = false,
		IReadOnlyDictionary<int, PlayerKnownListOperationSideEffectPacketConstructionFacts>? packetConstructionFactsByPlayerObjectId = null,
		ItemTemplateTable? itemTemplates = null) =>
		new(
			CreateRegionSnapshot([NearPlayerObjectId, FarPlayerObjectId]),
			new PlayerKnownListVisibilityRangeObject(
				OwnerPlayerObjectId,
				WorldId: 210010000,
				InstanceId: 1,
				X: 0,
				Y: 0,
				Z: 0),
			candidateFacts,
			executeMembershipMutation,
			packetConstructionFactsByPlayerObjectId,
			itemTemplates);

	private static PlayerKnownListRegionSnapshot CreateRegionSnapshot(IReadOnlyList<int> candidateIds) =>
		new(
			OwnerPlayerObjectId,
			new PlayerKnownListRegionKey(WorldId: 210010000, InstanceId: 1, RegionId: 10),
			ScannedRegionIds: [10, 11],
			CandidatePlayerObjectIds: candidateIds,
			SourcePlayerCount: candidateIds.Count + 1,
			ExcludedOwnerCount: 1,
			ExcludedDifferentWorldOrInstanceCount: 0,
			ExcludedOutsideNeighbourRegionsCount: 0,
			ExcludedUnspawnedCount: 0,
			ExcludesOwnerByNormalAddPath: true,
			DeduplicatesByObjectId: true,
			PreservesSuppliedRegionOrdering: true,
			IsJavaRegionKnownListParity: false,
			"test region snapshot",
			IsLive: false);

	private static PlayerKnownListOperationSideEffectPacketConstructionFacts CreatePacketFacts(
		int objectId,
		string name,
		bool stance = false,
		float rideMovementSpeed = 0) =>
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
			ActiveMotions: stance ? [new PlayerMotion(11, 1010, true)] : [],
			RideMovementSpeed: rideMovementSpeed);

	private static Player CreatePlayer(int objectId, string name, string race) =>
		new()
		{
			ObjectId = objectId,
			Name = name,
			Race = race,
			Gender = "MALE",
			PlayerClass = "GLADIATOR",
			Position = new WorldPosition(210010000, 1, 2, 3, 4),
		};

	private static ItemTemplateTable CreateItemTemplates() =>
		new(
		[
			new ItemTemplateSummary(
				MainHandSwordId,
				"weapon",
				DescriptionId: 0,
				Mask: 0,
				Level: 1,
				"SWORD",
				ItemType: "WEAPON",
				Quality: "COMMON",
				Race: "PC_ALL",
				MaxStackCount: 1,
				Price: 1,
				ValidEquipmentSlots: MainHandSlot,
				WeaponStats: new ItemWeaponStats(
					MinDamage: 1,
					MaxDamage: 2,
					AttackSpeed: 1400,
					PhysicalCritical: 0,
					PhysicalAccuracy: 0,
					Parry: 0,
					MagicalAccuracy: 0,
					MagicalBoost: 0,
					AttackRange: 1500,
					HitCount: 1,
					ReduceMax: 0)),
		]);

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x12345678);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}

	private const int OwnerPlayerObjectId = 9001;
	private const int NearPlayerObjectId = 9002;
	private const int FarPlayerObjectId = 9003;
	private const int MissingFactPlayerObjectId = 9004;
	private const int RideNpcId = 730001;
	private const int MainHandSwordId = 100000001;
	private const long MainHandSlot = 1L;
}
