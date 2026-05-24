using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ClientPackets;

namespace Aion.GameServer.Services;

public sealed class PlayerSummonSkillExecutionService
{
	private const int TargetTooFarNextSkillDelayMilliseconds = 5000;

	public PlayerSummonSkillInvocationExecutionResult PlanInvocationExecution(
		PlayerSummonSkillInvocationPlan? invocationPlan,
		SkillTemplateTable skillTemplates,
		Player? player = null)
	{
		if (invocationPlan == null)
			return PlayerSummonSkillInvocationExecutionResult.MissingPlan();

		// Java parity: SkillEngine.getSkill returns null when DataManager.SKILL_DATA has no template.
		var skillTemplate = skillTemplates.GetSkillTemplate(invocationPlan.SkillId);
		if (skillTemplate == null)
			return PlayerSummonSkillInvocationExecutionResult.MissingSkillTemplate(invocationPlan);

		if (invocationPlan.ActorKind == PlayerSummonSkillInvocationActorKind.Mercenary
			&& player?.TryGetSummonKnownObject(invocationPlan.ActorObjectId, out var knownObject) == true
			&& knownObject.IsSkillCooldownDisabled(skillTemplate.CooldownId))
		{
			return PlayerSummonSkillInvocationExecutionResult.DisabledNpcSkill(
				invocationPlan,
				skillTemplate.SkillId,
				skillTemplate.CooldownId);
		}

		return PlayerSummonSkillInvocationExecutionResult.WouldUseSkill(
			invocationPlan,
			skillTemplate.SkillId,
			skillTemplate.CooldownId);
	}

	public PlayerSummonSkillInvocationUseResult PreviewInvocationUse(
		PlayerSummonSkillInvocationExecutionResult? executionResult,
		bool skillUseSucceeded)
	{
		if (executionResult == null)
			return PlayerSummonSkillInvocationUseResult.MissingExecution();

		if (executionResult.Status != PlayerSummonSkillInvocationExecutionStatus.WouldUseSkill
			|| executionResult.InvocationPlan == null)
		{
			return PlayerSummonSkillInvocationUseResult.NotReadyToUseSkill(executionResult);
		}

		// Java parity: Skill.useSkill returns false before SummonController can release the summon.
		if (!skillUseSucceeded)
			return PlayerSummonSkillInvocationUseResult.SkillUseFailed(executionResult);

		if (executionResult.InvocationPlan.ActorKind == PlayerSummonSkillInvocationActorKind.Summon
			&& executionResult.InvocationPlan.ReleaseOnSuccess)
		{
			return PlayerSummonSkillInvocationUseResult.WouldReleaseSummon(executionResult);
		}

		return PlayerSummonSkillInvocationUseResult.WouldCompleteWithoutRelease(executionResult);
	}

	public PlayerSummonKnownObjectLastSkillTimeRenewalResult RenewMercenaryLastSkillTime(
		Player player,
		PlayerSummonSkillInvocationExecutionResult? executionResult,
		long currentTimeMilliseconds)
	{
		if (executionResult == null)
			return PlayerSummonKnownObjectLastSkillTimeRenewalResult.MissingExecution();

		if (!executionResult.WouldRenewLastSkillTime
			|| executionResult.InvocationPlan?.ActorKind != PlayerSummonSkillInvocationActorKind.Mercenary)
		{
			return PlayerSummonKnownObjectLastSkillTimeRenewalResult.NotRenewable(executionResult);
		}

		// Java parity: NpcController.useSkill renews last skill time before CreatureController.useSkill.
		return player.TryRenewSummonKnownObjectLastSkillTime(
				executionResult.InvocationPlan.ActorObjectId,
				currentTimeMilliseconds)
			? PlayerSummonKnownObjectLastSkillTimeRenewalResult.Renewed(executionResult, currentTimeMilliseconds)
			: PlayerSummonKnownObjectLastSkillTimeRenewalResult.MissingKnownObject(executionResult);
	}

	public PlayerSummonKnownObjectNextSkillReadiness EvaluateMercenaryNextSkillReadiness(
		PlayerSummonKnownObject knownObject,
		int nextSkillDelayMilliseconds,
		long currentTimeMilliseconds)
	{
		if (nextSkillDelayMilliseconds < 0)
			return PlayerSummonKnownObjectNextSkillReadiness.RandomDelayUnsupported(knownObject, nextSkillDelayMilliseconds);

		// Java parity: NpcGameStats.canUseNextSkill -> nextSkillDelay == 0 || now >= lastSkillTime + nextSkillDelay.
		if (nextSkillDelayMilliseconds == 0)
			return PlayerSummonKnownObjectNextSkillReadiness.Ready(knownObject, nextSkillDelayMilliseconds, currentTimeMilliseconds);

		var lastSkillTime = knownObject.LastSkillTimeMilliseconds ?? 0;
		var readyAt = lastSkillTime + nextSkillDelayMilliseconds;
		return currentTimeMilliseconds >= readyAt
			? PlayerSummonKnownObjectNextSkillReadiness.Ready(knownObject, nextSkillDelayMilliseconds, currentTimeMilliseconds, readyAt)
			: PlayerSummonKnownObjectNextSkillReadiness.NotReady(knownObject, nextSkillDelayMilliseconds, currentTimeMilliseconds, readyAt);
	}

	public PlayerSummonKnownObjectNextSkillDelayResult SetMercenaryNextSkillDelay(
		Player player,
		int mercenaryObjectId,
		int nextSkillDelayMilliseconds)
	{
		if (nextSkillDelayMilliseconds < 0)
			return PlayerSummonKnownObjectNextSkillDelayResult.RandomDelayUnsupported(mercenaryObjectId, nextSkillDelayMilliseconds);

		// Java parity: NpcGameStats.setNextSkillDelay stores concrete delays directly.
		return player.TrySetSummonKnownObjectNextSkillDelay(mercenaryObjectId, nextSkillDelayMilliseconds)
			? PlayerSummonKnownObjectNextSkillDelayResult.Set(mercenaryObjectId, nextSkillDelayMilliseconds)
			: PlayerSummonKnownObjectNextSkillDelayResult.MissingKnownObject(mercenaryObjectId, nextSkillDelayMilliseconds);
	}

	public PlayerSummonKnownObjectSkillReadiness EvaluateMercenarySkillReadiness(
		PlayerSummonKnownObject knownObject,
		SkillTemplateSummary? skillTemplate,
		bool entryTimingReady = true,
		bool entryConditionReady = true)
	{
		return EvaluateMercenarySkillReadiness(
			knownObject,
			skillTemplate,
			entryTimingReadiness: null,
			entryConditionReadiness: null,
			entryTimingReady,
			entryConditionReady);
	}

	public PlayerSummonKnownObjectSkillReadiness EvaluateMercenarySkillReadiness(
		PlayerSummonKnownObject knownObject,
		SkillTemplateSummary? skillTemplate,
		PlayerSummonKnownObjectNpcSkillEntryReadiness? entryTimingReadiness,
		bool entryTimingReady = true,
		bool entryConditionReady = true)
	{
		return EvaluateMercenarySkillReadiness(
			knownObject,
			skillTemplate,
			entryTimingReadiness,
			entryConditionReadiness: null,
			entryTimingReady,
			entryConditionReady);
	}

	public PlayerSummonKnownObjectSkillReadiness EvaluateMercenarySkillReadiness(
		PlayerSummonKnownObject knownObject,
		SkillTemplateSummary? skillTemplate,
		PlayerSummonKnownObjectNpcSkillEntryReadiness? entryTimingReadiness,
		PlayerSummonKnownObjectNpcSkillConditionReadiness? entryConditionReadiness,
		bool entryTimingReady = true,
		bool entryConditionReady = true)
	{
		if (!entryTimingReady)
			return PlayerSummonKnownObjectSkillReadiness.EntryTimingNotReady(knownObject, skillTemplate, entryTimingReadiness, entryConditionReadiness);

		if (entryTimingReadiness is { Status: not PlayerSummonKnownObjectNpcSkillEntryReadinessStatus.Ready })
		{
			return PlayerSummonKnownObjectSkillReadiness.EntryTimingNotReady(knownObject, skillTemplate, entryTimingReadiness, entryConditionReadiness);
		}

		if (!entryConditionReady)
			return PlayerSummonKnownObjectSkillReadiness.EntryConditionNotReady(knownObject, skillTemplate, entryTimingReadiness, entryConditionReadiness);

		if (entryConditionReadiness is { Status: not PlayerSummonKnownObjectNpcSkillConditionReadinessStatus.Ready })
			return PlayerSummonKnownObjectSkillReadiness.EntryConditionNotReady(knownObject, skillTemplate, entryTimingReadiness, entryConditionReadiness);

		// Java parity: SkillAttackManager.isReady resolves SkillTemplate through DataManager.SKILL_DATA.
		if (skillTemplate == null)
			return PlayerSummonKnownObjectSkillReadiness.MissingSkillTemplate(knownObject, entryTimingReadiness, entryConditionReadiness);

		if (string.Equals(skillTemplate.SkillType, "MAGICAL", StringComparison.Ordinal)
			&& knownObject.IsAbnormalSet(PlayerAbnormalState.Silence))
		{
			return PlayerSummonKnownObjectSkillReadiness.BlockedBySilence(knownObject, skillTemplate, entryTimingReadiness, entryConditionReadiness);
		}

		if (string.Equals(skillTemplate.SkillType, "PHYSICAL", StringComparison.Ordinal)
			&& knownObject.IsAbnormalSet(PlayerAbnormalState.Bind))
		{
			return PlayerSummonKnownObjectSkillReadiness.BlockedByBind(knownObject, skillTemplate, entryTimingReadiness, entryConditionReadiness);
		}

		if (knownObject.IsInAnyAbnormalState(PlayerAbnormalState.CantAttackState))
			return PlayerSummonKnownObjectSkillReadiness.BlockedByCantAttackState(knownObject, skillTemplate, entryTimingReadiness, entryConditionReadiness);

		if (knownObject.IsTransformed && knownObject.TransformBansSkillUse)
			return PlayerSummonKnownObjectSkillReadiness.BlockedByTransformSkillBan(knownObject, skillTemplate, entryTimingReadiness, entryConditionReadiness);

		return PlayerSummonKnownObjectSkillReadiness.Ready(knownObject, skillTemplate, entryTimingReadiness, entryConditionReadiness);
	}

	public PlayerSummonKnownObjectNpcSkillEntryReadiness EvaluateMercenaryNpcSkillEntryReadiness(
		PlayerSummonKnownObjectNpcSkillEntryTiming timing,
		int hpPercentage,
		long elapsedFightTimeMilliseconds,
		long currentTimeMilliseconds,
		bool chanceReady = true)
	{
		if (timing.CooldownMilliseconds > currentTimeMilliseconds - timing.LastTimeUsedMilliseconds)
			return PlayerSummonKnownObjectNpcSkillEntryReadiness.OnCooldown(timing);

		if (!chanceReady)
			return PlayerSummonKnownObjectNpcSkillEntryReadiness.ChanceNotReady(timing);

		var hpReady = IsNpcSkillEntryHpReady(timing, hpPercentage);
		var timeReady = IsNpcSkillEntryTimeReady(timing, elapsedFightTimeMilliseconds);
		var ready = timing.ConjunctionType switch
		{
			PlayerSummonKnownObjectNpcSkillConjunction.Xor => hpReady ^ timeReady,
			PlayerSummonKnownObjectNpcSkillConjunction.Or => hpReady || timeReady,
			PlayerSummonKnownObjectNpcSkillConjunction.And => hpReady && timeReady,
			_ => hpReady && timeReady,
		};

		return ready
			? PlayerSummonKnownObjectNpcSkillEntryReadiness.Ready(timing, hpReady, timeReady)
			: PlayerSummonKnownObjectNpcSkillEntryReadiness.NotReady(timing, hpReady, timeReady);
	}

