namespace Aion.GameServer.Services;

public enum PlayerDeathWorkflowReportRowKind
{
	UnsupportedSideEffect,
	PlannedMetadata,
	LiveStateBoundary,
	PacketIntent,
	SchedulerIntent,
	CallbackIntent,
	EarlyReturn,
}

public sealed record PlayerDeathWorkflowReportRow(
	int Order,
	string JavaArtifact,
	string JavaOperation,
	PlayerDeathWorkflowReportRowKind Kind,
	bool IsLive,
	string Notes);

public sealed record PlayerDeathWorkflowReport(
	PlayerDeathWorkflowStatus Status,
	int PlayerObjectId,
	IReadOnlyList<PlayerDeathWorkflowReportRow> Rows,
	string JavaSource,
	bool IsLive);

public static class PlayerDeathWorkflowReportService
{
	public static PlayerDeathWorkflowReport CreateReport(PlayerDeathWorkflowPlan plan)
	{
		var rows = new List<PlayerDeathWorkflowReportRow>();

		AddIfPresent(rows, plan, PlayerDeathWorkflowStep.CancelCurrentSkill, "PlayerController", "cancelCurrentSkill(null)", PlayerDeathWorkflowReportRowKind.UnsupportedSideEffect, "Controller skill cancellation is planned only.");
		AddIfPresent(rows, plan, PlayerDeathWorkflowStep.SetRebirthReviveInfo, "PlayerController", "setRebirthReviveInfo()", PlayerDeathWorkflowReportRowKind.UnsupportedSideEffect, "Rebirth effect scan is planned only.");
		AddIfPresent(rows, plan, PlayerDeathWorkflowStep.ResolveLastAttackerMaster, "PlayerController", "lastAttacker.getMaster()", PlayerDeathWorkflowReportRowKind.PlannedMetadata, "Last-attacker master is supplied as workflow facts.");
		AddIfPresent(rows, plan, PlayerDeathWorkflowStep.CheckDuelState, "PlayerController", "DuelService.isDueling(player)", PlayerDeathWorkflowReportRowKind.PlannedMetadata, "Duel state is supplied as workflow facts.");
		AddIfPresent(rows, plan, PlayerDeathWorkflowStep.LoseDuel, "DuelService", "loseDuel(player)", PlayerDeathWorkflowReportRowKind.UnsupportedSideEffect, "Live duel mutation is not executed.");
		AddIfPresent(rows, plan, PlayerDeathWorkflowStep.RestoreDuelistHitPointsAndMana, "PlayerController", "restore duelist HP/MP floors", PlayerDeathWorkflowReportRowKind.UnsupportedSideEffect, "Life-stat restoration is not executed.");
		AddIfPresent(rows, plan, PlayerDeathWorkflowStep.ReturnAfterDuelOpponentKill, "PlayerController", "return after duel opponent kill", PlayerDeathWorkflowReportRowKind.EarlyReturn, "Java returns before summon release and super.onDie.");

		if (plan.Steps.Contains(PlayerDeathWorkflowStep.ReturnAfterDuelOpponentKill))
		{
			return Build(plan, rows);
		}

		AddIfPresent(rows, plan, PlayerDeathWorkflowStep.ReleaseSummon, "SummonsService", "doMode(RELEASE, summon, UNSPECIFIED)", PlayerDeathWorkflowReportRowKind.UnsupportedSideEffect, "Summon release is planned only.");
		AddStatePhaseRows(rows, plan, PlayerDeathStateTransitionPhase.PlayerControllerPreSuperCleanup);
		AddCoreSideEffectRows(rows, plan);
		AddStatePhaseRows(rows, plan, PlayerDeathStateTransitionPhase.CreatureControllerDeathStateSelection);
		AddFanoutRows(rows, plan);
		AddIfPresent(rows, plan, PlayerDeathWorkflowStep.ScheduleShowResurrectionOptions, "PlayerController", "scheduleShowResurrectionOptions()", PlayerDeathWorkflowReportRowKind.SchedulerIntent, "500ms SM_DIE scheduler intent is metadata only.");
		AddIfPresent(rows, plan, PlayerDeathWorkflowStep.InvokeInstanceHandlerOnDie, "InstanceHandler", "onDie(player, lastAttacker)", PlayerDeathWorkflowReportRowKind.CallbackIntent, "Instance callback is not invoked.");
		AddIfPresent(rows, plan, PlayerDeathWorkflowStep.ReturnAfterInstanceHandler, "PlayerController", "return after instance handler", PlayerDeathWorkflowReportRowKind.EarlyReturn, "Java returns when instance handler consumes death.");

		if (plan.Steps.Contains(PlayerDeathWorkflowStep.ReturnAfterInstanceHandler))
		{
			return Build(plan, rows);
		}

		AddIfPresent(rows, plan, PlayerDeathWorkflowStep.InvokeMapRegionOnDie, "MapRegion", "onDie(lastAttacker, player)", PlayerDeathWorkflowReportRowKind.CallbackIntent, "Map-region callback is not invoked.");
		AddIfPresent(rows, plan, PlayerDeathWorkflowStep.ReturnAfterMapRegion, "PlayerController", "return after map region", PlayerDeathWorkflowReportRowKind.EarlyReturn, "Java returns when map region consumes death.");

		if (plan.Steps.Contains(PlayerDeathWorkflowStep.ReturnAfterMapRegion))
		{
			return Build(plan, rows);
		}

		AddIfPresent(rows, plan, PlayerDeathWorkflowStep.DoReward, "PlayerController", "doReward()", PlayerDeathWorkflowReportRowKind.UnsupportedSideEffect, "Reward side effects are not executed.");
		AddIfPresent(rows, plan, PlayerDeathWorkflowStep.CalculateExperienceLoss, "PlayerCommonData", "calculateExpLoss()", PlayerDeathWorkflowReportRowKind.UnsupportedSideEffect, "XP-loss mutation is not executed.");
		AddIfPresent(rows, plan, PlayerDeathWorkflowStep.NotifyQuestEngineOnDie, "QuestEngine", "onDie(QuestEnv)", PlayerDeathWorkflowReportRowKind.CallbackIntent, "Quest callback is not dispatched.");

		return Build(plan, rows);
	}

