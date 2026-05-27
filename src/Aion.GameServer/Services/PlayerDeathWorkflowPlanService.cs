using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public enum PlayerDeathWorkflowStep
{
	CancelCurrentSkill,
	SetRebirthReviveInfo,
	ResolveLastAttackerMaster,
	CheckDuelState,
	LoseDuel,
	RestoreDuelistHitPointsAndMana,
	ReturnAfterDuelOpponentKill,
	ReleaseSummon,
	CheckFlyingBeforeDeath,
	ApplyPlayerDeathStateTransition,
	AbortMove,
	ClearCasting,
	RemoveAllEffects,
	NotifyDeathObservers,
	BroadcastDieEmotion,
	StopKnownCreaturesHatingPlayer,
	ScheduleShowResurrectionOptions,
	InvokeInstanceHandlerOnDie,
	ReturnAfterInstanceHandler,
	InvokeMapRegionOnDie,
	ReturnAfterMapRegion,
	DoReward,
	CalculateExperienceLoss,
	NotifyQuestEngineOnDie,
}

public enum PlayerDeathWorkflowStatus
{
	PlannedFullPlayerDeath,
	ReturnedAfterDuelOpponentKill,
	ReturnedAfterInstanceHandler,
	ReturnedAfterMapRegion,
}

public sealed record PlayerDeathWorkflowFacts(
	bool IsDueling = false,
	bool KilledByDuelOpponent = false,
	bool HasSummon = false,
	bool InstanceHandlerConsumesDeath = false,
	bool MapRegionConsumesDeath = false,
	bool LastAttackerMasterIsNpcOrPlayerSelf = false,
	int PlayerLevel = 1,
	bool HasNoDeathPenaltyEffect = false,
	bool HasTeleportTaskAtResurrectionOptionsCallback = false);

public sealed record PlayerDeathWorkflowPlan(
	PlayerDeathWorkflowStatus Status,
	int PlayerObjectId,
	PlayerDeathStateTransitionStatus PlannedTransitionStatus,
	bool WouldSetFlyingBeforeDeath,
	bool WouldUseFloatingCorpse,
	bool WouldReleaseSummon,
	bool WouldCalculateExperienceLoss,
	bool WouldScheduleResurrectionOptions,
	PlayerDeathResurrectionOptionsPlan? ResurrectionOptionsPlan,
	bool IsLive,
	IReadOnlyList<PlayerDeathWorkflowStep> Steps,
	IReadOnlyList<string> UnsupportedJavaBehaviors,
	string JavaSource);

public sealed class PlayerDeathWorkflowPlanService
{
	public PlayerDeathWorkflowPlan CreatePlan(Player player, PlayerDeathWorkflowFacts facts)
	{
		// Java parity breadcrumb:
		// PlayerController.onDie performs player branches, then calls CreatureController.onDie,
		// then resumes with resurrection, instance/map, reward, XP-loss, and quest callbacks.
		var steps = new List<PlayerDeathWorkflowStep>
		{
			PlayerDeathWorkflowStep.CancelCurrentSkill,
			PlayerDeathWorkflowStep.SetRebirthReviveInfo,
			PlayerDeathWorkflowStep.ResolveLastAttackerMaster,
			PlayerDeathWorkflowStep.CheckDuelState,
		};

		if (facts.IsDueling)
		{
			steps.Add(PlayerDeathWorkflowStep.LoseDuel);
			if (facts.KilledByDuelOpponent)
			{
				steps.Add(PlayerDeathWorkflowStep.RestoreDuelistHitPointsAndMana);
				steps.Add(PlayerDeathWorkflowStep.ReturnAfterDuelOpponentKill);
				return BuildPlan(
					player,
					facts,
					steps,
					PlayerDeathWorkflowStatus.ReturnedAfterDuelOpponentKill,
					wouldScheduleResurrectionOptions: false);
			}
		}

		if (facts.HasSummon)
		{
			steps.Add(PlayerDeathWorkflowStep.ReleaseSummon);
		}

		steps.Add(PlayerDeathWorkflowStep.CheckFlyingBeforeDeath);
		steps.Add(PlayerDeathWorkflowStep.ApplyPlayerDeathStateTransition);
		steps.Add(PlayerDeathWorkflowStep.AbortMove);
		steps.Add(PlayerDeathWorkflowStep.ClearCasting);
		steps.Add(PlayerDeathWorkflowStep.RemoveAllEffects);
		steps.Add(PlayerDeathWorkflowStep.NotifyDeathObservers);
		steps.Add(PlayerDeathWorkflowStep.BroadcastDieEmotion);
		steps.Add(PlayerDeathWorkflowStep.StopKnownCreaturesHatingPlayer);
		steps.Add(PlayerDeathWorkflowStep.ScheduleShowResurrectionOptions);
		steps.Add(PlayerDeathWorkflowStep.InvokeInstanceHandlerOnDie);

		if (facts.InstanceHandlerConsumesDeath)
		{
			steps.Add(PlayerDeathWorkflowStep.ReturnAfterInstanceHandler);
			return BuildPlan(
				player,
				facts,
				steps,
				PlayerDeathWorkflowStatus.ReturnedAfterInstanceHandler,
				wouldScheduleResurrectionOptions: true);
		}

		steps.Add(PlayerDeathWorkflowStep.InvokeMapRegionOnDie);
		if (facts.MapRegionConsumesDeath)
		{
			steps.Add(PlayerDeathWorkflowStep.ReturnAfterMapRegion);
			return BuildPlan(
				player,
				facts,
				steps,
				PlayerDeathWorkflowStatus.ReturnedAfterMapRegion,
				wouldScheduleResurrectionOptions: true);
		}

		steps.Add(PlayerDeathWorkflowStep.DoReward);
		if (ShouldCalculateExperienceLoss(facts))
		{
			steps.Add(PlayerDeathWorkflowStep.CalculateExperienceLoss);
		}

		steps.Add(PlayerDeathWorkflowStep.NotifyQuestEngineOnDie);
		return BuildPlan(
			player,
			facts,
			steps,
			PlayerDeathWorkflowStatus.PlannedFullPlayerDeath,
			wouldScheduleResurrectionOptions: true);
	}