	public PlayerSummonKnownObjectNpcSkillTemplateProjection ProjectMercenaryNpcSkillTemplate(
		PlayerSummonKnownObjectNpcSkillTemplateMetadata template,
		long lastTimeUsedMilliseconds = 0)
	{
		return new PlayerSummonKnownObjectNpcSkillTemplateProjection(
			ProjectMercenaryNpcSkillEntryTiming(template, lastTimeUsedMilliseconds),
			template.ConditionTemplate ?? new PlayerSummonKnownObjectNpcSkillConditionMetadata(),
			ResolveMercenaryNpcSkillTargetMode(template.Target),
			template.Probability,
			template.Priority,
			template.NextSkillTimeMilliseconds,
			template.NextChainId,
			template.ChainId,
			template.MaxChainTimeMilliseconds,
			template.IsPostSpawn);
	}

	public PlayerSummonKnownObjectNpcSkillEntryTiming ProjectMercenaryNpcSkillEntryTiming(
		PlayerSummonKnownObjectNpcSkillTemplateMetadata template,
		long lastTimeUsedMilliseconds = 0)
	{
		// Java parity: mirrors NpcSkillTemplate fields consumed by NpcSkillTemplateEntry.isReady.
		return new PlayerSummonKnownObjectNpcSkillEntryTiming(
			template.MinHpPercentage,
			template.MaxHpPercentage,
			template.MinTimeMilliseconds,
			template.MaxTimeMilliseconds,
			template.ConjunctionType,
			template.CooldownMilliseconds,
			lastTimeUsedMilliseconds);
	}

	public PlayerSummonKnownObjectSkillTargetMode ResolveMercenaryNpcSkillTargetMode(PlayerSummonKnownObjectNpcSkillTargetAttribute target)
	{
		return target switch
		{
			PlayerSummonKnownObjectNpcSkillTargetAttribute.None => PlayerSummonKnownObjectSkillTargetMode.None,
			PlayerSummonKnownObjectNpcSkillTargetAttribute.MostHated => PlayerSummonKnownObjectSkillTargetMode.MostHated,
			PlayerSummonKnownObjectNpcSkillTargetAttribute.Me => PlayerSummonKnownObjectSkillTargetMode.Self,
			_ => PlayerSummonKnownObjectSkillTargetMode.CreatureTarget,
		};
	}

	public PlayerSummonKnownObjectNpcSkillActionTargetSelection SelectMercenaryNpcSkillActionTarget(
		bool skillFirstTargetIsSelf,
		PlayerSummonKnownObjectNpcSkillTargetAttribute npcSkillTarget,
		bool hasFriendTarget = false,
		bool hasMostHatedTarget = false,
		bool hasSecondMostHatedTarget = false,
		bool hasThirdMostHatedTarget = false,
		bool hasRandomTarget = false,
		bool hasRandomExceptCurrentTarget = false)
	{
		if (skillFirstTargetIsSelf)
			return PlayerSummonKnownObjectNpcSkillActionTargetSelection.Selected(PlayerSummonKnownObjectNpcSkillActionTargetSource.Owner);

		return npcSkillTarget switch
		{
			PlayerSummonKnownObjectNpcSkillTargetAttribute.Friend => SelectOptionalMercenaryNpcSkillActionTarget(
				PlayerSummonKnownObjectNpcSkillActionTargetSource.Friend,
				hasFriendTarget),
			PlayerSummonKnownObjectNpcSkillTargetAttribute.Me => PlayerSummonKnownObjectNpcSkillActionTargetSelection.Selected(
				PlayerSummonKnownObjectNpcSkillActionTargetSource.Owner),
			PlayerSummonKnownObjectNpcSkillTargetAttribute.MostHated => SelectOptionalMercenaryNpcSkillActionTarget(
				PlayerSummonKnownObjectNpcSkillActionTargetSource.MostHated,
				hasMostHatedTarget),
			PlayerSummonKnownObjectNpcSkillTargetAttribute.SecondMostHated => SelectOptionalMercenaryNpcSkillActionTarget(
				PlayerSummonKnownObjectNpcSkillActionTargetSource.SecondMostHated,
				hasSecondMostHatedTarget),
			PlayerSummonKnownObjectNpcSkillTargetAttribute.ThirdMostHated => SelectOptionalMercenaryNpcSkillActionTarget(
				PlayerSummonKnownObjectNpcSkillActionTargetSource.ThirdMostHated,
				hasThirdMostHatedTarget),
			PlayerSummonKnownObjectNpcSkillTargetAttribute.Random => SelectOptionalMercenaryNpcSkillActionTarget(
				PlayerSummonKnownObjectNpcSkillActionTargetSource.Random,
				hasRandomTarget),
			PlayerSummonKnownObjectNpcSkillTargetAttribute.RandomExceptCurrentTarget => SelectOptionalMercenaryNpcSkillActionTarget(
				PlayerSummonKnownObjectNpcSkillActionTargetSource.RandomExceptCurrentTarget,
				hasRandomExceptCurrentTarget),
			PlayerSummonKnownObjectNpcSkillTargetAttribute.None => PlayerSummonKnownObjectNpcSkillActionTargetSelection.NotRequired(
				PlayerSummonKnownObjectNpcSkillActionTargetSource.None),
			_ => PlayerSummonKnownObjectNpcSkillActionTargetSelection.NotRequired(PlayerSummonKnownObjectNpcSkillActionTargetSource.None),
		};
	}

	private static PlayerSummonKnownObjectNpcSkillActionTargetSelection SelectOptionalMercenaryNpcSkillActionTarget(
		PlayerSummonKnownObjectNpcSkillActionTargetSource source,
		bool hasTarget)
	{
		return hasTarget
			? PlayerSummonKnownObjectNpcSkillActionTargetSelection.Selected(source)
			: PlayerSummonKnownObjectNpcSkillActionTargetSelection.MissingTarget(source);
	}

	public PlayerSummonKnownObjectNpcSkillActionPreview PreviewMercenaryNpcSkillAction(
		bool isInCastSubState,
		bool shouldResumeFightAfterInterruptedCast,
		bool hasCreatureTarget,
		bool targetIsDead,
		bool hasLastSkill,
		bool ownerUsesMeleeAggroRange,
		bool targetInAggroRange,
		PlayerSummonKnownObjectSkillReadiness? skillReadiness,
		PlayerSummonKnownObjectNpcSkillActionTargetSelection? targetSelection,
		bool controllerUseSkillSucceeded)
	{
		if (!isInCastSubState)
		{
			return shouldResumeFightAfterInterruptedCast
				? PlayerSummonKnownObjectNpcSkillActionPreview.ResumeFightAfterInterruptedCast()
				: PlayerSummonKnownObjectNpcSkillActionPreview.NotInCastSubState();
		}

		if (!hasCreatureTarget || targetIsDead || !hasLastSkill)
			return PlayerSummonKnownObjectNpcSkillActionPreview.TargetGiveUp();

		if (ownerUsesMeleeAggroRange && !targetInAggroRange)
			return PlayerSummonKnownObjectNpcSkillActionPreview.TargetTooFar();

		if (skillReadiness is { Status: not PlayerSummonKnownObjectSkillReadinessStatus.Ready })
			return PlayerSummonKnownObjectNpcSkillActionPreview.AfterUseSkillBlocked(skillReadiness);

		if (!controllerUseSkillSucceeded)
			return PlayerSummonKnownObjectNpcSkillActionPreview.AfterUseSkillUseFailed(targetSelection);

		return targetSelection is { ShouldSetOwnerTarget: true }
			? PlayerSummonKnownObjectNpcSkillActionPreview.WouldSetTargetAndUseSkill(targetSelection)
			: PlayerSummonKnownObjectNpcSkillActionPreview.WouldUseSkill(targetSelection);
	}

	private static bool IsNpcSkillEntryHpReady(PlayerSummonKnownObjectNpcSkillEntryTiming timing, int hpPercentage)
	{
		// Java parity: NpcSkillTemplateEntry.hpReady treats default 0..100 as "not about HP".
		return timing.MaxHpPercentage == 100 && timing.MinHpPercentage == 0
			|| timing.MaxHpPercentage >= hpPercentage && timing.MinHpPercentage <= hpPercentage;
	}

	private static bool IsNpcSkillEntryTimeReady(PlayerSummonKnownObjectNpcSkillEntryTiming timing, long elapsedFightTimeMilliseconds)
	{
		// Java parity: NpcSkillTemplateEntry.timeReady supports no time gate, min-only, and bounded ranges.
		return timing.MaxTimeMilliseconds == 0 && timing.MinTimeMilliseconds == 0
			|| timing.MaxTimeMilliseconds == 0 && timing.MinTimeMilliseconds <= elapsedFightTimeMilliseconds
			|| timing.MaxTimeMilliseconds >= elapsedFightTimeMilliseconds && timing.MinTimeMilliseconds <= elapsedFightTimeMilliseconds;
	}

	public PlayerSummonKnownObjectNpcSkillConditionReadiness EvaluateMercenaryNpcSkillConditionReadiness(
		PlayerSummonKnownObjectNpcSkillCondition condition,
		PlayerSummonKnownObjectNpcSkillConditionTarget? target,
		bool ownerExists = true,
		bool ownerIsDead = false,
		bool ownerIsAboutToDie = false)
	{
		if (!ownerExists || ownerIsDead || ownerIsAboutToDie)
			return PlayerSummonKnownObjectNpcSkillConditionReadiness.OwnerNotReady(condition, target);

		return condition switch
		{
			PlayerSummonKnownObjectNpcSkillCondition.None => PlayerSummonKnownObjectNpcSkillConditionReadiness.Ready(condition, target),
			PlayerSummonKnownObjectNpcSkillCondition.TargetIsAethersHold => MatchTargetAbnormalState(condition, target, PlayerAbnormalState.OpenAerial),
			PlayerSummonKnownObjectNpcSkillCondition.TargetIsStunned => MatchTargetAbnormalState(condition, target, PlayerAbnormalState.Stun),
			PlayerSummonKnownObjectNpcSkillCondition.TargetIsInAnyStun => MatchTargetAbnormalState(condition, target, PlayerAbnormalState.AnyStun),
			PlayerSummonKnownObjectNpcSkillCondition.TargetIsInStumble => MatchTargetAbnormalState(condition, target, PlayerAbnormalState.Stumble),
			PlayerSummonKnownObjectNpcSkillCondition.TargetIsSleeping => MatchTargetAbnormalState(condition, target, PlayerAbnormalState.Sleep),
			PlayerSummonKnownObjectNpcSkillCondition.TargetIsPoisoned => MatchTargetAbnormalState(condition, target, PlayerAbnormalState.Poison),
			PlayerSummonKnownObjectNpcSkillCondition.TargetIsBleeding => MatchTargetAbnormalState(condition, target, PlayerAbnormalState.Bleed),
			PlayerSummonKnownObjectNpcSkillCondition.TargetIsFlying => MatchTargetFlag(condition, target, static target => target.IsFlying),
			PlayerSummonKnownObjectNpcSkillCondition.TargetIsGate => MatchTargetFlag(condition, target, static target => target.Kind == PlayerSummonKnownObjectNpcSkillConditionTargetKind.Gate),
			PlayerSummonKnownObjectNpcSkillCondition.TargetIsPlayer => MatchTargetFlag(condition, target, static target => target.Kind == PlayerSummonKnownObjectNpcSkillConditionTargetKind.Player),
			PlayerSummonKnownObjectNpcSkillCondition.TargetIsNpc => MatchTargetFlag(condition, target, static target => target.Kind == PlayerSummonKnownObjectNpcSkillConditionTargetKind.Npc),
			PlayerSummonKnownObjectNpcSkillCondition.TargetIsMagicalClass => MatchTargetFlag(condition, target, static target => target.Kind == PlayerSummonKnownObjectNpcSkillConditionTargetKind.Player && target.IsPhysicalClass == false),
			PlayerSummonKnownObjectNpcSkillCondition.TargetIsPhysicalClass => MatchTargetFlag(condition, target, static target => target.Kind == PlayerSummonKnownObjectNpcSkillConditionTargetKind.Player && target.IsPhysicalClass == true),
			PlayerSummonKnownObjectNpcSkillCondition.TargetIsInRange => MatchTargetFlag(condition, target, static target => target.IsInRange),
			_ => PlayerSummonKnownObjectNpcSkillConditionReadiness.Unsupported(condition, target),
		};
	}