	private static void AddStatePhaseRows(
		ICollection<PlayerDeathWorkflowReportRow> rows,
		PlayerDeathWorkflowPlan plan,
		PlayerDeathStateTransitionPhase phase)
	{
		var phasePlan = plan.StatePhasePlans.FirstOrDefault(entry => entry.Phase == phase);
		if (phasePlan is null)
		{
			return;
		}

		foreach (var step in phasePlan.Steps)
		{
			Add(
				rows,
				phase == PlayerDeathStateTransitionPhase.PlayerControllerPreSuperCleanup ? "PlayerController" : "CreatureController",
				step.ToString(),
				PlayerDeathWorkflowReportRowKind.LiveStateBoundary,
				"State phase metadata is previewed; live mutation is opt-in through PlayerDeathStateTransitionService.Apply.");
		}
	}

	private static void AddCoreSideEffectRows(ICollection<PlayerDeathWorkflowReportRow> rows, PlayerDeathWorkflowPlan plan)
	{
		if (plan.CoreSideEffectPlan is null)
		{
			return;
		}

		foreach (var step in plan.CoreSideEffectPlan.Steps)
		{
			Add(rows, "CreatureController", step.ToString(), PlayerDeathWorkflowReportRowKind.UnsupportedSideEffect, "Movement/casting/effect runtime mutation is not executed.");
		}
	}

	private static void AddFanoutRows(ICollection<PlayerDeathWorkflowReportRow> rows, PlayerDeathWorkflowPlan plan)
	{
		if (plan.DeathEmotionFanoutPlan is null)
		{
			return;
		}

		Add(rows, "CreatureController", "notifyDeathObservers(lastAttacker)", PlayerDeathWorkflowReportRowKind.CallbackIntent, "Observer callbacks are not invoked.");
		Add(rows, "PacketSendUtility", "broadcastPacketAndReceive(SM_EMOTION DIE)", PlayerDeathWorkflowReportRowKind.PacketIntent, $"Target object id metadata: {plan.DeathEmotionFanoutPlan.EmotionTargetObjectId}.");
		Add(rows, "AggroList", "known creatures stopHating(owner)", PlayerDeathWorkflowReportRowKind.UnsupportedSideEffect, $"Cleanup intents: {plan.DeathEmotionFanoutPlan.KnownCreatureAggroCleanupIntents.Count}.");
	}

	private static void AddIfPresent(
		ICollection<PlayerDeathWorkflowReportRow> rows,
		PlayerDeathWorkflowPlan plan,
		PlayerDeathWorkflowStep step,
		string javaArtifact,
		string javaOperation,
		PlayerDeathWorkflowReportRowKind kind,
		string notes)
	{
		if (plan.Steps.Contains(step))
		{
			Add(rows, javaArtifact, javaOperation, kind, notes);
		}
	}

	private static void Add(
		ICollection<PlayerDeathWorkflowReportRow> rows,
		string javaArtifact,
		string javaOperation,
		PlayerDeathWorkflowReportRowKind kind,
		string notes)
	{
		rows.Add(new PlayerDeathWorkflowReportRow(
			rows.Count + 1,
			javaArtifact,
			javaOperation,
			kind,
			IsLive: false,
			notes));
	}

	private static PlayerDeathWorkflowReport Build(PlayerDeathWorkflowPlan plan, IReadOnlyList<PlayerDeathWorkflowReportRow> rows)
	{
		return new PlayerDeathWorkflowReport(
			plan.Status,
			plan.PlayerObjectId,
			rows.ToArray(),
			"com.aionemu.gameserver.controllers.PlayerController.onDie + nested com.aionemu.gameserver.controllers.CreatureController.onDie Java-order audit report",
			IsLive: false);
	}
}
