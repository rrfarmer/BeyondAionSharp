using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerProtectionAttackUtilRecipientPlannerServiceTests
{
	[Fact]
	public void CreatePlan_ProjectsEligibleCastCancellationCreatures()
	{
		var plan = PlayerProtectionAttackUtilRecipientPlannerService.CreatePlan(
			ProtectedPlayerObjectId,
			[
				new PlayerProtectionAttackUtilKnownObjectFact(
					KnownObjectId: 2001,
					PlayerProtectionAttackUtilKnownObjectKind.Creature,
					TargetObjectId: ProtectedPlayerObjectId,
					IsCasting: true,
					CastingSkillFirstTargetObjectId: ProtectedPlayerObjectId),
				new PlayerProtectionAttackUtilKnownObjectFact(
					KnownObjectId: 2002,
					PlayerProtectionAttackUtilKnownObjectKind.Player,
					TargetObjectId: ProtectedPlayerObjectId,
					IsCasting: true,
					CastingSkillFirstTargetObjectId: ProtectedPlayerObjectId),
			]);

		Assert.True(plan.UsesKnownListForEachObject);
		Assert.False(plan.IsLive);
		Assert.Equal([2001, 2002], plan.CastCancellationObjectIds);
		Assert.All(plan.CastCancellationProjections.Where(projection => projection.WouldCancelCast), projection =>
		{
			Assert.Equal(PlayerProtectionAttackUtilCandidateStatus.Eligible, projection.Status);
			Assert.Contains("cancelCurrentSkill", projection.Notes);
			Assert.False(projection.IsLive);
		});
	}

	[Fact]
	public void CreatePlan_SkipsIneligibleCastCancellationCandidates()
	{
		var plan = PlayerProtectionAttackUtilRecipientPlannerService.CreatePlan(
			ProtectedPlayerObjectId,
			[
				new PlayerProtectionAttackUtilKnownObjectFact(2001, PlayerProtectionAttackUtilKnownObjectKind.Other, ProtectedPlayerObjectId, IsCasting: true, ProtectedPlayerObjectId),
				new PlayerProtectionAttackUtilKnownObjectFact(2002, PlayerProtectionAttackUtilKnownObjectKind.Creature, TargetObjectId: 9999, IsCasting: true, ProtectedPlayerObjectId),
				new PlayerProtectionAttackUtilKnownObjectFact(2003, PlayerProtectionAttackUtilKnownObjectKind.Creature, ProtectedPlayerObjectId, IsCasting: false, CastingSkillFirstTargetObjectId: null),
				new PlayerProtectionAttackUtilKnownObjectFact(2004, PlayerProtectionAttackUtilKnownObjectKind.Creature, ProtectedPlayerObjectId, IsCasting: true, CastingSkillFirstTargetObjectId: 9999),
			]);

		Assert.Empty(plan.CastCancellationObjectIds);
		Assert.Equal(
			[
				PlayerProtectionAttackUtilCandidateStatus.SkippedNotCreature,
				PlayerProtectionAttackUtilCandidateStatus.SkippedTargetMismatch,
				PlayerProtectionAttackUtilCandidateStatus.SkippedNotCasting,
				PlayerProtectionAttackUtilCandidateStatus.SkippedCastingFirstTargetMismatch,
			],
			plan.CastCancellationProjections.Select(projection => projection.Status));
	}

	[Fact]
	public void CreatePlan_ProjectsTargetClearPlayersWithoutValidateSeeFilter()
	{
		var plan = PlayerProtectionAttackUtilRecipientPlannerService.CreatePlan(
			ProtectedPlayerObjectId,
			[
				new PlayerProtectionAttackUtilKnownObjectFact(
					KnownObjectId: 3001,
					PlayerProtectionAttackUtilKnownObjectKind.Player,
					TargetObjectId: ProtectedPlayerObjectId,
					IsCasting: false,
					CastingSkillFirstTargetObjectId: null,
					CanSeeProtectedTarget: true),
				new PlayerProtectionAttackUtilKnownObjectFact(
					KnownObjectId: 3002,
					PlayerProtectionAttackUtilKnownObjectKind.Player,
					TargetObjectId: ProtectedPlayerObjectId,
					IsCasting: false,
					CastingSkillFirstTargetObjectId: null,
					CanSeeProtectedTarget: false),
			],
			validateSeeForTargetRemoval: false);

		Assert.True(plan.UsesKnownListForEachPlayer);
		Assert.False(plan.ValidateSeeForTargetRemoval);
		Assert.Equal([3001, 3002], plan.TargetClearPlayerObjectIds);
		Assert.All(plan.TargetClearProjections.Where(projection => projection.WouldClearTarget), projection =>
		{
			Assert.Equal(PlayerProtectionAttackUtilCandidateStatus.Eligible, projection.Status);
			Assert.Contains("validateSee=false", projection.Notes);
		});
	}

	[Fact]
	public void CreatePlan_AppliesValidateSeeOnlyWhenRequested()
	{
		var plan = PlayerProtectionAttackUtilRecipientPlannerService.CreatePlan(
			ProtectedPlayerObjectId,
			[
				new PlayerProtectionAttackUtilKnownObjectFact(3001, PlayerProtectionAttackUtilKnownObjectKind.Player, ProtectedPlayerObjectId, IsCasting: false, CastingSkillFirstTargetObjectId: null, CanSeeProtectedTarget: true),
				new PlayerProtectionAttackUtilKnownObjectFact(3002, PlayerProtectionAttackUtilKnownObjectKind.Player, ProtectedPlayerObjectId, IsCasting: false, CastingSkillFirstTargetObjectId: null, CanSeeProtectedTarget: false),
				new PlayerProtectionAttackUtilKnownObjectFact(3003, PlayerProtectionAttackUtilKnownObjectKind.Creature, ProtectedPlayerObjectId, IsCasting: false, CastingSkillFirstTargetObjectId: null),
				new PlayerProtectionAttackUtilKnownObjectFact(3004, PlayerProtectionAttackUtilKnownObjectKind.Player, TargetObjectId: 9999, IsCasting: false, CastingSkillFirstTargetObjectId: null),
			],
			validateSeeForTargetRemoval: true);

		Assert.True(plan.ValidateSeeForTargetRemoval);
		Assert.Equal([3002], plan.TargetClearPlayerObjectIds);
		Assert.Equal(
			[
				PlayerProtectionAttackUtilCandidateStatus.SkippedCanSeeWhenValidateSee,
				PlayerProtectionAttackUtilCandidateStatus.Eligible,
				PlayerProtectionAttackUtilCandidateStatus.SkippedNotPlayer,
				PlayerProtectionAttackUtilCandidateStatus.SkippedTargetMismatch,
			],
			plan.TargetClearProjections.Select(projection => projection.Status));
	}

	[Fact]
	public void CreatePlan_CollapsesDuplicateKnownObjectIdsUsingLastFact()
	{
		var plan = PlayerProtectionAttackUtilRecipientPlannerService.CreatePlan(
			ProtectedPlayerObjectId,
			[
				new PlayerProtectionAttackUtilKnownObjectFact(4001, PlayerProtectionAttackUtilKnownObjectKind.Player, TargetObjectId: 9999, IsCasting: false, CastingSkillFirstTargetObjectId: null),
				new PlayerProtectionAttackUtilKnownObjectFact(4001, PlayerProtectionAttackUtilKnownObjectKind.Player, ProtectedPlayerObjectId, IsCasting: false, CastingSkillFirstTargetObjectId: null),
			]);

		Assert.True(plan.DuplicateKnownObjectIdsCollapsed);
		Assert.Single(plan.TargetClearProjections);
		Assert.Equal([4001], plan.TargetClearPlayerObjectIds);
	}

	[Fact]
	public void CreatePlan_EmptyKnownListInputProducesNoRecipients()
	{
		var plan = PlayerProtectionAttackUtilRecipientPlannerService.CreatePlan(
			ProtectedPlayerObjectId,
			knownObjectFacts: null);

		Assert.Empty(plan.CastCancellationProjections);
		Assert.Empty(plan.TargetClearProjections);
		Assert.Empty(plan.CastCancellationObjectIds);
		Assert.Empty(plan.TargetClearPlayerObjectIds);
		Assert.Contains("AttackUtil.cancelCastOn", plan.JavaSource);
		Assert.False(plan.IsLive);
	}

	private const int ProtectedPlayerObjectId = 1001;
}
