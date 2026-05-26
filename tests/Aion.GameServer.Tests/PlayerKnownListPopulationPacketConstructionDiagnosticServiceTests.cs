using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class PlayerKnownListPopulationPacketConstructionDiagnosticServiceTests
{
	[Fact]
	public void Summarize_CompletePopulationMetadataPreservesCandidateAndOperationOrdering()
	{
		var population = CreatePopulationService();
		var diagnostics = new PlayerKnownListPopulationPacketConstructionDiagnosticService();
		var owner = CreatePlayer(OwnerPlayerObjectId, "Owner", "ELYOS");
		owner.StanceSkillId = 1200;
		var candidate = CreatePlayer(NearPlayerObjectId, "Candidate", "ASMODIANS");
		candidate.MountRide(new PlayerRideInfo(RideNpcId, StartFp: 0, CostFp: null, SprintSpeed: 9.5f, FlySpeed: 10.5f, MoveSpeed: 7.25f));
		var ownerViewingCandidate = new PlayerKnownListOperationSideEffectDirectionFacts(
			ViewerAggroIconToSubject: true,
			SubjectIsInRideMode: true,
			SubjectRideNpcId: RideNpcId);
		var candidateViewingOwner = new PlayerKnownListOperationSideEffectDirectionFacts(SubjectIsUnderStance: true);
		var plan = population.Plan(CreateRequest(
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
						RideAttackSpeedFacts: new PlayerKnownListPacketConstructionAttackSpeedFacts(1400, 1200)),
					CandidateViewingOwnerPacketFactPlanRequest: new PlayerKnownListPacketConstructionFactPlanRequest(
						candidate,
						owner,
						candidateViewingOwner)),
			]));

		var diagnostic = diagnostics.Summarize(plan);

		Assert.Equal(PlayerKnownListPopulationPacketConstructionDiagnosticStatus.Complete, diagnostic.Status);
		Assert.False(diagnostic.ExecutesLivePackets);
		Assert.False(diagnostic.IsLive);
		Assert.False(diagnostic.IsJavaControllerParity);
		Assert.Equal(2, diagnostic.CandidateCount);
		Assert.Equal(1, diagnostic.CandidatesWithRangePlanCount);
		Assert.Equal(1, diagnostic.AttachedSideEffectDescriptorCandidateCount);
		Assert.Equal(2, diagnostic.AttachedSideEffectCount);
		Assert.Equal(2, diagnostic.CandidateFactPlanCount);
		Assert.Equal(2, diagnostic.CompletedFactPlanCount);
		Assert.Equal(0, diagnostic.BlockedFactPlanCount);
		Assert.Empty(diagnostic.FactPlanBlockerCountsByKind);
		Assert.Equal(1, diagnostic.PacketConstructionPlanCount);
		Assert.Equal(1, diagnostic.ConstructedPacketConstructionPlanCount);
		Assert.Equal(2, diagnostic.PacketConstructionResultCount);
		Assert.Equal(2, diagnostic.ConstructedPacketConstructionResultCount);
		Assert.Equal(6, diagnostic.ConstructedPlayerPacketCount);
		Assert.Equal(0, diagnostic.BlockedPlayerPacketCount);
		Assert.Equal(6, diagnostic.PlayerPacketResultStatusCountsByKind[PlayerKnownListPlayerSideEffectPacketConstructionResultStatus.Constructed]);
		Assert.Equal(2, diagnostic.ConstructedPacketCountsByKind[PlayerKnownListPlayerSideEffectKind.SmPlayerInfo]);
		Assert.Equal(2, diagnostic.ConstructedPacketCountsByKind[PlayerKnownListPlayerSideEffectKind.SmMotion]);
		Assert.Equal(1, diagnostic.ConstructedPacketCountsByKind[PlayerKnownListPlayerSideEffectKind.SmEmotionRide]);
		Assert.Equal(1, diagnostic.ConstructedPacketCountsByKind[PlayerKnownListPlayerSideEffectKind.SmPlayerStance]);
		var candidateDiagnostic = diagnostic.CandidateDiagnostics[0];
		Assert.Equal(NearPlayerObjectId, candidateDiagnostic.CandidatePlayerObjectId);
		Assert.Equal(0, candidateDiagnostic.CandidateOrder);
		Assert.Equal(PlayerKnownListPopulationPacketConstructionDiagnosticStatus.Complete, candidateDiagnostic.Status);
		Assert.Equal(
			[
				PlayerKnownListPopulationPacketConstructionFactPlanDirection.OwnerViewingCandidate,
				PlayerKnownListPopulationPacketConstructionFactPlanDirection.CandidateViewingOwner,
			],
			candidateDiagnostic.FactPlans.Select(factPlan => factPlan.Direction));
		Assert.Equal(
			[PlayerKnownListTwoWayOperationStepKind.CandidateSeesOwner, PlayerKnownListTwoWayOperationStepKind.OwnerSeesCandidate],
			candidateDiagnostic.PacketConstructionResults.Select(result => result.OperationStepKind));
		Assert.Equal(FarPlayerObjectId, diagnostic.CandidateDiagnostics[1].CandidatePlayerObjectId);
		Assert.Equal(PlayerKnownListPopulationPacketConstructionDiagnosticStatus.NoPacketConstructionMetadata, diagnostic.CandidateDiagnostics[1].Status);
	}

	[Fact]
	public void Summarize_PartialMetadataSurfacesBlockedFactPlansAndPacketConstructionResults()
	{
		var population = CreatePopulationService();
		var diagnostics = new PlayerKnownListPopulationPacketConstructionDiagnosticService();
		var owner = CreatePlayer(OwnerPlayerObjectId, "Owner", "ELYOS");
		owner.StanceSkillId = 1200;
		var candidate = CreatePlayer(NearPlayerObjectId, "Candidate", "ASMODIANS");
		candidate.IsInRideMode = true;
		var ownerViewingCandidate = new PlayerKnownListOperationSideEffectDirectionFacts(
			SubjectIsInRideMode: true,
			SubjectRideNpcId: RideNpcId);
		var candidateViewingOwner = new PlayerKnownListOperationSideEffectDirectionFacts(SubjectIsUnderStance: true);
		var plan = population.Plan(CreateRequest(
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
			]));

		var diagnostic = diagnostics.Summarize(plan);

		Assert.Equal(PlayerKnownListPopulationPacketConstructionDiagnosticStatus.Partial, diagnostic.Status);
		Assert.Equal(2, diagnostic.CandidateFactPlanCount);
		Assert.Equal(1, diagnostic.CompletedFactPlanCount);
		Assert.Equal(1, diagnostic.BlockedFactPlanCount);
		Assert.Equal(1, diagnostic.FactPlanBlockerCountsByKind[PlayerKnownListPacketConstructionFactBlocker.MissingRideInfo]);
		Assert.Equal(1, diagnostic.FactPlanBlockerCountsByKind[PlayerKnownListPacketConstructionFactBlocker.MissingRideAttackSpeedFacts]);
		Assert.Equal(1, diagnostic.PartiallyConstructedPacketConstructionPlanCount);
		Assert.Equal(1, diagnostic.ConstructedPacketConstructionResultCount);
		Assert.Equal(1, diagnostic.BlockedMissingSubjectFactsResultCount);
		Assert.Equal(3, diagnostic.ConstructedPlayerPacketCount);
		Assert.Equal(0, diagnostic.BlockedPlayerPacketCount);
		var candidateDiagnostic = diagnostic.CandidateDiagnostics[0];
		Assert.Equal(PlayerKnownListPopulationPacketConstructionDiagnosticStatus.Partial, candidateDiagnostic.Status);
		var blockedFactPlan = Assert.Single(
			candidateDiagnostic.FactPlans,
			factPlan => factPlan.Status == PlayerKnownListPacketConstructionFactPlanStatus.Blocked);
		Assert.Equal(PlayerKnownListPopulationPacketConstructionFactPlanDirection.OwnerViewingCandidate, blockedFactPlan.Direction);
		Assert.Contains(PlayerKnownListPacketConstructionFactBlocker.MissingRideInfo, blockedFactPlan.Blockers);
		Assert.Contains(PlayerKnownListPacketConstructionFactBlocker.MissingRideAttackSpeedFacts, blockedFactPlan.Blockers);
		Assert.Equal(
			PlayerKnownListOperationSideEffectPacketConstructionResultStatus.BlockedMissingSubjectFacts,
			candidateDiagnostic.PacketConstructionResults[1].Status);
	}

	[Fact]
	public void Summarize_NoPacketConstructionMetadataStillReportsPopulationShape()
	{
		var population = CreatePopulationService();
		var diagnostics = new PlayerKnownListPopulationPacketConstructionDiagnosticService();
		var plan = population.Plan(CreateRequest(
			[new PlayerKnownListPopulationCandidateFact(NearPlayerObjectId, X: 10, Y: 0, Z: 0)],
			regionCandidateIds: [NearPlayerObjectId, MissingFactPlayerObjectId]));

		var diagnostic = diagnostics.Summarize(plan);

		Assert.Equal(PlayerKnownListPopulationPacketConstructionDiagnosticStatus.NoPacketConstructionMetadata, diagnostic.Status);
		Assert.Equal(2, diagnostic.CandidateCount);
		Assert.Equal(1, diagnostic.MissingCandidateFactCount);
		Assert.Equal(0, diagnostic.CandidateFactPlanCount);
		Assert.Equal(0, diagnostic.PacketConstructionPlanCount);
		Assert.All(diagnostic.CandidateDiagnostics, candidate =>
		{
			Assert.Equal(PlayerKnownListPopulationPacketConstructionDiagnosticStatus.NoPacketConstructionMetadata, candidate.Status);
			Assert.Empty(candidate.FactPlans);
			Assert.Empty(candidate.PacketConstructionResults);
		});
	}

	private static PlayerKnownListPopulationPlanService CreatePopulationService() =>
		new(
			new PlayerKnownListVisibilityRangePlanService(),
			new PlayerKnownListTwoWayMembershipAdapterService(new PlayerKnownListMembershipService()),
			new PlayerKnownListOperationSideEffectAttachmentService());

	private static PlayerKnownListPopulationPlanRequest CreateRequest(
		IReadOnlyList<PlayerKnownListPopulationCandidateFact> candidateFacts,
		IReadOnlyList<int>? regionCandidateIds = null) =>
		new(
			CreateRegionSnapshot(regionCandidateIds ?? [NearPlayerObjectId, FarPlayerObjectId]),
			new PlayerKnownListVisibilityRangeObject(
				OwnerPlayerObjectId,
				WorldId: 210010000,
				InstanceId: 1,
				X: 0,
				Y: 0,
				Z: 0),
			candidateFacts);

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

	private const int OwnerPlayerObjectId = 9001;
	private const int NearPlayerObjectId = 9002;
	private const int FarPlayerObjectId = 9003;
	private const int MissingFactPlayerObjectId = 9004;
	private const int RideNpcId = 730001;
}