	public PlayerSummonKnownObjectNpcSkillConditionReadiness EvaluateMercenaryNpcSkillConditionReadiness(
		PlayerSummonKnownObjectNpcSkillConditionMetadata conditionMetadata,
		PlayerSummonKnownObjectNpcSkillConditionTarget? target,
		bool ownerExists = true,
		bool ownerIsDead = false,
		bool ownerIsAboutToDie = false)
	{
		return EvaluateMercenaryNpcSkillConditionReadiness(
			conditionMetadata.Condition,
			target,
			ownerExists,
			ownerIsDead,
			ownerIsAboutToDie);
	}

	public PlayerSummonKnownObjectNpcSkillConditionTarget ProjectMercenaryNpcSkillConditionTarget(
		PlayerSummonKnownObjectNpcSkillConditionTargetKind kind,
		PlayerSummonKnownObjectNpcSkillConditionMetadata conditionMetadata,
		double distanceMeters,
		PlayerAbnormalState abnormalState = PlayerAbnormalState.None,
		bool isFlying = false,
		bool? isPhysicalClass = null)
	{
		// Java parity: NpcSkillTemplateEntry.conditionReady uses PositionUtil.isInRange(..., condTemp.getRange(), false).
		return new PlayerSummonKnownObjectNpcSkillConditionTarget(
			kind,
			abnormalState,
			isFlying,
			isPhysicalClass,
			distanceMeters <= conditionMetadata.RangeMeters);
	}

	public PlayerSummonKnownObjectNpcSkillSelectionResult SelectMercenaryNpcSkillCandidate(
		IEnumerable<PlayerSummonKnownObjectNpcSkillCandidate> candidates,
		bool includeChainSkills = false)
	{
		var orderedCandidates = candidates
			.OrderByDescending(candidate => candidate.Projection.Priority)
			.ThenBy(candidate => candidate.Position)
			.ToList();

		if (orderedCandidates.Count == 0)
			return PlayerSummonKnownObjectNpcSkillSelectionResult.Empty();

		foreach (var candidate in orderedCandidates)
		{
			// Java parity: SkillAttackManager.chooseNextSkill skips ordinary priority entries with a non-zero chain id.
			if (!includeChainSkills && candidate.Projection.ChainId != 0)
				continue;

			if (candidate.EntryTimingReadiness.Status != PlayerSummonKnownObjectNpcSkillEntryReadinessStatus.Ready
				|| candidate.EntryConditionReadiness.Status != PlayerSummonKnownObjectNpcSkillConditionReadinessStatus.Ready)
			{
				continue;
			}

			if (candidate.TargetRangeReadiness is
				{
					Status: not PlayerSummonKnownObjectTargetRangeReadinessStatus.Ready
						and not PlayerSummonKnownObjectTargetRangeReadinessStatus.NotRequired,
				})
			{
				return PlayerSummonKnownObjectNpcSkillSelectionResult.TargetRangeNotReady(candidate);
			}

			return PlayerSummonKnownObjectNpcSkillSelectionResult.Ready(candidate);
		}

		return PlayerSummonKnownObjectNpcSkillSelectionResult.NoReadyCandidate();
	}

	public PlayerSummonKnownObjectNpcSkillSelectionResult SelectMercenaryQueuedNpcSkillCandidate(
		PlayerSummonKnownObjectNpcSkillCandidate? queuedCandidate,
		bool initialSkillDelayElapsed,
		bool canUseNextSkill)
	{
		if (queuedCandidate == null)
			return PlayerSummonKnownObjectNpcSkillSelectionResult.Empty();

		// Java parity: chooseNextSkill checks queued nextSkillTime == 0 before initial-delay/can-use gates.
		if (queuedCandidate.Projection.NextSkillTimeMilliseconds == 0)
		{
			var immediateQueuedResult = SelectSingleMercenaryNpcSkillCandidate(
				queuedCandidate,
				PlayerSummonKnownObjectNpcSkillSelectionSource.ImmediateQueuedSkill);
			if (immediateQueuedResult.Status != PlayerSummonKnownObjectNpcSkillSelectionStatus.NoReadyCandidate)
				return immediateQueuedResult;
		}

		if (!initialSkillDelayElapsed || !canUseNextSkill)
			return PlayerSummonKnownObjectNpcSkillSelectionResult.WaitingForDelayGate(queuedCandidate);

		return SelectSingleMercenaryNpcSkillCandidate(
			queuedCandidate,
			PlayerSummonKnownObjectNpcSkillSelectionSource.DelayedQueuedSkill);
	}

	public PlayerSummonKnownObjectNpcSkillSelectionResult SelectMercenaryChainNpcSkillCandidate(
		PlayerSummonKnownObjectNpcSkillTemplateProjection? lastSkill,
		IEnumerable<PlayerSummonKnownObjectNpcSkillCandidate> candidates,
		long elapsedSinceLastSkillMilliseconds)
	{
		if (lastSkill == null
			|| lastSkill.NextChainId <= 0
			|| elapsedSinceLastSkillMilliseconds >= lastSkill.MaxChainTimeMilliseconds)
		{
			return PlayerSummonKnownObjectNpcSkillSelectionResult.NoReadyCandidate(
				PlayerSummonKnownObjectNpcSkillSelectionSource.ChainSkill);
		}

		var chainCandidates = candidates
			.Where(candidate => candidate.Projection.ChainId == lastSkill.NextChainId)
			.ToList();

		if (chainCandidates.Count == 0)
		{
			return PlayerSummonKnownObjectNpcSkillSelectionResult.NoReadyCandidate(
				PlayerSummonKnownObjectNpcSkillSelectionSource.ChainSkill);
		}

		var orderedCandidates = chainCandidates.Any(candidate => candidate.Projection.Priority > 0)
			? chainCandidates
				.OrderByDescending(candidate => candidate.Projection.Priority)
				.ThenBy(candidate => candidate.Position)
			: chainCandidates.OrderBy(candidate => candidate.Position);

		foreach (var candidate in orderedCandidates)
		{
			var result = SelectSingleMercenaryNpcSkillCandidate(
				candidate,
				PlayerSummonKnownObjectNpcSkillSelectionSource.ChainSkill);
			if (result.Status != PlayerSummonKnownObjectNpcSkillSelectionStatus.NoReadyCandidate)
				return result;
		}

		return PlayerSummonKnownObjectNpcSkillSelectionResult.NoReadyCandidate(
			PlayerSummonKnownObjectNpcSkillSelectionSource.ChainSkill);
	}

	public PlayerSummonKnownObjectNpcSkillSelectionResult SelectMercenaryNextNpcSkillCandidate(
		bool isInCastSubState,
		bool initialSkillDelayElapsed,
		bool canUseNextSkill,
		PlayerSummonKnownObjectNpcSkillCandidate? queuedCandidate,
		PlayerSummonKnownObjectNpcSkillTemplateProjection? lastSkill,
		IEnumerable<PlayerSummonKnownObjectNpcSkillCandidate> candidates,
		long elapsedSinceLastSkillMilliseconds)
	{
		if (isInCastSubState)
			return PlayerSummonKnownObjectNpcSkillSelectionResult.InCastSubState();

		var candidateList = candidates.ToList();

		if (queuedCandidate is { Projection.NextSkillTimeMilliseconds: 0 })
		{
			// Java parity: chooseNextSkill checks queued nextSkillTime == 0 before initial-delay/can-use gates.
			var immediateQueuedResult = SelectSingleMercenaryNpcSkillCandidate(
				queuedCandidate,
				PlayerSummonKnownObjectNpcSkillSelectionSource.ImmediateQueuedSkill);
			if (immediateQueuedResult.Status != PlayerSummonKnownObjectNpcSkillSelectionStatus.NoReadyCandidate)
				return immediateQueuedResult;
		}

		if (!initialSkillDelayElapsed || !canUseNextSkill)
		{
			return PlayerSummonKnownObjectNpcSkillSelectionResult.WaitingForDelayGate(
				queuedCandidate,
				PlayerSummonKnownObjectNpcSkillSelectionSource.ChooseNextSkillGate);
		}

		if (queuedCandidate != null)
		{
			var delayedQueuedResult = SelectSingleMercenaryNpcSkillCandidate(
				queuedCandidate,
				PlayerSummonKnownObjectNpcSkillSelectionSource.DelayedQueuedSkill);
			if (delayedQueuedResult.Status != PlayerSummonKnownObjectNpcSkillSelectionStatus.NoReadyCandidate)
				return delayedQueuedResult;
		}

		var chainResult = SelectMercenaryChainNpcSkillCandidate(
			lastSkill,
			candidateList,
			elapsedSinceLastSkillMilliseconds);
		if (chainResult.Status != PlayerSummonKnownObjectNpcSkillSelectionStatus.NoReadyCandidate)
			return chainResult;

		return SelectMercenaryNpcSkillCandidate(candidateList);
	}

	public PlayerSummonKnownObjectNpcSkillSelectionPreview PreviewMercenaryNextNpcSkillSelection(
		PlayerSummonKnownObject knownObject,
		long fightStartingTimeMilliseconds,
		int initialSkillDelayMilliseconds,
		long currentTimeMilliseconds,
		bool isInCastSubState,
		PlayerSummonKnownObjectNpcSkillCandidate? queuedCandidate,
		PlayerSummonKnownObjectNpcSkillTemplateProjection? lastSkill,
		IEnumerable<PlayerSummonKnownObjectNpcSkillCandidate> candidates)
	{
		var elapsedFightTime = currentTimeMilliseconds - fightStartingTimeMilliseconds;
		var initialSkillDelayElapsed = elapsedFightTime > initialSkillDelayMilliseconds;
		var nextSkillDelay = knownObject.NextSkillDelayMilliseconds ?? 0;
		var nextSkillReadiness = EvaluateMercenaryNextSkillReadiness(
			knownObject,
			nextSkillDelay,
			currentTimeMilliseconds);
		var elapsedSinceLastSkill = currentTimeMilliseconds - (knownObject.LastSkillTimeMilliseconds ?? 0);
		var selection = SelectMercenaryNextNpcSkillCandidate(
			isInCastSubState,
			initialSkillDelayElapsed,
			nextSkillReadiness.Status == PlayerSummonKnownObjectNextSkillReadinessStatus.Ready,
			queuedCandidate,
			lastSkill,
			candidates,
			elapsedSinceLastSkill);

		return new PlayerSummonKnownObjectNpcSkillSelectionPreview(
			knownObject,
			currentTimeMilliseconds,
			elapsedFightTime,
			initialSkillDelayMilliseconds,
			initialSkillDelayElapsed,
			nextSkillReadiness,
			elapsedSinceLastSkill,
			selection);
	}

	private static PlayerSummonKnownObjectNpcSkillSelectionResult SelectSingleMercenaryNpcSkillCandidate(
		PlayerSummonKnownObjectNpcSkillCandidate candidate,
		PlayerSummonKnownObjectNpcSkillSelectionSource source)
	{
		if (candidate.EntryTimingReadiness.Status != PlayerSummonKnownObjectNpcSkillEntryReadinessStatus.Ready
			|| candidate.EntryConditionReadiness.Status != PlayerSummonKnownObjectNpcSkillConditionReadinessStatus.Ready)
		{
			return PlayerSummonKnownObjectNpcSkillSelectionResult.NoReadyCandidate(source);
		}

		if (candidate.TargetRangeReadiness is
			{
				Status: not PlayerSummonKnownObjectTargetRangeReadinessStatus.Ready
					and not PlayerSummonKnownObjectTargetRangeReadinessStatus.NotRequired,
			})
		{
			return PlayerSummonKnownObjectNpcSkillSelectionResult.TargetRangeNotReady(candidate, source);
		}

		return PlayerSummonKnownObjectNpcSkillSelectionResult.Ready(candidate, source);
	}

	private static PlayerSummonKnownObjectNpcSkillConditionReadiness MatchTargetAbnormalState(
		PlayerSummonKnownObjectNpcSkillCondition condition,
		PlayerSummonKnownObjectNpcSkillConditionTarget? target,
		PlayerAbnormalState abnormalState)
	{
		return MatchTargetFlag(condition, target, target => target.IsInAnyAbnormalState(abnormalState));
	}

	private static PlayerSummonKnownObjectNpcSkillConditionReadiness MatchTargetFlag(
		PlayerSummonKnownObjectNpcSkillCondition condition,
		PlayerSummonKnownObjectNpcSkillConditionTarget? target,
		Func<PlayerSummonKnownObjectNpcSkillConditionTarget, bool> predicate)
	{
		if (target == null)
			return PlayerSummonKnownObjectNpcSkillConditionReadiness.MissingTarget(condition);

		return predicate(target)
			? PlayerSummonKnownObjectNpcSkillConditionReadiness.Ready(condition, target)
			: PlayerSummonKnownObjectNpcSkillConditionReadiness.NotReady(condition, target);
	}

