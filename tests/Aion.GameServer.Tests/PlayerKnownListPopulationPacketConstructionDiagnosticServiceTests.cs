using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
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
		Assert.Equal(1, diagnostic.RideAttackSpeedFactSourceCountsByKind[PlayerKnownListPacketConstructionAttackSpeedFactSource.Supplied]);
		Assert.Equal(1, diagnostic.RideAttackSpeedFactSourceCountsByKind[PlayerKnownListPacketConstructionAttackSpeedFactSource.None]);
		Assert.Empty(diagnostic.RideAttackSpeedResolutionStatusCountsByKind);
		Assert.Equal(2, diagnostic.AbnormalEffectFactSourceCountsByKind[PlayerKnownListPacketConstructionAbnormalEffectFactSource.None]);
		Assert.Empty(diagnostic.AbnormalEffectResolutionStatusCountsByKind);
		Assert.Equal(2, diagnostic.PacketConstructionFactSourceCount);
		Assert.Equal(0, diagnostic.RequestPacketConstructionFactSourceCount);
		Assert.Equal(2, diagnostic.GeneratedPacketConstructionFactSourceCount);
		Assert.Equal(0, diagnostic.IgnoredGeneratedPacketConstructionFactSourceCount);
		Assert.Equal(2, diagnostic.PacketConstructionFactSourceCountsByKind[PlayerKnownListPopulationPacketConstructionFactSourceKind.GeneratedFactPlan]);
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
				PlayerKnownListPopulationPacketConstructionFactSourceKind.GeneratedFactPlan,
				PlayerKnownListPopulationPacketConstructionFactSourceKind.GeneratedFactPlan,
			],
			candidateDiagnostic.PacketConstructionFactSources.Select(source => source.Kind));
		Assert.Equal(
			[
				PlayerKnownListPopulationPacketConstructionFactPlanDirection.OwnerViewingCandidate,
				PlayerKnownListPopulationPacketConstructionFactPlanDirection.CandidateViewingOwner,
			],
			candidateDiagnostic.FactPlans.Select(factPlan => factPlan.Direction));
		Assert.Equal(PlayerKnownListPacketConstructionAttackSpeedFactSource.Supplied, candidateDiagnostic.FactPlans[0].RideAttackSpeedFactSource);
		Assert.Equal(PlayerKnownListPacketConstructionAttackSpeedFactSource.None, candidateDiagnostic.FactPlans[1].RideAttackSpeedFactSource);
		Assert.Equal(PlayerKnownListPacketConstructionAbnormalEffectFactSource.None, candidateDiagnostic.FactPlans[0].AbnormalEffectFactSource);
		Assert.Equal(PlayerKnownListPacketConstructionAbnormalEffectFactSource.None, candidateDiagnostic.FactPlans[1].AbnormalEffectFactSource);
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
		Assert.Equal(2, diagnostic.RideAttackSpeedFactSourceCountsByKind[PlayerKnownListPacketConstructionAttackSpeedFactSource.None]);
		Assert.Equal(2, diagnostic.AbnormalEffectFactSourceCountsByKind[PlayerKnownListPacketConstructionAbnormalEffectFactSource.None]);
		Assert.Equal(1, diagnostic.PacketConstructionFactSourceCount);
		Assert.Equal(0, diagnostic.RequestPacketConstructionFactSourceCount);
		Assert.Equal(1, diagnostic.GeneratedPacketConstructionFactSourceCount);
		Assert.Equal(0, diagnostic.IgnoredGeneratedPacketConstructionFactSourceCount);
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
	public void Summarize_AttackSpeedResolverMetadataCountsResolvedApproximation()
	{
		var population = CreatePopulationService();
		var diagnostics = new PlayerKnownListPopulationPacketConstructionDiagnosticService();
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
		var plan = population.Plan(CreateRequest(
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
			itemTemplates: CreateItemTemplates()));

		var diagnostic = diagnostics.Summarize(plan);

		Assert.Equal(1, diagnostic.RideAttackSpeedFactSourceCountsByKind[PlayerKnownListPacketConstructionAttackSpeedFactSource.ResolvedApproximation]);
		Assert.Equal(1, diagnostic.RideAttackSpeedResolutionStatusCountsByKind[PlayerKnownListAttackSpeedFactResolutionStatus.ResolvedApproximation]);
		var factPlan = Assert.Single(diagnostic.CandidateDiagnostics[0].FactPlans);
		Assert.Equal(PlayerKnownListPacketConstructionAttackSpeedFactSource.ResolvedApproximation, factPlan.RideAttackSpeedFactSource);
		Assert.Equal(PlayerKnownListAttackSpeedFactResolutionStatus.ResolvedApproximation, factPlan.RideAttackSpeedResolutionStatus);
		Assert.Equal(PlayerKnownListPacketConstructionAbnormalEffectFactSource.None, factPlan.AbnormalEffectFactSource);
		Assert.Null(factPlan.AbnormalEffectResolutionStatus);
	}

	[Fact]
	public void Summarize_AbnormalEffectResolverMetadataCountsResolvedSnapshot()
	{
		var population = CreatePopulationService();
		var diagnostics = new PlayerKnownListPopulationPacketConstructionDiagnosticService();
		var owner = CreatePlayer(OwnerPlayerObjectId, "Owner", "ELYOS");
		var candidate = CreatePlayer(NearPlayerObjectId, "Candidate", "ASMODIANS");
		candidate.AbnormalState = PlayerAbnormalState.Root;
		var ownerViewingCandidate = new PlayerKnownListOperationSideEffectDirectionFacts(
			SubjectHasAbnormalEffects: true);
		var resolvedEffects = new[]
		{
			CreateAbnormalEffect(skillId: 1201, remainingTime: -1),
		};
		var plan = population.Plan(CreateRequest(
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
						AbnormalEffectResolution: CreateResolvedAbnormalEffects(resolvedEffects, abnormalEffectMask: 0x20, slots: 1))),
			]));

		var diagnostic = diagnostics.Summarize(plan);

		Assert.Equal(1, diagnostic.AbnormalEffectFactSourceCountsByKind[PlayerKnownListPacketConstructionAbnormalEffectFactSource.ResolvedSnapshot]);
		Assert.Equal(1, diagnostic.AbnormalEffectResolutionStatusCountsByKind[PlayerKnownListAbnormalEffectFactResolutionStatus.ResolvedSnapshot]);
		var factPlan = Assert.Single(diagnostic.CandidateDiagnostics[0].FactPlans);
		Assert.Equal(PlayerKnownListPacketConstructionAbnormalEffectFactSource.ResolvedSnapshot, factPlan.AbnormalEffectFactSource);
		Assert.Equal(PlayerKnownListAbnormalEffectFactResolutionStatus.ResolvedSnapshot, factPlan.AbnormalEffectResolutionStatus);
		Assert.Equal(PlayerKnownListPacketConstructionAttackSpeedFactSource.None, factPlan.RideAttackSpeedFactSource);
		Assert.Null(factPlan.RideAttackSpeedResolutionStatus);
	}

	[Fact]
	public void Summarize_RequestFactsRemainAuthoritativeAndGeneratedOverrideIsReported()
	{
		var population = CreatePopulationService();
		var diagnostics = new PlayerKnownListPopulationPacketConstructionDiagnosticService();
		var owner = CreatePlayer(OwnerPlayerObjectId, "Owner", "ELYOS");
		var candidate = CreatePlayer(NearPlayerObjectId, "Candidate", "ASMODIANS");
		candidate.MountRide(new PlayerRideInfo(RideNpcId, StartFp: 0, CostFp: null, SprintSpeed: 9.5f, FlySpeed: 10.5f, MoveSpeed: 7.25f));
		var ownerViewingCandidate = new PlayerKnownListOperationSideEffectDirectionFacts(
			SubjectIsInRideMode: true,
			SubjectRideNpcId: RideNpcId);
		var plan = population.Plan(CreateRequest(
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
						RideAttackSpeedFacts: new PlayerKnownListPacketConstructionAttackSpeedFacts(1400, 1200))),
			],
			packetConstructionFactsByPlayerObjectId: new Dictionary<int, PlayerKnownListOperationSideEffectPacketConstructionFacts>
			{
				[NearPlayerObjectId] = CreatePacketFacts(NearPlayerObjectId, "ExplicitCandidate", rideMovementSpeed: 3.5f),
			}));

		var diagnostic = diagnostics.Summarize(plan);

		Assert.Equal(2, diagnostic.PacketConstructionFactSourceCount);
		Assert.Equal(1, diagnostic.RequestPacketConstructionFactSourceCount);
		Assert.Equal(0, diagnostic.GeneratedPacketConstructionFactSourceCount);
		Assert.Equal(1, diagnostic.IgnoredGeneratedPacketConstructionFactSourceCount);
		Assert.Equal(1, diagnostic.PacketConstructionFactSourceCountsByKind[PlayerKnownListPopulationPacketConstructionFactSourceKind.Request]);
		Assert.Equal(1, diagnostic.PacketConstructionFactSourceCountsByKind[PlayerKnownListPopulationPacketConstructionFactSourceKind.GeneratedFactPlanIgnoredByRequest]);
		var candidateDiagnostic = diagnostic.CandidateDiagnostics[0];
		Assert.Equal(
			[
				PlayerKnownListPopulationPacketConstructionFactSourceKind.Request,
				PlayerKnownListPopulationPacketConstructionFactSourceKind.GeneratedFactPlanIgnoredByRequest,
			],
			candidateDiagnostic.PacketConstructionFactSources.Select(source => source.Kind));
	}

	[Fact]
	public void Summarize_RequestFactsOnlyCountsSourcesConsumedByCandidateSideEffects()
	{
		var population = CreatePopulationService();
		var diagnostics = new PlayerKnownListPopulationPacketConstructionDiagnosticService();
		var ownerViewingCandidate = new PlayerKnownListOperationSideEffectDirectionFacts();
		var plan = population.Plan(CreateRequest(
			[
				new PlayerKnownListPopulationCandidateFact(
					NearPlayerObjectId,
					X: 10,
					Y: 0,
					Z: 0,
					OwnerCanSeeCandidate: true,
					CandidateCanSeeOwner: false,
					OwnerViewingCandidateSideEffectFacts: ownerViewingCandidate),
			],
			packetConstructionFactsByPlayerObjectId: new Dictionary<int, PlayerKnownListOperationSideEffectPacketConstructionFacts>
			{
				[NearPlayerObjectId] = CreatePacketFacts(NearPlayerObjectId, "Candidate"),
				[UnrelatedPacketFactPlayerObjectId] = CreatePacketFacts(UnrelatedPacketFactPlayerObjectId, "Unrelated"),
			}));

		var diagnostic = diagnostics.Summarize(plan);

		Assert.Equal(1, diagnostic.PacketConstructionFactSourceCount);
		Assert.Equal(1, diagnostic.RequestPacketConstructionFactSourceCount);
		var candidateDiagnostic = diagnostic.CandidateDiagnostics[0];
		var source = Assert.Single(candidateDiagnostic.PacketConstructionFactSources);
		Assert.Equal(NearPlayerObjectId, source.SubjectPlayerObjectId);
		Assert.Equal(PlayerKnownListPopulationPacketConstructionFactSourceKind.Request, source.Kind);
		Assert.DoesNotContain(
			diagnostic.CandidateDiagnostics.SelectMany(candidate => candidate.PacketConstructionFactSources),
			source => source.SubjectPlayerObjectId == UnrelatedPacketFactPlayerObjectId);
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
			Assert.Empty(candidate.PacketConstructionFactSources);
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
		IReadOnlyList<int>? regionCandidateIds = null,
		IReadOnlyDictionary<int, PlayerKnownListOperationSideEffectPacketConstructionFacts>? packetConstructionFactsByPlayerObjectId = null,
		ItemTemplateTable? itemTemplates = null) =>
		new(
			CreateRegionSnapshot(regionCandidateIds ?? [NearPlayerObjectId, FarPlayerObjectId]),
			new PlayerKnownListVisibilityRangeObject(
				OwnerPlayerObjectId,
				WorldId: 210010000,
				InstanceId: 1,
				X: 0,
				Y: 0,
				Z: 0),
			candidateFacts,
			PacketConstructionFactsByPlayerObjectId: packetConstructionFactsByPlayerObjectId,
			ItemTemplates: itemTemplates);

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

	private static PlayerKnownListOperationSideEffectPacketConstructionFacts CreatePacketFacts(
		int objectId,
		string name,
		float rideMovementSpeed = 0) =>
		new(
			CreatePlayer(objectId, name, "ELYOS"),
			ActiveMotions: [],
			RideMovementSpeed: rideMovementSpeed);

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

	private static PlayerKnownListAbnormalEffectFactResolution CreateResolvedAbnormalEffects(
		IReadOnlyList<SmAbnormalEffectEntry> effects,
		int abnormalEffectMask,
		int slots) =>
		new(
			PlayerKnownListAbnormalEffectFactResolutionStatus.ResolvedSnapshot,
			new PlayerKnownListAbnormalEffectFacts(effects, abnormalEffectMask, slots),
			NeedsJavaEffectControllerParity: true,
			IsLive: false,
			IsJavaEffectControllerParity: false,
			"com.aionemu.gameserver.controllers.effect.EffectController.getAbnormalEffects",
			"Resolved from supplied abnormal-effect snapshot.");

	private static SmAbnormalEffectEntry CreateAbnormalEffect(
		int skillId,
		int remainingTime) =>
		new(
			EffectorObjectId: 7001,
			skillId,
			SkillLevel: 3,
			TargetSlotId: 1,
			TargetSlotOrdinal: 0,
			remainingTime);

	private const int OwnerPlayerObjectId = 9001;
	private const int NearPlayerObjectId = 9002;
	private const int FarPlayerObjectId = 9003;
	private const int MissingFactPlayerObjectId = 9004;
	private const int UnrelatedPacketFactPlayerObjectId = 9005;
	private const int RideNpcId = 730001;
	private const int MainHandSwordId = 100000001;
	private const long MainHandSlot = 1L;
}
