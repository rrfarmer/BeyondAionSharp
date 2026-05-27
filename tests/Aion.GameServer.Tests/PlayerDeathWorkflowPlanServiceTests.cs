using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerDeathWorkflowPlanServiceTests
{
	private readonly PlayerDeathWorkflowPlanService _service = new();

	[Fact]
	public void CreatePlan_FlyingPlayerOrdersJavaSideEffectsAroundStateTransition()
	{
		var player = new Player
		{
			ObjectId = PlayerObjectId,
			CreatureState = PlayerCreatureState.Active | PlayerCreatureState.Flying,
			FlyState = PlayerFlyState.Flying,
			LifeStats = new PlayerLifeStats(CurrentHp: 0, CurrentMp: 0, CurrentFp: 0),
		};
		var facts = new PlayerDeathWorkflowFacts(
			HasSummon: true,
			LastAttackerMasterIsNpcOrPlayerSelf: true,
			PlayerLevel: 10,
			LastAttackerObjectId: LastAttackerObjectId,
			KnownCreatureObjectIds: new[] { KnownCreatureOneObjectId, KnownCreatureTwoObjectId });

		var plan = _service.CreatePlan(player, facts);

		Assert.Equal(PlayerDeathWorkflowStatus.PlannedFullPlayerDeath, plan.Status);
		Assert.False(plan.IsLive);
		Assert.True(plan.WouldSetFlyingBeforeDeath);
		Assert.True(plan.WouldUseFloatingCorpse);
		var playerControllerPhase = Assert.Single(plan.StatePhasePlans, phase => phase.Phase == PlayerDeathStateTransitionPhase.PlayerControllerPreSuperCleanup);
		var creatureControllerPhase = Assert.Single(plan.StatePhasePlans, phase => phase.Phase == PlayerDeathStateTransitionPhase.CreatureControllerDeathStateSelection);
		Assert.Contains(PlayerDeathStateTransitionStep.SetFlyingBeforeDeathFlag, playerControllerPhase.Steps);
		Assert.Equal(
			new[] { PlayerDeathStateTransitionStep.ClearActiveState, PlayerDeathStateTransitionStep.SetFloatingCorpseState },
			creatureControllerPhase.Steps);
		Assert.True(plan.WouldReleaseSummon);
		Assert.True(plan.WouldCalculateExperienceLoss);
		Assert.True(plan.WouldScheduleResurrectionOptions);
		Assert.NotNull(plan.ResurrectionOptionsPlan);
		Assert.Equal(PlayerDeathResurrectionOptionsPlanStatus.SendSmDie, plan.ResurrectionOptionsPlan.Status);
		Assert.NotNull(plan.CoreSideEffectPlan);
		AssertOrdered(
			plan.Steps,
			PlayerDeathWorkflowStep.AbortMove,
			PlayerDeathWorkflowStep.ClearCasting,
			PlayerDeathWorkflowStep.RemoveAllEffects,
			PlayerDeathWorkflowStep.NotifyDeathObservers);
		Assert.NotNull(plan.DeathEmotionFanoutPlan);
		Assert.Equal(LastAttackerObjectId, plan.DeathEmotionFanoutPlan.EmotionTargetObjectId);
		Assert.Equal(
			new[] { KnownCreatureOneObjectId, KnownCreatureTwoObjectId },
			plan.DeathEmotionFanoutPlan.KnownCreatureAggroCleanupIntents.Select(intent => intent.CreatureObjectId));
		Assert.Equal(PlayerDeathStateTransitionStatus.FloatingCorpseApplied, plan.PlannedTransitionStatus);
		AssertOrdered(
			plan.Steps,
			PlayerDeathWorkflowStep.CancelCurrentSkill,
			PlayerDeathWorkflowStep.SetRebirthReviveInfo,
			PlayerDeathWorkflowStep.ResolveLastAttackerMaster,
			PlayerDeathWorkflowStep.CheckDuelState,
			PlayerDeathWorkflowStep.ReleaseSummon,
			PlayerDeathWorkflowStep.CheckFlyingBeforeDeath,
			PlayerDeathWorkflowStep.ApplyPlayerDeathStateTransition,
			PlayerDeathWorkflowStep.AbortMove,
			PlayerDeathWorkflowStep.ClearCasting,
			PlayerDeathWorkflowStep.RemoveAllEffects,
			PlayerDeathWorkflowStep.NotifyDeathObservers,
			PlayerDeathWorkflowStep.BroadcastDieEmotion,
			PlayerDeathWorkflowStep.StopKnownCreaturesHatingPlayer,
			PlayerDeathWorkflowStep.ScheduleShowResurrectionOptions,
			PlayerDeathWorkflowStep.InvokeInstanceHandlerOnDie,
			PlayerDeathWorkflowStep.InvokeMapRegionOnDie,
			PlayerDeathWorkflowStep.DoReward,
			PlayerDeathWorkflowStep.CalculateExperienceLoss,
			PlayerDeathWorkflowStep.NotifyQuestEngineOnDie);
		Assert.Contains(plan.UnsupportedJavaBehaviors, gap => gap.Contains("SM_DIE"));
		Assert.Contains("PlayerController.onDie", plan.JavaSource);
	}

	[Fact]
	public void CreatePlan_DuelOpponentKillReturnsBeforeSummonAndSuperOnDieLikeJava()
	{
		var player = new Player
		{
			ObjectId = PlayerObjectId,
			CreatureState = PlayerCreatureState.Active | PlayerCreatureState.Flying,
		};
		var facts = new PlayerDeathWorkflowFacts(
			IsDueling: true,
			KilledByDuelOpponent: true,
			HasSummon: true,
			PlayerLevel: 10,
			LastAttackerMasterIsNpcOrPlayerSelf: true);

		var plan = _service.CreatePlan(player, facts);

		Assert.Equal(PlayerDeathWorkflowStatus.ReturnedAfterDuelOpponentKill, plan.Status);
		Assert.False(plan.WouldReleaseSummon);
		Assert.False(plan.WouldScheduleResurrectionOptions);
		Assert.Empty(plan.StatePhasePlans);
		Assert.Null(plan.ResurrectionOptionsPlan);
		Assert.Null(plan.CoreSideEffectPlan);
		Assert.Null(plan.DeathEmotionFanoutPlan);
		Assert.False(plan.WouldCalculateExperienceLoss);
		AssertOrdered(
			plan.Steps,
			PlayerDeathWorkflowStep.CancelCurrentSkill,
			PlayerDeathWorkflowStep.SetRebirthReviveInfo,
			PlayerDeathWorkflowStep.ResolveLastAttackerMaster,
			PlayerDeathWorkflowStep.CheckDuelState,
			PlayerDeathWorkflowStep.LoseDuel,
			PlayerDeathWorkflowStep.RestoreDuelistHitPointsAndMana,
			PlayerDeathWorkflowStep.ReturnAfterDuelOpponentKill);
		Assert.DoesNotContain(PlayerDeathWorkflowStep.ReleaseSummon, plan.Steps);
		Assert.DoesNotContain(PlayerDeathWorkflowStep.ApplyPlayerDeathStateTransition, plan.Steps);
		Assert.DoesNotContain(PlayerDeathWorkflowStep.ScheduleShowResurrectionOptions, plan.Steps);
	}

	[Fact]
	public void CreatePlan_InstanceHandlerReturnStopsBeforeMapRewardAndQuest()
	{
		var player = new Player
		{
			ObjectId = PlayerObjectId,
			CreatureState = PlayerCreatureState.Active,
			LifeStats = new PlayerLifeStats(CurrentHp: 0, CurrentMp: 0, CurrentFp: 0),
		};
		var facts = new PlayerDeathWorkflowFacts(
			InstanceHandlerConsumesDeath: true,
			LastAttackerMasterIsNpcOrPlayerSelf: true,
			PlayerLevel: 10,
			LastAttackerObjectId: PlayerObjectId);

		var plan = _service.CreatePlan(player, facts);

		Assert.Equal(PlayerDeathWorkflowStatus.ReturnedAfterInstanceHandler, plan.Status);
		Assert.NotEmpty(plan.StatePhasePlans);
		Assert.True(plan.WouldScheduleResurrectionOptions);
		Assert.NotNull(plan.ResurrectionOptionsPlan);
		Assert.Equal(PlayerDeathResurrectionOptionsPlanStatus.SendSmDie, plan.ResurrectionOptionsPlan.Status);
		Assert.NotNull(plan.CoreSideEffectPlan);
		Assert.NotNull(plan.DeathEmotionFanoutPlan);
		Assert.Equal(0, plan.DeathEmotionFanoutPlan.EmotionTargetObjectId);
		Assert.False(plan.WouldCalculateExperienceLoss);
		AssertOrdered(
			plan.Steps,
			PlayerDeathWorkflowStep.ApplyPlayerDeathStateTransition,
			PlayerDeathWorkflowStep.ScheduleShowResurrectionOptions,
			PlayerDeathWorkflowStep.InvokeInstanceHandlerOnDie,
			PlayerDeathWorkflowStep.ReturnAfterInstanceHandler);
		Assert.DoesNotContain(PlayerDeathWorkflowStep.InvokeMapRegionOnDie, plan.Steps);
		Assert.DoesNotContain(PlayerDeathWorkflowStep.DoReward, plan.Steps);
		Assert.DoesNotContain(PlayerDeathWorkflowStep.NotifyQuestEngineOnDie, plan.Steps);
	}

	[Fact]
	public void CreatePlan_MapRegionReturnStopsBeforeRewardAndQuest()
	{
		var player = new Player
		{
			ObjectId = PlayerObjectId,
			CreatureState = PlayerCreatureState.Active,
			LifeStats = new PlayerLifeStats(CurrentHp: 0, CurrentMp: 0, CurrentFp: 0),
		};
		var facts = new PlayerDeathWorkflowFacts(
			MapRegionConsumesDeath: true,
			HasTeleportTaskAtResurrectionOptionsCallback: true,
			LastAttackerMasterIsNpcOrPlayerSelf: true,
			PlayerLevel: 10,
			LastAttackerObjectId: LastAttackerObjectId);

		var plan = _service.CreatePlan(player, facts);

		Assert.Equal(PlayerDeathWorkflowStatus.ReturnedAfterMapRegion, plan.Status);
		Assert.NotEmpty(plan.StatePhasePlans);
		Assert.True(plan.WouldScheduleResurrectionOptions);
		Assert.NotNull(plan.ResurrectionOptionsPlan);
		Assert.Equal(PlayerDeathResurrectionOptionsPlanStatus.SkipTeleportTask, plan.ResurrectionOptionsPlan.Status);
		Assert.NotNull(plan.CoreSideEffectPlan);
		Assert.NotNull(plan.DeathEmotionFanoutPlan);
		Assert.False(plan.WouldCalculateExperienceLoss);
		AssertOrdered(
			plan.Steps,
			PlayerDeathWorkflowStep.InvokeInstanceHandlerOnDie,
			PlayerDeathWorkflowStep.InvokeMapRegionOnDie,
			PlayerDeathWorkflowStep.ReturnAfterMapRegion);
		Assert.DoesNotContain(PlayerDeathWorkflowStep.DoReward, plan.Steps);
		Assert.DoesNotContain(PlayerDeathWorkflowStep.CalculateExperienceLoss, plan.Steps);
		Assert.DoesNotContain(PlayerDeathWorkflowStep.NotifyQuestEngineOnDie, plan.Steps);
	}

	private static void AssertOrdered(IReadOnlyList<PlayerDeathWorkflowStep> actual, params PlayerDeathWorkflowStep[] expected)
	{
		var previousIndex = -1;
		foreach (var step in expected)
		{
			var currentIndex = Array.IndexOf(actual.ToArray(), step);
			Assert.True(currentIndex > previousIndex, $"Expected {step} after index {previousIndex}, actual order: {string.Join(", ", actual)}");
			previousIndex = currentIndex;
		}
	}

	private const int PlayerObjectId = 1001;
	private const int LastAttackerObjectId = 2002;
	private const int KnownCreatureOneObjectId = 3003;
	private const int KnownCreatureTwoObjectId = 3004;
}