	public PlayerSummonKnownObjectTargetRangeReadiness EvaluateMercenaryTargetRange(
		PlayerSummonKnownObject knownObject,
		bool requiresCreatureTargetCheck,
		bool hasCreatureTarget,
		bool targetIsDead = false,
		bool canSeeTarget = true,
		bool isAreaTarget = false,
		bool isInRange = true)
	{
		return EvaluateMercenaryTargetRange(
			knownObject,
			requiresCreatureTargetCheck
				? PlayerSummonKnownObjectSkillTargetMode.CreatureTarget
				: PlayerSummonKnownObjectSkillTargetMode.SkipRangeCheck,
			hasCreatureTarget,
			targetIsDead,
			canSeeTarget,
			isAreaTarget,
			isInRange);
	}

	public PlayerSummonKnownObjectTargetRangeReadiness EvaluateMercenaryTargetRange(
		PlayerSummonKnownObject knownObject,
		PlayerSummonKnownObjectSkillTargetMode targetMode,
		bool hasCreatureTarget,
		bool targetIsDead = false,
		bool canSeeTarget = true,
		bool isAreaTarget = false,
		bool isInRange = true)
	{
		if (targetMode != PlayerSummonKnownObjectSkillTargetMode.CreatureTarget)
			return PlayerSummonKnownObjectTargetRangeReadiness.NotRequired(knownObject);

		if (!hasCreatureTarget)
			return PlayerSummonKnownObjectTargetRangeReadiness.MissingCreatureTarget(knownObject, TargetTooFarNextSkillDelayMilliseconds);

		if (targetIsDead)
			return PlayerSummonKnownObjectTargetRangeReadiness.TargetDead(knownObject, TargetTooFarNextSkillDelayMilliseconds);

		if (!canSeeTarget)
			return PlayerSummonKnownObjectTargetRangeReadiness.CannotSeeTarget(knownObject, TargetTooFarNextSkillDelayMilliseconds);

		// Java parity: SkillAttackManager.targetTooFar skips PositionUtil.isInRange for AREA target range skills.
		if (!isAreaTarget && !isInRange)
			return PlayerSummonKnownObjectTargetRangeReadiness.TargetOutOfRange(knownObject, TargetTooFarNextSkillDelayMilliseconds);

		return PlayerSummonKnownObjectTargetRangeReadiness.Ready(knownObject);
	}

	public PlayerSummonKnownObjectTargetRangeDelayResult ApplyMercenaryTargetRangeDelay(
		Player player,
		int mercenaryObjectId,
		PlayerSummonKnownObjectTargetRangeReadiness? targetRangeReadiness)
	{
		if (targetRangeReadiness == null)
			return PlayerSummonKnownObjectTargetRangeDelayResult.MissingRangeEvaluation(mercenaryObjectId);

		if (!targetRangeReadiness.ShouldSetNextSkillDelay)
			return PlayerSummonKnownObjectTargetRangeDelayResult.NotRequired(mercenaryObjectId, targetRangeReadiness);

		// Java parity: SkillAttackManager.getNpcSkillEntryIfNotTooFarAway sets nextSkillDelay to 5000 on too-far targets.
		return player.TrySetSummonKnownObjectNextSkillDelay(mercenaryObjectId, targetRangeReadiness.NextSkillDelayMilliseconds!.Value)
			? PlayerSummonKnownObjectTargetRangeDelayResult.Set(mercenaryObjectId, targetRangeReadiness)
			: PlayerSummonKnownObjectTargetRangeDelayResult.MissingKnownObject(mercenaryObjectId, targetRangeReadiness);
	}

	public PlayerSummonKnownObjectSkillAttackPreview PreviewMercenarySkillAttack(
		PlayerSummonKnownObject knownObject,
		long fightStartingTimeMilliseconds,
		int initialSkillDelayMilliseconds,
		long currentTimeMilliseconds,
		bool isCasting,
		bool hasReadyQueuedInstantSkill = false)
	{
		if (isCasting)
			return PlayerSummonKnownObjectSkillAttackPreview.BlockedCasting(knownObject, currentTimeMilliseconds);

		// Java parity: SkillAttackManager returns a ready queued skill with nextSkillTime == 0 before the ordinary delay gate.
		if (hasReadyQueuedInstantSkill)
			return PlayerSummonKnownObjectSkillAttackPreview.WouldUseQueuedInstantSkill(knownObject, currentTimeMilliseconds);

		var elapsedFightTime = currentTimeMilliseconds - fightStartingTimeMilliseconds;
		if (elapsedFightTime <= initialSkillDelayMilliseconds)
		{
			return PlayerSummonKnownObjectSkillAttackPreview.InitialDelayNotElapsed(
				knownObject,
				currentTimeMilliseconds,
				elapsedFightTime,
				initialSkillDelayMilliseconds);
		}

		var nextSkillDelay = knownObject.NextSkillDelayMilliseconds ?? 0;
		var readiness = EvaluateMercenaryNextSkillReadiness(knownObject, nextSkillDelay, currentTimeMilliseconds);
		return readiness.Status == PlayerSummonKnownObjectNextSkillReadinessStatus.Ready
			? PlayerSummonKnownObjectSkillAttackPreview.WouldEvaluateSkills(knownObject, currentTimeMilliseconds, readiness)
			: PlayerSummonKnownObjectSkillAttackPreview.NextSkillNotReady(knownObject, currentTimeMilliseconds, readiness);
	}

	public PlayerSummonSkillExecutionResult ValidateExecution(
		Player player,
		PlayerPetSkillOrder order,
		PetSkillTable petSkills,
		PlayerSummonCastSpellTarget? resolvedTarget = null)
	{
		// Java parity: controllers/SummonController.useSkill(SkillOrder) petHasSkill guard before SkillEngine invocation.
		if (!player.HasPetSummon || player.PetSummonNpcId == 0)
			return PlayerSummonSkillExecutionResult.MissingSummon(order, resolvedTarget);

		if (!petSkills.PetHasSkill(player.PetSummonNpcId, order.SkillId))
			return PlayerSummonSkillExecutionResult.InvalidPetSkill(player.PetSummonNpcId, order, resolvedTarget);

		return PlayerSummonSkillExecutionResult.WouldInvokeSkillEngine(
			player.PetSummonObjectId,
			player.PetSummonNpcId,
			order,
			resolvedTarget,
			order.Release
				? [
					PlayerSummonSkillExecutionAction.GetSkill,
					PlayerSummonSkillExecutionAction.SetHate,
					PlayerSummonSkillExecutionAction.UseSkill,
					PlayerSummonSkillExecutionAction.ReleaseOnSuccess,
				]
				: [
					PlayerSummonSkillExecutionAction.GetSkill,
					PlayerSummonSkillExecutionAction.SetHate,
					PlayerSummonSkillExecutionAction.UseSkill,
				]);
	}

	public PlayerMercenarySkillExecutionResult ValidateMercenaryExecution(
		Player player,
		CmSummonCastSpell packet,
		PetSkillTable petSkills,
		PlayerSummonCastSpellTarget? resolvedTarget = null)
	{
		// Java parity: CM_SUMMON_CASTSPELL mercenary branch checks petHasSkill before controller.useSkill(skillId, skillLvl).
		if (player.GetSummonOrMercenaryKind(packet.SummonObjectId) != PlayerSummonOrMercenaryKind.Mercenary)
			return PlayerMercenarySkillExecutionResult.MissingMercenary(packet.SkillId, packet.SkillLevel, packet.TargetObjectId, resolvedTarget);

		var mercenaryNpcId = player.GetSummonOrMercenaryNpcId(packet.SummonObjectId);
		if (mercenaryNpcId == 0)
			return PlayerMercenarySkillExecutionResult.MissingMercenary(packet.SkillId, packet.SkillLevel, packet.TargetObjectId, resolvedTarget);

		if (!petSkills.PetHasSkill(mercenaryNpcId, packet.SkillId))
			return PlayerMercenarySkillExecutionResult.InvalidMercenarySkill(
				mercenaryNpcId,
				packet.SkillId,
				packet.SkillLevel,
				packet.TargetObjectId,
				resolvedTarget);

		return PlayerMercenarySkillExecutionResult.WouldInvokeController(
			packet.SummonObjectId,
			mercenaryNpcId,
			packet.SkillId,
			packet.SkillLevel,
			packet.TargetObjectId,
			resolvedTarget);
	}
}

public sealed record PlayerSummonSkillExecutionResult(
	PlayerSummonSkillExecutionStatus Status,
	int PetSummonNpcId,
	PlayerPetSkillOrder Order,
	PlayerSummonCastSpellTarget? ResolvedTarget = null,
	IReadOnlyList<PlayerSummonSkillExecutionAction>? PlannedActions = null,
	PlayerSummonSkillInvocationPlan? InvocationPlan = null,
	PlayerSummonSkillInvocationExecutionResult? InvocationExecution = null)
{
	public IReadOnlyList<PlayerSummonSkillExecutionAction> Actions => PlannedActions ?? Array.Empty<PlayerSummonSkillExecutionAction>();

	public static PlayerSummonSkillExecutionResult MissingSummon(
		PlayerPetSkillOrder order,
		PlayerSummonCastSpellTarget? resolvedTarget)
	{
		return new PlayerSummonSkillExecutionResult(
			PlayerSummonSkillExecutionStatus.MissingSummon,
			0,
			order,
			resolvedTarget);
	}

	public static PlayerSummonSkillExecutionResult InvalidPetSkill(
		int petSummonNpcId,
		PlayerPetSkillOrder order,
		PlayerSummonCastSpellTarget? resolvedTarget)
	{
		return new PlayerSummonSkillExecutionResult(
			PlayerSummonSkillExecutionStatus.InvalidPetSkill,
			petSummonNpcId,
			order,
			resolvedTarget);
	}

	public static PlayerSummonSkillExecutionResult WouldInvokeSkillEngine(
		int petSummonObjectId,
		int petSummonNpcId,
		PlayerPetSkillOrder order,
		PlayerSummonCastSpellTarget? resolvedTarget,
		IReadOnlyList<PlayerSummonSkillExecutionAction> plannedActions)
	{
		// Java parity: SummonController.useSkill(SkillOrder) passes level 1 into SkillEngine.getSkill.
		var invocationPlan = new PlayerSummonSkillInvocationPlan(
			PlayerSummonSkillInvocationActorKind.Summon,
			petSummonObjectId,
			petSummonNpcId,
			order.SkillId,
			SkillLevel: 1,
			resolvedTarget,
			order.Hate,
			ReleaseOnSuccess: order.Release);

		return new PlayerSummonSkillExecutionResult(
			PlayerSummonSkillExecutionStatus.WouldInvokeSkillEngine,
			petSummonNpcId,
			order,
			resolvedTarget,
			plannedActions,
			invocationPlan);
	}
}

public enum PlayerSummonSkillExecutionStatus
{
	MissingSummon,
	InvalidPetSkill,
	WouldInvokeSkillEngine,
}

public enum PlayerSummonSkillExecutionAction
{
	GetSkill,
	SetHate,
	UseSkill,
	ReleaseOnSuccess,
}

public sealed record PlayerSummonSkillInvocationPlan(
	PlayerSummonSkillInvocationActorKind ActorKind,
	int ActorObjectId,
	int ActorTemplateId,
	int SkillId,
	int SkillLevel,
	PlayerSummonCastSpellTarget? Target,
	int Hate,
	bool ReleaseOnSuccess);

public enum PlayerSummonSkillInvocationActorKind
{
	Summon,
	Mercenary,
}