	private static bool ShouldCalculateExperienceLoss(PlayerDeathWorkflowFacts facts)
	{
		return facts.LastAttackerMasterIsNpcOrPlayerSelf
			&& facts.PlayerLevel > 4
			&& !facts.HasNoDeathPenaltyEffect;
	}

	private static PlayerDeathWorkflowPlan BuildPlan(
		Player player,
		PlayerDeathWorkflowFacts facts,
		IReadOnlyList<PlayerDeathWorkflowStep> steps,
		PlayerDeathWorkflowStatus status,
		bool wouldScheduleResurrectionOptions)
	{
		var wouldSetFlyingBeforeDeath = player.IsInState(PlayerCreatureState.Flying);
		var wouldUseFloatingCorpse = player.IsFlyingBeforeDeath || wouldSetFlyingBeforeDeath;

		return new PlayerDeathWorkflowPlan(
			status,
			player.ObjectId,
			wouldUseFloatingCorpse
				? PlayerDeathStateTransitionStatus.FloatingCorpseApplied
				: PlayerDeathStateTransitionStatus.DeadStateApplied,
			wouldSetFlyingBeforeDeath,
			wouldUseFloatingCorpse,
			facts.HasSummon && steps.Contains(PlayerDeathWorkflowStep.ReleaseSummon),
			ShouldCalculateExperienceLoss(facts) && steps.Contains(PlayerDeathWorkflowStep.CalculateExperienceLoss),
			wouldScheduleResurrectionOptions,
			wouldScheduleResurrectionOptions
				? PlayerDeathResurrectionOptionsPlanService.CreatePlan(
					player,
					facts.HasTeleportTaskAtResurrectionOptionsCallback)
				: null,
			IsLive: false,
			steps.ToArray(),
			new[]
			{
				"cancelCurrentSkill side effect is planned but not executed.",
				"Rebirth effect scan is planned but not executed.",
				"DuelService loseDuel and HP/MP restoration are planned but not executed.",
				"SummonsService RELEASE is planned but not executed.",
				"CreatureController movement abort, casting clear, effect removal, observer callback, SM_EMOTION DIE broadcast, and known-list stopHating fanout are planned but not executed.",
				"ThreadPoolManager 500ms SM_DIE scheduling is planned but not executed.",
				"Instance and map-region death callbacks are planned from facts rather than invoked.",
				"Reward, XP-loss, no-death-penalty effect scan, and QuestEngine callback are planned but not executed.",
			},
			"com.aionemu.gameserver.controllers.PlayerController.onDie + com.aionemu.gameserver.controllers.CreatureController.onDie");
	}
}
