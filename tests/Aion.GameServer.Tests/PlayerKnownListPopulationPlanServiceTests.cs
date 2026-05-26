using Aion.GameServer.Services;

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

	private static PlayerKnownListPopulationPlanService CreateService(PlayerKnownListMembershipService membership) =>
		new(
			new PlayerKnownListVisibilityRangePlanService(),
			new PlayerKnownListTwoWayMembershipAdapterService(membership),
			new PlayerKnownListOperationSideEffectAttachmentService());

	private static PlayerKnownListPopulationPlanRequest CreateRequest(
		IReadOnlyList<PlayerKnownListPopulationCandidateFact> candidateFacts,
		bool executeMembershipMutation = false) =>
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
			executeMembershipMutation);

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

	private const int OwnerPlayerObjectId = 9001;
	private const int NearPlayerObjectId = 9002;
	private const int FarPlayerObjectId = 9003;
	private const int MissingFactPlayerObjectId = 9004;
	private const int RideNpcId = 730001;
}