public sealed record PlayerMercenarySkillExecutionResult(
	PlayerMercenarySkillExecutionStatus Status,
	int MercenaryNpcId,
	int SkillId,
	int SkillLevel,
	int TargetObjectId,
	PlayerSummonCastSpellTarget? ResolvedTarget = null,
	IReadOnlyList<PlayerMercenarySkillExecutionAction>? PlannedActions = null,
	PlayerMercenarySkillExecutionAudit? Audit = null,
	PlayerSummonSkillInvocationPlan? InvocationPlan = null,
	PlayerSummonSkillInvocationExecutionResult? InvocationExecution = null)
{
	public IReadOnlyList<PlayerMercenarySkillExecutionAction> Actions => PlannedActions ?? Array.Empty<PlayerMercenarySkillExecutionAction>();

	public static PlayerMercenarySkillExecutionResult MissingMercenary(
		int skillId,
		int skillLevel,
		int targetObjectId,
		PlayerSummonCastSpellTarget? resolvedTarget)
	{
		return new PlayerMercenarySkillExecutionResult(
			PlayerMercenarySkillExecutionStatus.MissingMercenary,
			0,
			skillId,
			skillLevel,
			targetObjectId,
			resolvedTarget);
	}

	public static PlayerMercenarySkillExecutionResult InvalidMercenarySkill(
		int mercenaryNpcId,
		int skillId,
		int skillLevel,
		int targetObjectId,
		PlayerSummonCastSpellTarget? resolvedTarget)
	{
		var audit = new PlayerMercenarySkillExecutionAudit(PlayerMercenarySkillExecutionAuditKind.InvalidMercenarySkill, skillId);
		return new PlayerMercenarySkillExecutionResult(
			PlayerMercenarySkillExecutionStatus.InvalidMercenarySkill,
			mercenaryNpcId,
			skillId,
			skillLevel,
			targetObjectId,
			resolvedTarget,
			Audit: audit);
	}

	public static PlayerMercenarySkillExecutionResult WouldInvokeController(
		int mercenaryObjectId,
		int mercenaryNpcId,
		int skillId,
		int skillLevel,
		int targetObjectId,
		PlayerSummonCastSpellTarget? resolvedTarget)
	{
		var invocationPlan = new PlayerSummonSkillInvocationPlan(
			PlayerSummonSkillInvocationActorKind.Mercenary,
			mercenaryObjectId,
			mercenaryNpcId,
			skillId,
			skillLevel,
			resolvedTarget,
			Hate: 0,
			ReleaseOnSuccess: false);

		return new PlayerMercenarySkillExecutionResult(
			PlayerMercenarySkillExecutionStatus.WouldInvokeController,
			mercenaryNpcId,
			skillId,
			skillLevel,
			targetObjectId,
			resolvedTarget,
			[
				PlayerMercenarySkillExecutionAction.SetTarget,
				PlayerMercenarySkillExecutionAction.UseSkill,
			],
			InvocationPlan: invocationPlan);
	}
}

public sealed record PlayerSummonSkillInvocationExecutionResult(
	PlayerSummonSkillInvocationExecutionStatus Status,
	PlayerSummonSkillInvocationPlan? InvocationPlan = null,
	int? SkillTemplateId = null,
	int? SkillCooldownId = null,
	bool WouldRenewLastSkillTime = false,
	IReadOnlyList<PlayerSummonSkillInvocationExecutionAction>? PlannedActions = null)
{
	public IReadOnlyList<PlayerSummonSkillInvocationExecutionAction> Actions => PlannedActions
		?? Array.Empty<PlayerSummonSkillInvocationExecutionAction>();

	public static PlayerSummonSkillInvocationExecutionResult MissingPlan()
	{
		return new PlayerSummonSkillInvocationExecutionResult(PlayerSummonSkillInvocationExecutionStatus.MissingPlan);
	}

	public static PlayerSummonSkillInvocationExecutionResult MissingSkillTemplate(PlayerSummonSkillInvocationPlan invocationPlan)
	{
		return new PlayerSummonSkillInvocationExecutionResult(
			PlayerSummonSkillInvocationExecutionStatus.MissingSkillTemplate,
			invocationPlan);
	}

	public static PlayerSummonSkillInvocationExecutionResult WouldUseSkill(
		PlayerSummonSkillInvocationPlan invocationPlan,
		int skillTemplateId,
		int skillCooldownId)
	{
		return new PlayerSummonSkillInvocationExecutionResult(
			PlayerSummonSkillInvocationExecutionStatus.WouldUseSkill,
			invocationPlan,
			skillTemplateId,
			skillCooldownId,
			WouldRenewLastSkillTime: invocationPlan.ActorKind == PlayerSummonSkillInvocationActorKind.Mercenary,
			PlannedActions: CreateActions(invocationPlan));
	}

	public static PlayerSummonSkillInvocationExecutionResult DisabledNpcSkill(
		PlayerSummonSkillInvocationPlan invocationPlan,
		int skillTemplateId,
		int skillCooldownId)
	{
		return new PlayerSummonSkillInvocationExecutionResult(
			PlayerSummonSkillInvocationExecutionStatus.DisabledNpcSkill,
			invocationPlan,
			skillTemplateId,
			skillCooldownId);
	}

	private static IReadOnlyList<PlayerSummonSkillInvocationExecutionAction> CreateActions(PlayerSummonSkillInvocationPlan invocationPlan)
	{
		if (invocationPlan.ActorKind == PlayerSummonSkillInvocationActorKind.Mercenary)
		{
			return
			[
				PlayerSummonSkillInvocationExecutionAction.SetTarget,
				PlayerSummonSkillInvocationExecutionAction.ResolveSkillTemplate,
				PlayerSummonSkillInvocationExecutionAction.RenewLastSkillTime,
				PlayerSummonSkillInvocationExecutionAction.UseSkill,
			];
		}

		return invocationPlan.ReleaseOnSuccess
			? [
				PlayerSummonSkillInvocationExecutionAction.ResolveSkillTemplate,
				PlayerSummonSkillInvocationExecutionAction.SetHate,
				PlayerSummonSkillInvocationExecutionAction.UseSkill,
				PlayerSummonSkillInvocationExecutionAction.ReleaseOnSuccessfulUse,
			]
			: [
				PlayerSummonSkillInvocationExecutionAction.ResolveSkillTemplate,
				PlayerSummonSkillInvocationExecutionAction.SetHate,
				PlayerSummonSkillInvocationExecutionAction.UseSkill,
			];
	}
}

public enum PlayerSummonSkillInvocationExecutionStatus
{
	MissingPlan,
	MissingSkillTemplate,
	DisabledNpcSkill,
	WouldUseSkill,
}

public enum PlayerSummonSkillInvocationExecutionAction
{
	ResolveSkillTemplate,
	SetTarget,
	RenewLastSkillTime,
	SetHate,
	UseSkill,
	ReleaseOnSuccessfulUse,
}

public sealed record PlayerSummonSkillInvocationUseResult(
	PlayerSummonSkillInvocationUseStatus Status,
	PlayerSummonSkillInvocationExecutionResult? ExecutionResult = null,
	bool SkillUseSucceeded = false,
	bool ShouldReleaseSummon = false)
{
	public static PlayerSummonSkillInvocationUseResult MissingExecution()
	{
		return new PlayerSummonSkillInvocationUseResult(PlayerSummonSkillInvocationUseStatus.MissingExecution);
	}

	public static PlayerSummonSkillInvocationUseResult NotReadyToUseSkill(PlayerSummonSkillInvocationExecutionResult executionResult)
	{
		return new PlayerSummonSkillInvocationUseResult(
			PlayerSummonSkillInvocationUseStatus.NotReadyToUseSkill,
			executionResult);
	}

	public static PlayerSummonSkillInvocationUseResult SkillUseFailed(PlayerSummonSkillInvocationExecutionResult executionResult)
	{
		return new PlayerSummonSkillInvocationUseResult(
			PlayerSummonSkillInvocationUseStatus.SkillUseFailed,
			executionResult);
	}

	public static PlayerSummonSkillInvocationUseResult WouldCompleteWithoutRelease(PlayerSummonSkillInvocationExecutionResult executionResult)
	{
		return new PlayerSummonSkillInvocationUseResult(
			PlayerSummonSkillInvocationUseStatus.WouldCompleteWithoutRelease,
			executionResult,
			SkillUseSucceeded: true);
	}

	public static PlayerSummonSkillInvocationUseResult WouldReleaseSummon(PlayerSummonSkillInvocationExecutionResult executionResult)
	{
		return new PlayerSummonSkillInvocationUseResult(
			PlayerSummonSkillInvocationUseStatus.WouldReleaseSummon,
			executionResult,
			SkillUseSucceeded: true,
			ShouldReleaseSummon: true);
	}
}

public enum PlayerSummonSkillInvocationUseStatus
{
	MissingExecution,
	NotReadyToUseSkill,
	SkillUseFailed,
	WouldCompleteWithoutRelease,
	WouldReleaseSummon,
}

public sealed record PlayerSummonKnownObjectLastSkillTimeRenewalResult(
	PlayerSummonKnownObjectLastSkillTimeRenewalStatus Status,
	PlayerSummonSkillInvocationExecutionResult? ExecutionResult = null,
	long? LastSkillTimeMilliseconds = null)
{
	public static PlayerSummonKnownObjectLastSkillTimeRenewalResult MissingExecution()
	{
		return new PlayerSummonKnownObjectLastSkillTimeRenewalResult(PlayerSummonKnownObjectLastSkillTimeRenewalStatus.MissingExecution);
	}

	public static PlayerSummonKnownObjectLastSkillTimeRenewalResult NotRenewable(
		PlayerSummonSkillInvocationExecutionResult executionResult)
	{
		return new PlayerSummonKnownObjectLastSkillTimeRenewalResult(
			PlayerSummonKnownObjectLastSkillTimeRenewalStatus.NotRenewable,
			executionResult);
	}

	public static PlayerSummonKnownObjectLastSkillTimeRenewalResult MissingKnownObject(
		PlayerSummonSkillInvocationExecutionResult executionResult)
	{
		return new PlayerSummonKnownObjectLastSkillTimeRenewalResult(
			PlayerSummonKnownObjectLastSkillTimeRenewalStatus.MissingKnownObject,
			executionResult);
	}

	public static PlayerSummonKnownObjectLastSkillTimeRenewalResult Renewed(
		PlayerSummonSkillInvocationExecutionResult executionResult,
		long lastSkillTimeMilliseconds)
	{
		return new PlayerSummonKnownObjectLastSkillTimeRenewalResult(
			PlayerSummonKnownObjectLastSkillTimeRenewalStatus.Renewed,
			executionResult,
			lastSkillTimeMilliseconds);
	}
}

public enum PlayerSummonKnownObjectLastSkillTimeRenewalStatus
{
	MissingExecution,
	NotRenewable,
	MissingKnownObject,
	Renewed,
}

public sealed record PlayerSummonKnownObjectNextSkillReadiness(
	PlayerSummonKnownObjectNextSkillReadinessStatus Status,
	PlayerSummonKnownObject KnownObject,
	int NextSkillDelayMilliseconds,
	long CurrentTimeMilliseconds,
	long? ReadyAtMilliseconds = null)
{
	public static PlayerSummonKnownObjectNextSkillReadiness Ready(
		PlayerSummonKnownObject knownObject,
		int nextSkillDelayMilliseconds,
		long currentTimeMilliseconds,
		long? readyAtMilliseconds = null)
	{
		return new PlayerSummonKnownObjectNextSkillReadiness(
			PlayerSummonKnownObjectNextSkillReadinessStatus.Ready,
			knownObject,
			nextSkillDelayMilliseconds,
			currentTimeMilliseconds,
			readyAtMilliseconds);
	}

	public static PlayerSummonKnownObjectNextSkillReadiness NotReady(
		PlayerSummonKnownObject knownObject,
		int nextSkillDelayMilliseconds,
		long currentTimeMilliseconds,
		long readyAtMilliseconds)
	{
		return new PlayerSummonKnownObjectNextSkillReadiness(
			PlayerSummonKnownObjectNextSkillReadinessStatus.NotReady,
			knownObject,
			nextSkillDelayMilliseconds,
			currentTimeMilliseconds,
			readyAtMilliseconds);
	}

	public static PlayerSummonKnownObjectNextSkillReadiness RandomDelayUnsupported(
		PlayerSummonKnownObject knownObject,
		int nextSkillDelayMilliseconds)
	{
		return new PlayerSummonKnownObjectNextSkillReadiness(
			PlayerSummonKnownObjectNextSkillReadinessStatus.RandomDelayUnsupported,
			knownObject,
			nextSkillDelayMilliseconds,
			CurrentTimeMilliseconds: 0);
	}
}

