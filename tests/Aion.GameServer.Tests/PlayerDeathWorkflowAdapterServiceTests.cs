using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerDeathWorkflowAdapterServiceTests
{
	private readonly PlayerDeathWorkflowAdapterService _service = new();

	[Fact]
	public void Apply_DisabledExposesWorkflowPlanWithoutMutatingPlayer()
	{
		var player = CreateFlyingPlayer();
		var facts = new PlayerDeathWorkflowFacts(HasSummon: true, LastAttackerMasterIsNpcOrPlayerSelf: true, PlayerLevel: 10);

		var result = _service.Apply(new PlayerDeathWorkflowAdapterRequest(player, facts));

		Assert.Equal(PlayerDeathWorkflowAdapterStatus.DisabledPlanned, result.Status);
		Assert.Equal(PlayerDeathWorkflowStatus.PlannedFullPlayerDeath, result.Plan.Status);
		Assert.NotNull(result.Plan.ResurrectionOptionsPlan);
		Assert.NotNull(result.Plan.CoreSideEffectPlan);
		Assert.NotNull(result.Plan.DeathEmotionFanoutPlan);
		Assert.Null(result.StateTransitionResult);
		Assert.False(result.MutatedPlayerState);
		Assert.False(result.SentPackets);
		Assert.False(result.ScheduledTasks);
		Assert.False(result.ExecutedExternalCallbacks);
		Assert.True(result.ExposesPlanForObservation);
		Assert.False(result.IsLive);
		Assert.True(player.IsInState(PlayerCreatureState.Flying));
		Assert.False(player.IsFlyingBeforeDeath);
	}

	[Fact]
	public void Apply_LiveStateOnlyMutationAppliesDeathTransitionAndLeavesSideEffectsPlanned()
	{
		var player = CreateFlyingPlayer();
		var facts = new PlayerDeathWorkflowFacts(
			HasSummon: true,
			LastAttackerMasterIsNpcOrPlayerSelf: true,
			PlayerLevel: 10,
			LastAttackerObjectId: LastAttackerObjectId,
			KnownCreatureObjectIds: new[] { KnownCreatureObjectId });

		var result = _service.Apply(new PlayerDeathWorkflowAdapterRequest(
			player,
			facts,
			ExecuteLiveStateMutation: true));

		Assert.Equal(PlayerDeathWorkflowAdapterStatus.LiveStateTransitionApplied, result.Status);
		Assert.NotNull(result.StateTransitionResult);
		Assert.True(result.MutatedPlayerState);
		Assert.False(result.SentPackets);
		Assert.False(result.ScheduledTasks);
		Assert.False(result.ExecutedExternalCallbacks);
		Assert.True(result.IsLive);
		Assert.Equal(PlayerDeathStateTransitionStatus.FloatingCorpseApplied, result.StateTransitionResult.Status);
		Assert.True(player.IsFlyingBeforeDeath);
		Assert.False(player.IsInState(PlayerCreatureState.Flying));
		Assert.True(player.IsInState(PlayerCreatureState.FloatingCorpse));
		Assert.Contains(PlayerDeathWorkflowStep.ReleaseSummon, result.Plan.Steps);
		Assert.Contains(PlayerDeathWorkflowStep.ScheduleShowResurrectionOptions, result.Plan.Steps);
		Assert.NotNull(result.Plan.ResurrectionOptionsPlan);
		Assert.Equal(PlayerDeathResurrectionOptionsPlanStatus.SendSmDie, result.Plan.ResurrectionOptionsPlan.Status);
		Assert.NotNull(result.Plan.CoreSideEffectPlan);
		Assert.False(result.Plan.CoreSideEffectPlan.MutatedMovement);
		Assert.False(result.Plan.CoreSideEffectPlan.MutatedCasting);
		Assert.False(result.Plan.CoreSideEffectPlan.MutatedEffects);
		Assert.NotNull(result.Plan.DeathEmotionFanoutPlan);
		Assert.Equal(LastAttackerObjectId, result.Plan.DeathEmotionFanoutPlan.EmotionTargetObjectId);
		Assert.Equal(KnownCreatureObjectId, Assert.Single(result.Plan.DeathEmotionFanoutPlan.KnownCreatureAggroCleanupIntents).CreatureObjectId);
		Assert.True(result.Plan.WouldCalculateExperienceLoss);
		Assert.Contains("state transition applied", result.JavaSource);
	}

	[Fact]
	public void Apply_DuelOpponentEarlyReturnDoesNotApplyStateTransition()
	{
		var player = CreateFlyingPlayer();
		var facts = new PlayerDeathWorkflowFacts(
			IsDueling: true,
			KilledByDuelOpponent: true,
			HasSummon: true,
			LastAttackerMasterIsNpcOrPlayerSelf: true,
			PlayerLevel: 10);

		var result = _service.Apply(new PlayerDeathWorkflowAdapterRequest(
			player,
			facts,
			ExecuteLiveStateMutation: true));

		Assert.Equal(PlayerDeathWorkflowAdapterStatus.EarlyReturnPlanned, result.Status);
		Assert.Equal(PlayerDeathWorkflowStatus.ReturnedAfterDuelOpponentKill, result.Plan.Status);
		Assert.Null(result.StateTransitionResult);
		Assert.Null(result.Plan.ResurrectionOptionsPlan);
		Assert.Null(result.Plan.CoreSideEffectPlan);
		Assert.Null(result.Plan.DeathEmotionFanoutPlan);
		Assert.False(result.MutatedPlayerState);
		Assert.True(result.IsLive);
		Assert.True(player.IsInState(PlayerCreatureState.Flying));
		Assert.False(player.IsFlyingBeforeDeath);
		Assert.DoesNotContain(PlayerDeathWorkflowStep.ApplyPlayerDeathStateTransition, result.Plan.Steps);
	}

	[Fact]
	public void Apply_InstanceHandlerEarlyReturnStillAppliesStateTransitionBecauseJavaReturnsAfterSuperOnDie()
	{
		var player = CreateFlyingPlayer();
		var facts = new PlayerDeathWorkflowFacts(InstanceHandlerConsumesDeath: true);

		var result = _service.Apply(new PlayerDeathWorkflowAdapterRequest(
			player,
			facts,
			ExecuteLiveStateMutation: true));

		Assert.Equal(PlayerDeathWorkflowAdapterStatus.LiveStateTransitionApplied, result.Status);
		Assert.Equal(PlayerDeathWorkflowStatus.ReturnedAfterInstanceHandler, result.Plan.Status);
		Assert.NotNull(result.StateTransitionResult);
		Assert.NotNull(result.Plan.ResurrectionOptionsPlan);
		Assert.NotNull(result.Plan.CoreSideEffectPlan);
		Assert.NotNull(result.Plan.DeathEmotionFanoutPlan);
		Assert.True(result.MutatedPlayerState);
		Assert.True(player.IsInState(PlayerCreatureState.FloatingCorpse));
		Assert.Contains(PlayerDeathWorkflowStep.ReturnAfterInstanceHandler, result.Plan.Steps);
		Assert.False(result.ExecutedExternalCallbacks);
	}

	private static Player CreateFlyingPlayer()
	{
		return new Player
		{
			ObjectId = PlayerObjectId,
			CreatureState = PlayerCreatureState.Active | PlayerCreatureState.Flying | PlayerCreatureState.Resting,
			FlyState = PlayerFlyState.Flying,
			LifeStats = new PlayerLifeStats(CurrentHp: 0, CurrentMp: 0, CurrentFp: 0),
			IsInRideMode = true,
			RideInfo = new PlayerRideInfo(1, 2, 3, 4, 5, 6),
		};
	}

	private const int PlayerObjectId = 1001;
	private const int LastAttackerObjectId = 2002;
	private const int KnownCreatureObjectId = 3003;
}