public enum PlayerSummonKnownObjectNextSkillReadinessStatus
{
	Ready,
	NotReady,
	RandomDelayUnsupported,
}

public sealed record PlayerSummonKnownObjectNextSkillDelayResult(
	PlayerSummonKnownObjectNextSkillDelayStatus Status,
	int MercenaryObjectId,
	int RequestedDelayMilliseconds,
	int? StoredDelayMilliseconds = null)
{
	public static PlayerSummonKnownObjectNextSkillDelayResult Set(int mercenaryObjectId, int delayMilliseconds)
	{
		return new PlayerSummonKnownObjectNextSkillDelayResult(
			PlayerSummonKnownObjectNextSkillDelayStatus.Set,
			mercenaryObjectId,
			delayMilliseconds,
			delayMilliseconds);
	}

	public static PlayerSummonKnownObjectNextSkillDelayResult MissingKnownObject(
		int mercenaryObjectId,
		int delayMilliseconds)
	{
		return new PlayerSummonKnownObjectNextSkillDelayResult(
			PlayerSummonKnownObjectNextSkillDelayStatus.MissingKnownObject,
			mercenaryObjectId,
			delayMilliseconds);
	}

	public static PlayerSummonKnownObjectNextSkillDelayResult RandomDelayUnsupported(
		int mercenaryObjectId,
		int delayMilliseconds)
	{
		return new PlayerSummonKnownObjectNextSkillDelayResult(
			PlayerSummonKnownObjectNextSkillDelayStatus.RandomDelayUnsupported,
			mercenaryObjectId,
			delayMilliseconds);
	}
}

public enum PlayerSummonKnownObjectNextSkillDelayStatus
{
	Set,
	MissingKnownObject,
	RandomDelayUnsupported,
}

public sealed record PlayerSummonKnownObjectSkillReadiness(
	PlayerSummonKnownObjectSkillReadinessStatus Status,
	PlayerSummonKnownObject KnownObject,
	SkillTemplateSummary? SkillTemplate = null,
	PlayerSummonKnownObjectNpcSkillEntryReadiness? EntryTimingReadiness = null,
	PlayerSummonKnownObjectNpcSkillConditionReadiness? EntryConditionReadiness = null)
{
	public static PlayerSummonKnownObjectSkillReadiness EntryTimingNotReady(
		PlayerSummonKnownObject knownObject,
		SkillTemplateSummary? skillTemplate,
		PlayerSummonKnownObjectNpcSkillEntryReadiness? entryTimingReadiness = null,
		PlayerSummonKnownObjectNpcSkillConditionReadiness? entryConditionReadiness = null)
	{
		return new PlayerSummonKnownObjectSkillReadiness(
			PlayerSummonKnownObjectSkillReadinessStatus.EntryTimingNotReady,
			knownObject,
			skillTemplate,
			entryTimingReadiness,
			entryConditionReadiness);
	}

	public static PlayerSummonKnownObjectSkillReadiness EntryConditionNotReady(
		PlayerSummonKnownObject knownObject,
		SkillTemplateSummary? skillTemplate,
		PlayerSummonKnownObjectNpcSkillEntryReadiness? entryTimingReadiness = null,
		PlayerSummonKnownObjectNpcSkillConditionReadiness? entryConditionReadiness = null)
	{
		return new PlayerSummonKnownObjectSkillReadiness(
			PlayerSummonKnownObjectSkillReadinessStatus.EntryConditionNotReady,
			knownObject,
			skillTemplate,
			entryTimingReadiness,
			entryConditionReadiness);
	}

	public static PlayerSummonKnownObjectSkillReadiness MissingSkillTemplate(
		PlayerSummonKnownObject knownObject,
		PlayerSummonKnownObjectNpcSkillEntryReadiness? entryTimingReadiness = null,
		PlayerSummonKnownObjectNpcSkillConditionReadiness? entryConditionReadiness = null)
	{
		return new PlayerSummonKnownObjectSkillReadiness(
			PlayerSummonKnownObjectSkillReadinessStatus.MissingSkillTemplate,
			knownObject,
			EntryTimingReadiness: entryTimingReadiness,
			EntryConditionReadiness: entryConditionReadiness);
	}

	public static PlayerSummonKnownObjectSkillReadiness BlockedBySilence(
		PlayerSummonKnownObject knownObject,
		SkillTemplateSummary skillTemplate,
		PlayerSummonKnownObjectNpcSkillEntryReadiness? entryTimingReadiness = null,
		PlayerSummonKnownObjectNpcSkillConditionReadiness? entryConditionReadiness = null)
	{
		return new PlayerSummonKnownObjectSkillReadiness(
			PlayerSummonKnownObjectSkillReadinessStatus.BlockedBySilence,
			knownObject,
			skillTemplate,
			entryTimingReadiness,
			entryConditionReadiness);
	}

	public static PlayerSummonKnownObjectSkillReadiness BlockedByBind(
		PlayerSummonKnownObject knownObject,
		SkillTemplateSummary skillTemplate,
		PlayerSummonKnownObjectNpcSkillEntryReadiness? entryTimingReadiness = null,
		PlayerSummonKnownObjectNpcSkillConditionReadiness? entryConditionReadiness = null)
	{
		return new PlayerSummonKnownObjectSkillReadiness(
			PlayerSummonKnownObjectSkillReadinessStatus.BlockedByBind,
			knownObject,
			skillTemplate,
			entryTimingReadiness,
			entryConditionReadiness);
	}

	public static PlayerSummonKnownObjectSkillReadiness BlockedByCantAttackState(
		PlayerSummonKnownObject knownObject,
		SkillTemplateSummary skillTemplate,
		PlayerSummonKnownObjectNpcSkillEntryReadiness? entryTimingReadiness = null,
		PlayerSummonKnownObjectNpcSkillConditionReadiness? entryConditionReadiness = null)
	{
		return new PlayerSummonKnownObjectSkillReadiness(
			PlayerSummonKnownObjectSkillReadinessStatus.BlockedByCantAttackState,
			knownObject,
			skillTemplate,
			entryTimingReadiness,
			entryConditionReadiness);
	}

	public static PlayerSummonKnownObjectSkillReadiness BlockedByTransformSkillBan(
		PlayerSummonKnownObject knownObject,
		SkillTemplateSummary skillTemplate,
		PlayerSummonKnownObjectNpcSkillEntryReadiness? entryTimingReadiness = null,
		PlayerSummonKnownObjectNpcSkillConditionReadiness? entryConditionReadiness = null)
	{
		return new PlayerSummonKnownObjectSkillReadiness(
			PlayerSummonKnownObjectSkillReadinessStatus.BlockedByTransformSkillBan,
			knownObject,
			skillTemplate,
			entryTimingReadiness,
			entryConditionReadiness);
	}

	public static PlayerSummonKnownObjectSkillReadiness Ready(
		PlayerSummonKnownObject knownObject,
		SkillTemplateSummary skillTemplate,
		PlayerSummonKnownObjectNpcSkillEntryReadiness? entryTimingReadiness = null,
		PlayerSummonKnownObjectNpcSkillConditionReadiness? entryConditionReadiness = null)
	{
		return new PlayerSummonKnownObjectSkillReadiness(
			PlayerSummonKnownObjectSkillReadinessStatus.Ready,
			knownObject,
			skillTemplate,
			entryTimingReadiness,
			entryConditionReadiness);
	}
}

public enum PlayerSummonKnownObjectSkillReadinessStatus
{
	EntryTimingNotReady,
	EntryConditionNotReady,
	MissingSkillTemplate,
	BlockedBySilence,
	BlockedByBind,
	BlockedByCantAttackState,
	BlockedByTransformSkillBan,
	Ready,
}

public sealed record PlayerSummonKnownObjectNpcSkillTemplateMetadata(
	int SkillId = 0,
	int SkillLevel = 0,
	int Probability = 0,
	int MinHpPercentage = 0,
	int MaxHpPercentage = 100,
	long MaxTimeMilliseconds = 0,
	long MinTimeMilliseconds = 0,
	PlayerSummonKnownObjectNpcSkillConjunction ConjunctionType = PlayerSummonKnownObjectNpcSkillConjunction.And,
	long CooldownMilliseconds = 0,
	bool IsPostSpawn = false,
	int Priority = 0,
	int NextSkillTimeMilliseconds = -1,
	PlayerSummonKnownObjectNpcSkillConditionMetadata? ConditionTemplate = null,
	int NextChainId = 0,
	int ChainId = 0,
	int MaxChainTimeMilliseconds = 15000,
	PlayerSummonKnownObjectNpcSkillTargetAttribute Target = PlayerSummonKnownObjectNpcSkillTargetAttribute.MostHated);

public sealed record PlayerSummonKnownObjectNpcSkillTemplateProjection(
	PlayerSummonKnownObjectNpcSkillEntryTiming EntryTiming,
	PlayerSummonKnownObjectNpcSkillConditionMetadata ConditionTemplate,
	PlayerSummonKnownObjectSkillTargetMode TargetMode,
	int Probability,
	int Priority,
	int NextSkillTimeMilliseconds,
	int NextChainId,
	int ChainId,
	int MaxChainTimeMilliseconds,
	bool IsPostSpawn);

public sealed record PlayerSummonKnownObjectNpcSkillCandidate(
	int Position,
	PlayerSummonKnownObjectNpcSkillTemplateProjection Projection,
	PlayerSummonKnownObjectNpcSkillEntryReadiness EntryTimingReadiness,
	PlayerSummonKnownObjectNpcSkillConditionReadiness EntryConditionReadiness,
	PlayerSummonKnownObjectTargetRangeReadiness? TargetRangeReadiness = null);

public sealed record PlayerSummonKnownObjectNpcSkillSelectionPreview(
	PlayerSummonKnownObject KnownObject,
	long CurrentTimeMilliseconds,
	long ElapsedFightTimeMilliseconds,
	int InitialSkillDelayMilliseconds,
	bool InitialSkillDelayElapsed,
	PlayerSummonKnownObjectNextSkillReadiness NextSkillReadiness,
	long ElapsedSinceLastSkillMilliseconds,
	PlayerSummonKnownObjectNpcSkillSelectionResult Selection);

public sealed record PlayerSummonKnownObjectNpcSkillSelectionResult(
	PlayerSummonKnownObjectNpcSkillSelectionStatus Status,
	PlayerSummonKnownObjectNpcSkillCandidate? Candidate = null,
	PlayerSummonKnownObjectNpcSkillSelectionSource Source = PlayerSummonKnownObjectNpcSkillSelectionSource.OrdinaryPriority)
{
	public static PlayerSummonKnownObjectNpcSkillSelectionResult InCastSubState()
	{
		return new PlayerSummonKnownObjectNpcSkillSelectionResult(
			PlayerSummonKnownObjectNpcSkillSelectionStatus.InCastSubState,
			Source: PlayerSummonKnownObjectNpcSkillSelectionSource.ChooseNextSkillGate);
	}

	public static PlayerSummonKnownObjectNpcSkillSelectionResult Empty()
	{
		return new PlayerSummonKnownObjectNpcSkillSelectionResult(PlayerSummonKnownObjectNpcSkillSelectionStatus.Empty);
	}

	public static PlayerSummonKnownObjectNpcSkillSelectionResult NoReadyCandidate(
		PlayerSummonKnownObjectNpcSkillSelectionSource source = PlayerSummonKnownObjectNpcSkillSelectionSource.OrdinaryPriority)
	{
		return new PlayerSummonKnownObjectNpcSkillSelectionResult(
			PlayerSummonKnownObjectNpcSkillSelectionStatus.NoReadyCandidate,
			Source: source);
	}

	public static PlayerSummonKnownObjectNpcSkillSelectionResult WaitingForDelayGate(
		PlayerSummonKnownObjectNpcSkillCandidate? candidate,
		PlayerSummonKnownObjectNpcSkillSelectionSource source = PlayerSummonKnownObjectNpcSkillSelectionSource.DelayedQueuedSkill)
	{
		return new PlayerSummonKnownObjectNpcSkillSelectionResult(
			PlayerSummonKnownObjectNpcSkillSelectionStatus.WaitingForDelayGate,
			candidate,
			source);
	}

	public static PlayerSummonKnownObjectNpcSkillSelectionResult TargetRangeNotReady(
		PlayerSummonKnownObjectNpcSkillCandidate candidate,
		PlayerSummonKnownObjectNpcSkillSelectionSource source = PlayerSummonKnownObjectNpcSkillSelectionSource.OrdinaryPriority)
	{
		return new PlayerSummonKnownObjectNpcSkillSelectionResult(
			PlayerSummonKnownObjectNpcSkillSelectionStatus.TargetRangeNotReady,
			candidate,
			source);
	}

	public static PlayerSummonKnownObjectNpcSkillSelectionResult Ready(
		PlayerSummonKnownObjectNpcSkillCandidate candidate,
		PlayerSummonKnownObjectNpcSkillSelectionSource source = PlayerSummonKnownObjectNpcSkillSelectionSource.OrdinaryPriority)
	{
		return new PlayerSummonKnownObjectNpcSkillSelectionResult(
			PlayerSummonKnownObjectNpcSkillSelectionStatus.Ready,
			candidate,
			source);
	}
}

public enum PlayerSummonKnownObjectNpcSkillSelectionStatus
{
	InCastSubState,
	Empty,
	NoReadyCandidate,
	WaitingForDelayGate,
	TargetRangeNotReady,
	Ready,
}

public enum PlayerSummonKnownObjectNpcSkillSelectionSource
{
	ChooseNextSkillGate,
	OrdinaryPriority,
	ImmediateQueuedSkill,
	DelayedQueuedSkill,
	ChainSkill,
}

public sealed record PlayerSummonKnownObjectNpcSkillConditionMetadata(
	PlayerSummonKnownObjectNpcSkillCondition Condition = PlayerSummonKnownObjectNpcSkillCondition.None,
	int HpBelowPercentage = 50,
	int RangeMeters = 10,
	int NpcId = 0,
	int DelayMilliseconds = 0,
	bool CanDie = true,
	int DespawnTimeMilliseconds = 500);

public sealed record PlayerSummonKnownObjectNpcSkillEntryTiming(
	int MinHpPercentage = 0,
	int MaxHpPercentage = 100,
	long MinTimeMilliseconds = 0,
	long MaxTimeMilliseconds = 0,
	PlayerSummonKnownObjectNpcSkillConjunction ConjunctionType = PlayerSummonKnownObjectNpcSkillConjunction.And,
	long CooldownMilliseconds = 0,
	long LastTimeUsedMilliseconds = 0);

public enum PlayerSummonKnownObjectNpcSkillConjunction
{
	And,
	Or,
	Xor,
}

public enum PlayerSummonKnownObjectNpcSkillTargetAttribute
{
	Friend,
	Me,
	MostHated,
	SecondMostHated,
	ThirdMostHated,
	Random,
	RandomExceptCurrentTarget,
	None,
}

public sealed record PlayerSummonKnownObjectNpcSkillActionTargetSelection(
	PlayerSummonKnownObjectNpcSkillActionTargetSelectionStatus Status,
	PlayerSummonKnownObjectNpcSkillActionTargetSource Source)
{
	public bool ShouldSetOwnerTarget => Status == PlayerSummonKnownObjectNpcSkillActionTargetSelectionStatus.Selected
		&& Source != PlayerSummonKnownObjectNpcSkillActionTargetSource.None;

	public static PlayerSummonKnownObjectNpcSkillActionTargetSelection NotRequired(PlayerSummonKnownObjectNpcSkillActionTargetSource source)
	{
		return new PlayerSummonKnownObjectNpcSkillActionTargetSelection(
			PlayerSummonKnownObjectNpcSkillActionTargetSelectionStatus.NotRequired,
			source);
	}

	public static PlayerSummonKnownObjectNpcSkillActionTargetSelection MissingTarget(PlayerSummonKnownObjectNpcSkillActionTargetSource source)
	{
		return new PlayerSummonKnownObjectNpcSkillActionTargetSelection(
			PlayerSummonKnownObjectNpcSkillActionTargetSelectionStatus.MissingTarget,
			source);
	}

	public static PlayerSummonKnownObjectNpcSkillActionTargetSelection Selected(PlayerSummonKnownObjectNpcSkillActionTargetSource source)
	{
		return new PlayerSummonKnownObjectNpcSkillActionTargetSelection(
			PlayerSummonKnownObjectNpcSkillActionTargetSelectionStatus.Selected,
			source);
	}
}

public enum PlayerSummonKnownObjectNpcSkillActionTargetSelectionStatus
{
	NotRequired,
	MissingTarget,
	Selected,
}

public enum PlayerSummonKnownObjectNpcSkillActionTargetSource
{
	None,
	Owner,
	Friend,
	MostHated,
	SecondMostHated,
	ThirdMostHated,
	Random,
	RandomExceptCurrentTarget,
}

public sealed record PlayerSummonKnownObjectNpcSkillActionPreview(
	PlayerSummonKnownObjectNpcSkillActionPreviewStatus Status,
	PlayerSummonKnownObjectSkillReadiness? SkillReadiness = null,
	PlayerSummonKnownObjectNpcSkillActionTargetSelection? TargetSelection = null)
{
	public bool ShouldSetSubStateNone =>
		Status is PlayerSummonKnownObjectNpcSkillActionPreviewStatus.TargetGiveUp
			or PlayerSummonKnownObjectNpcSkillActionPreviewStatus.AfterUseSkillBlocked
			or PlayerSummonKnownObjectNpcSkillActionPreviewStatus.AfterUseSkillUseFailed;

	public bool ShouldAbortCast => Status == PlayerSummonKnownObjectNpcSkillActionPreviewStatus.TargetTooFar;

	public bool ShouldSetOwnerTarget => TargetSelection?.ShouldSetOwnerTarget == true
		&& Status == PlayerSummonKnownObjectNpcSkillActionPreviewStatus.WouldSetTargetAndUseSkill;

	public bool ShouldUseSkill =>
		Status is PlayerSummonKnownObjectNpcSkillActionPreviewStatus.WouldSetTargetAndUseSkill
			or PlayerSummonKnownObjectNpcSkillActionPreviewStatus.WouldUseSkill
			or PlayerSummonKnownObjectNpcSkillActionPreviewStatus.AfterUseSkillUseFailed;

	public static PlayerSummonKnownObjectNpcSkillActionPreview NotInCastSubState()
	{
		return new PlayerSummonKnownObjectNpcSkillActionPreview(
			PlayerSummonKnownObjectNpcSkillActionPreviewStatus.NotInCastSubState);
	}

	public static PlayerSummonKnownObjectNpcSkillActionPreview ResumeFightAfterInterruptedCast()
	{
		return new PlayerSummonKnownObjectNpcSkillActionPreview(
			PlayerSummonKnownObjectNpcSkillActionPreviewStatus.ResumeFightAfterInterruptedCast);
	}

	public static PlayerSummonKnownObjectNpcSkillActionPreview TargetGiveUp()
	{
		return new PlayerSummonKnownObjectNpcSkillActionPreview(
			PlayerSummonKnownObjectNpcSkillActionPreviewStatus.TargetGiveUp);
	}

	public static PlayerSummonKnownObjectNpcSkillActionPreview TargetTooFar()
	{
		return new PlayerSummonKnownObjectNpcSkillActionPreview(
			PlayerSummonKnownObjectNpcSkillActionPreviewStatus.TargetTooFar);
	}

	public static PlayerSummonKnownObjectNpcSkillActionPreview AfterUseSkillBlocked(PlayerSummonKnownObjectSkillReadiness skillReadiness)
	{
		return new PlayerSummonKnownObjectNpcSkillActionPreview(
			PlayerSummonKnownObjectNpcSkillActionPreviewStatus.AfterUseSkillBlocked,
			skillReadiness);
	}

	public static PlayerSummonKnownObjectNpcSkillActionPreview AfterUseSkillUseFailed(
		PlayerSummonKnownObjectNpcSkillActionTargetSelection? targetSelection)
	{
		return new PlayerSummonKnownObjectNpcSkillActionPreview(
			PlayerSummonKnownObjectNpcSkillActionPreviewStatus.AfterUseSkillUseFailed,
			TargetSelection: targetSelection);
	}

	public static PlayerSummonKnownObjectNpcSkillActionPreview WouldSetTargetAndUseSkill(
		PlayerSummonKnownObjectNpcSkillActionTargetSelection targetSelection)
	{
		return new PlayerSummonKnownObjectNpcSkillActionPreview(
			PlayerSummonKnownObjectNpcSkillActionPreviewStatus.WouldSetTargetAndUseSkill,
			TargetSelection: targetSelection);
	}

	public static PlayerSummonKnownObjectNpcSkillActionPreview WouldUseSkill(
		PlayerSummonKnownObjectNpcSkillActionTargetSelection? targetSelection)
	{
		return new PlayerSummonKnownObjectNpcSkillActionPreview(
			PlayerSummonKnownObjectNpcSkillActionPreviewStatus.WouldUseSkill,
			TargetSelection: targetSelection);
	}
}

public enum PlayerSummonKnownObjectNpcSkillActionPreviewStatus
{
	NotInCastSubState,
	ResumeFightAfterInterruptedCast,
	TargetGiveUp,
	TargetTooFar,
	AfterUseSkillBlocked,
	WouldSetTargetAndUseSkill,
	WouldUseSkill,
	AfterUseSkillUseFailed,
}

public sealed record PlayerSummonKnownObjectNpcSkillEntryReadiness(
	PlayerSummonKnownObjectNpcSkillEntryReadinessStatus Status,
	PlayerSummonKnownObjectNpcSkillEntryTiming Timing,
	bool HpReady = false,
	bool TimeReady = false)
{
	public static PlayerSummonKnownObjectNpcSkillEntryReadiness OnCooldown(PlayerSummonKnownObjectNpcSkillEntryTiming timing)
	{
		return new PlayerSummonKnownObjectNpcSkillEntryReadiness(
			PlayerSummonKnownObjectNpcSkillEntryReadinessStatus.OnCooldown,
			timing);
	}

	public static PlayerSummonKnownObjectNpcSkillEntryReadiness ChanceNotReady(PlayerSummonKnownObjectNpcSkillEntryTiming timing)
	{
		return new PlayerSummonKnownObjectNpcSkillEntryReadiness(
			PlayerSummonKnownObjectNpcSkillEntryReadinessStatus.ChanceNotReady,
			timing);
	}

	public static PlayerSummonKnownObjectNpcSkillEntryReadiness NotReady(
		PlayerSummonKnownObjectNpcSkillEntryTiming timing,
		bool hpReady,
		bool timeReady)
	{
		return new PlayerSummonKnownObjectNpcSkillEntryReadiness(
			PlayerSummonKnownObjectNpcSkillEntryReadinessStatus.NotReady,
			timing,
			hpReady,
			timeReady);
	}

	public static PlayerSummonKnownObjectNpcSkillEntryReadiness Ready(
		PlayerSummonKnownObjectNpcSkillEntryTiming timing,
		bool hpReady,
		bool timeReady)
	{
		return new PlayerSummonKnownObjectNpcSkillEntryReadiness(
			PlayerSummonKnownObjectNpcSkillEntryReadinessStatus.Ready,
			timing,
			hpReady,
			timeReady);
	}
}

public enum PlayerSummonKnownObjectNpcSkillEntryReadinessStatus
{
	OnCooldown,
	ChanceNotReady,
	NotReady,
	Ready,
}

public sealed record PlayerSummonKnownObjectNpcSkillConditionTarget(
	PlayerSummonKnownObjectNpcSkillConditionTargetKind Kind,
	PlayerAbnormalState AbnormalState = PlayerAbnormalState.None,
	bool IsFlying = false,
	bool? IsPhysicalClass = null,
	bool IsInRange = false)
{
	public bool IsInAnyAbnormalState(PlayerAbnormalState state)
	{
		// Java parity: NpcSkillTemplateEntry.conditionReady delegates target states to EffectController.isInAnyAbnormalState.
		return state == PlayerAbnormalState.None ? AbnormalState == PlayerAbnormalState.None : (AbnormalState & state) != 0;
	}
}

public enum PlayerSummonKnownObjectNpcSkillConditionTargetKind
{
	Unknown,
	Player,
	Npc,
	Gate,
}

public enum PlayerSummonKnownObjectNpcSkillCondition
{
	None,
	HelpFriend,
	TargetIsInAnyStun,
	TargetIsInRange,
	TargetIsInStumble,
	TargetIsStunned,
	TargetIsSleeping,
	TargetIsAethersHold,
	TargetIsPoisoned,
	TargetIsBleeding,
	TargetIsFlying,
	TargetIsGate,
	TargetIsPlayer,
	TargetIsNpc,
	TargetIsPhysicalClass,
	TargetIsMagicalClass,
	TargetHasCarvedSignet,
	TargetHasCarvedSignetLevelIi,
	TargetHasCarvedSignetLevelIii,
	TargetHasCarvedSignetLevelIv,
	TargetHasCarvedSignetLevelV,
	NpcIsAlive,
}

public sealed record PlayerSummonKnownObjectNpcSkillConditionReadiness(
	PlayerSummonKnownObjectNpcSkillConditionReadinessStatus Status,
	PlayerSummonKnownObjectNpcSkillCondition Condition,
	PlayerSummonKnownObjectNpcSkillConditionTarget? Target = null)
{
	public static PlayerSummonKnownObjectNpcSkillConditionReadiness OwnerNotReady(
		PlayerSummonKnownObjectNpcSkillCondition condition,
		PlayerSummonKnownObjectNpcSkillConditionTarget? target)
	{
		return new PlayerSummonKnownObjectNpcSkillConditionReadiness(
			PlayerSummonKnownObjectNpcSkillConditionReadinessStatus.OwnerNotReady,
			condition,
			target);
	}

	public static PlayerSummonKnownObjectNpcSkillConditionReadiness MissingTarget(PlayerSummonKnownObjectNpcSkillCondition condition)
	{
		return new PlayerSummonKnownObjectNpcSkillConditionReadiness(
			PlayerSummonKnownObjectNpcSkillConditionReadinessStatus.MissingTarget,
			condition);
	}

	public static PlayerSummonKnownObjectNpcSkillConditionReadiness Unsupported(
		PlayerSummonKnownObjectNpcSkillCondition condition,
		PlayerSummonKnownObjectNpcSkillConditionTarget? target)
	{
		return new PlayerSummonKnownObjectNpcSkillConditionReadiness(
			PlayerSummonKnownObjectNpcSkillConditionReadinessStatus.Unsupported,
			condition,
			target);
	}

	public static PlayerSummonKnownObjectNpcSkillConditionReadiness NotReady(
		PlayerSummonKnownObjectNpcSkillCondition condition,
		PlayerSummonKnownObjectNpcSkillConditionTarget target)
	{
		return new PlayerSummonKnownObjectNpcSkillConditionReadiness(
			PlayerSummonKnownObjectNpcSkillConditionReadinessStatus.NotReady,
			condition,
			target);
	}

	public static PlayerSummonKnownObjectNpcSkillConditionReadiness Ready(
		PlayerSummonKnownObjectNpcSkillCondition condition,
		PlayerSummonKnownObjectNpcSkillConditionTarget? target)
	{
		return new PlayerSummonKnownObjectNpcSkillConditionReadiness(
			PlayerSummonKnownObjectNpcSkillConditionReadinessStatus.Ready,
			condition,
			target);
	}
}

public enum PlayerSummonKnownObjectNpcSkillConditionReadinessStatus
{
	OwnerNotReady,
	MissingTarget,
	Unsupported,
	NotReady,
	Ready,
}

public sealed record PlayerSummonKnownObjectTargetRangeReadiness(
	PlayerSummonKnownObjectTargetRangeReadinessStatus Status,
	PlayerSummonKnownObject KnownObject,
	int? NextSkillDelayMilliseconds = null)
{
	public bool ShouldSetNextSkillDelay => NextSkillDelayMilliseconds.HasValue;

	public static PlayerSummonKnownObjectTargetRangeReadiness NotRequired(PlayerSummonKnownObject knownObject)
	{
		return new PlayerSummonKnownObjectTargetRangeReadiness(
			PlayerSummonKnownObjectTargetRangeReadinessStatus.NotRequired,
			knownObject);
	}

	public static PlayerSummonKnownObjectTargetRangeReadiness MissingCreatureTarget(
		PlayerSummonKnownObject knownObject,
		int nextSkillDelayMilliseconds)
	{
		return new PlayerSummonKnownObjectTargetRangeReadiness(
			PlayerSummonKnownObjectTargetRangeReadinessStatus.MissingCreatureTarget,
			knownObject,
			nextSkillDelayMilliseconds);
	}

	public static PlayerSummonKnownObjectTargetRangeReadiness TargetDead(
		PlayerSummonKnownObject knownObject,
		int nextSkillDelayMilliseconds)
	{
		return new PlayerSummonKnownObjectTargetRangeReadiness(
			PlayerSummonKnownObjectTargetRangeReadinessStatus.TargetDead,
			knownObject,
			nextSkillDelayMilliseconds);
	}

	public static PlayerSummonKnownObjectTargetRangeReadiness CannotSeeTarget(
		PlayerSummonKnownObject knownObject,
		int nextSkillDelayMilliseconds)
	{
		return new PlayerSummonKnownObjectTargetRangeReadiness(
			PlayerSummonKnownObjectTargetRangeReadinessStatus.CannotSeeTarget,
			knownObject,
			nextSkillDelayMilliseconds);
	}

	public static PlayerSummonKnownObjectTargetRangeReadiness TargetOutOfRange(
		PlayerSummonKnownObject knownObject,
		int nextSkillDelayMilliseconds)
	{
		return new PlayerSummonKnownObjectTargetRangeReadiness(
			PlayerSummonKnownObjectTargetRangeReadinessStatus.TargetOutOfRange,
			knownObject,
			nextSkillDelayMilliseconds);
	}

	public static PlayerSummonKnownObjectTargetRangeReadiness Ready(PlayerSummonKnownObject knownObject)
	{
		return new PlayerSummonKnownObjectTargetRangeReadiness(
			PlayerSummonKnownObjectTargetRangeReadinessStatus.Ready,
			knownObject);
	}
}

public enum PlayerSummonKnownObjectTargetRangeReadinessStatus
{
	NotRequired,
	MissingCreatureTarget,
	TargetDead,
	CannotSeeTarget,
	TargetOutOfRange,
	Ready,
}

public enum PlayerSummonKnownObjectSkillTargetMode
{
	// Java parity: SkillAttackManager.targetTooFar skips explicit range checks for firstTarget ME
	// and NPC skill targets NONE, MOST_HATED, or ME.
	SkipRangeCheck,
	None,
	MostHated,
	Self,
	CreatureTarget,
}

public sealed record PlayerSummonKnownObjectTargetRangeDelayResult(
	PlayerSummonKnownObjectTargetRangeDelayStatus Status,
	int MercenaryObjectId,
	PlayerSummonKnownObjectTargetRangeReadiness? TargetRangeReadiness = null,
	int? StoredDelayMilliseconds = null)
{
	public static PlayerSummonKnownObjectTargetRangeDelayResult MissingRangeEvaluation(int mercenaryObjectId)
	{
		return new PlayerSummonKnownObjectTargetRangeDelayResult(
			PlayerSummonKnownObjectTargetRangeDelayStatus.MissingRangeEvaluation,
			mercenaryObjectId);
	}

	public static PlayerSummonKnownObjectTargetRangeDelayResult NotRequired(
		int mercenaryObjectId,
		PlayerSummonKnownObjectTargetRangeReadiness targetRangeReadiness)
	{
		return new PlayerSummonKnownObjectTargetRangeDelayResult(
			PlayerSummonKnownObjectTargetRangeDelayStatus.NotRequired,
			mercenaryObjectId,
			targetRangeReadiness);
	}

	public static PlayerSummonKnownObjectTargetRangeDelayResult MissingKnownObject(
		int mercenaryObjectId,
		PlayerSummonKnownObjectTargetRangeReadiness targetRangeReadiness)
	{
		return new PlayerSummonKnownObjectTargetRangeDelayResult(
			PlayerSummonKnownObjectTargetRangeDelayStatus.MissingKnownObject,
			mercenaryObjectId,
			targetRangeReadiness);
	}

	public static PlayerSummonKnownObjectTargetRangeDelayResult Set(
		int mercenaryObjectId,
		PlayerSummonKnownObjectTargetRangeReadiness targetRangeReadiness)
	{
		return new PlayerSummonKnownObjectTargetRangeDelayResult(
			PlayerSummonKnownObjectTargetRangeDelayStatus.Set,
			mercenaryObjectId,
			targetRangeReadiness,
			targetRangeReadiness.NextSkillDelayMilliseconds);
	}
}

public enum PlayerSummonKnownObjectTargetRangeDelayStatus
{
	MissingRangeEvaluation,
	NotRequired,
	MissingKnownObject,
	Set,
}

public sealed record PlayerSummonKnownObjectSkillAttackPreview(
	PlayerSummonKnownObjectSkillAttackPreviewStatus Status,
	PlayerSummonKnownObject KnownObject,
	long CurrentTimeMilliseconds,
	long? ElapsedFightTimeMilliseconds = null,
	int? InitialSkillDelayMilliseconds = null,
	PlayerSummonKnownObjectNextSkillReadiness? Readiness = null)
{
	public static PlayerSummonKnownObjectSkillAttackPreview BlockedCasting(
		PlayerSummonKnownObject knownObject,
		long currentTimeMilliseconds)
	{
		return new PlayerSummonKnownObjectSkillAttackPreview(
			PlayerSummonKnownObjectSkillAttackPreviewStatus.BlockedCasting,
			knownObject,
			currentTimeMilliseconds);
	}

	public static PlayerSummonKnownObjectSkillAttackPreview WouldUseQueuedInstantSkill(
		PlayerSummonKnownObject knownObject,
		long currentTimeMilliseconds)
	{
		return new PlayerSummonKnownObjectSkillAttackPreview(
			PlayerSummonKnownObjectSkillAttackPreviewStatus.WouldUseQueuedInstantSkill,
			knownObject,
			currentTimeMilliseconds);
	}

	public static PlayerSummonKnownObjectSkillAttackPreview InitialDelayNotElapsed(
		PlayerSummonKnownObject knownObject,
		long currentTimeMilliseconds,
		long elapsedFightTimeMilliseconds,
		int initialSkillDelayMilliseconds)
	{
		return new PlayerSummonKnownObjectSkillAttackPreview(
			PlayerSummonKnownObjectSkillAttackPreviewStatus.InitialDelayNotElapsed,
			knownObject,
			currentTimeMilliseconds,
			elapsedFightTimeMilliseconds,
			initialSkillDelayMilliseconds);
	}

	public static PlayerSummonKnownObjectSkillAttackPreview NextSkillNotReady(
		PlayerSummonKnownObject knownObject,
		long currentTimeMilliseconds,
		PlayerSummonKnownObjectNextSkillReadiness readiness)
	{
		return new PlayerSummonKnownObjectSkillAttackPreview(
			PlayerSummonKnownObjectSkillAttackPreviewStatus.NextSkillNotReady,
			knownObject,
			currentTimeMilliseconds,
			Readiness: readiness);
	}

	public static PlayerSummonKnownObjectSkillAttackPreview WouldEvaluateSkills(
		PlayerSummonKnownObject knownObject,
		long currentTimeMilliseconds,
		PlayerSummonKnownObjectNextSkillReadiness readiness)
	{
		return new PlayerSummonKnownObjectSkillAttackPreview(
			PlayerSummonKnownObjectSkillAttackPreviewStatus.WouldEvaluateSkills,
			knownObject,
			currentTimeMilliseconds,
			Readiness: readiness);
	}
}

public enum PlayerSummonKnownObjectSkillAttackPreviewStatus
{
	BlockedCasting,
	WouldUseQueuedInstantSkill,
	InitialDelayNotElapsed,
	NextSkillNotReady,
	WouldEvaluateSkills,
}

public enum PlayerMercenarySkillExecutionStatus
{
	MissingMercenary,
	InvalidMercenarySkill,
	WouldInvokeController,
}

public enum PlayerMercenarySkillExecutionAction
{
	SetTarget,
	UseSkill,
}

public sealed record PlayerMercenarySkillExecutionAudit(
	PlayerMercenarySkillExecutionAuditKind Kind,
	int SkillId);

public enum PlayerMercenarySkillExecutionAuditKind
{
	InvalidMercenarySkill,
}
