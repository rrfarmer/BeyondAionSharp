using System.Buffers.Binary;
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

	public PlayerSummonKnownObjectNpcSkillPreviewCaptureResult CaptureMercenaryNpcSkillPreview(
		Player player,
		int mercenaryObjectId,
		PlayerSummonKnownObjectNpcSkillCandidateListProjection? skillListProjection,
		PlayerSummonKnownObjectNpcSkillSelectionPreview? selectionPreview,
		PlayerSummonKnownObjectNpcSkillActionPreview? actionPreview,
		PlayerSummonKnownObjectNpcSkillPostSpawnPreview? postSpawnPreview = null,
		PlayerSummonKnownObjectNpcSkillActionWorkflowPreview? actionWorkflowPreview = null,
		PlayerSummonKnownObjectNpcSkillPerformAttackPreview? performAttackPreview = null,
		PlayerSummonKnownObjectNpcSkillPerformAttackExecutionPreview? performAttackExecutionPreview = null,
		PlayerSummonKnownObjectNpcSkillSchedulerCallbackOutcome? schedulerCallbackOutcome = null)
	{
		// Java parity: represents owner Npc skill-list/chooseNextSkill/skillAction/fireOnEndCastEvents state without invoking live AI or controller effects.
		return player.TryStoreSummonKnownObjectNpcSkillPreview(
				mercenaryObjectId,
				skillListProjection,
				selectionPreview,
				actionPreview,
				postSpawnPreview,
				actionWorkflowPreview,
				performAttackPreview,
				performAttackExecutionPreview,
				schedulerCallbackOutcome)
			? PlayerSummonKnownObjectNpcSkillPreviewCaptureResult.Captured(mercenaryObjectId)
			: PlayerSummonKnownObjectNpcSkillPreviewCaptureResult.MissingKnownObject(mercenaryObjectId);
	}

	public PlayerSummonKnownObjectNpcSkillAttackCycleSnapshot ProjectMercenaryNpcSkillAttackCycle(
		Player player,
		int mercenaryObjectId)
	{
		// Java parity: groups represented SkillAttackManager.chooseNextSkill/performAttack/skillAction state for a future live NpcAI bridge.
		return player.TryGetSummonKnownObject(mercenaryObjectId, out var knownObject)
			? ProjectMercenaryNpcSkillAttackCycle(knownObject)
			: PlayerSummonKnownObjectNpcSkillAttackCycleSnapshot.MissingKnownObject(mercenaryObjectId);
	}

	public PlayerSummonKnownObjectNpcSkillAttackCycleSnapshot ProjectMercenaryNpcSkillAttackCycle(
		PlayerSummonKnownObject knownObject)
	{
		// Java parity: this is metadata aggregation only; live AI mutation, scheduler callbacks, packets, and controller effects remain unwired.
		return PlayerSummonKnownObjectNpcSkillAttackCycleSnapshot.Captured(
			knownObject.ObjectId,
			knownObject,
			knownObject.LastNpcSkillListProjection,
			knownObject.LastNpcSkillSelectionPreview,
			knownObject.LastNpcSkillActionPreview,
			knownObject.LastNpcSkillPostSpawnPreview,
			knownObject.LastNpcSkillActionWorkflowPreview,
			knownObject.LastNpcSkillPerformAttackPreview,
			knownObject.LastNpcSkillPerformAttackExecutionPreview,
			knownObject.LastNpcSkillSchedulerCallbackOutcome);
	}

	public PlayerSummonKnownObjectNpcSkillAttackCycleReadiness EvaluateMercenaryNpcSkillAttackCycleReadiness(
		PlayerSummonKnownObjectNpcSkillAttackCycleSnapshot? cycleSnapshot)
	{
		// Java parity: validates represented SkillAttackManager state before any future live NpcAI adapter attempts invocation.
		if (cycleSnapshot == null)
			return PlayerSummonKnownObjectNpcSkillAttackCycleReadiness.MissingCycle();

		if (!cycleSnapshot.IsCaptured || cycleSnapshot.KnownObject == null)
			return PlayerSummonKnownObjectNpcSkillAttackCycleReadiness.MissingKnownObject(cycleSnapshot);

		var missingPieces = new List<PlayerSummonKnownObjectNpcSkillAttackCycleReadinessPiece>();

		if (!cycleSnapshot.HasSkillListProjection)
			missingPieces.Add(PlayerSummonKnownObjectNpcSkillAttackCycleReadinessPiece.SkillListProjection);
		if (!cycleSnapshot.HasSelectionPreview)
			missingPieces.Add(PlayerSummonKnownObjectNpcSkillAttackCycleReadinessPiece.SelectionPreview);
		if (!cycleSnapshot.HasActionPreview)
			missingPieces.Add(PlayerSummonKnownObjectNpcSkillAttackCycleReadinessPiece.ActionPreview);
		if (!cycleSnapshot.HasPostSpawnPreview)
			missingPieces.Add(PlayerSummonKnownObjectNpcSkillAttackCycleReadinessPiece.PostSpawnPreview);
		if (!cycleSnapshot.HasActionWorkflowPreview)
			missingPieces.Add(PlayerSummonKnownObjectNpcSkillAttackCycleReadinessPiece.ActionWorkflowPreview);
		if (!cycleSnapshot.HasPerformAttackPreview)
			missingPieces.Add(PlayerSummonKnownObjectNpcSkillAttackCycleReadinessPiece.PerformAttackPreview);
		if (!cycleSnapshot.HasPerformAttackExecutionPreview)
			missingPieces.Add(PlayerSummonKnownObjectNpcSkillAttackCycleReadinessPiece.PerformAttackExecutionPreview);
		if (!cycleSnapshot.HasSchedulerCallbackOutcome)
			missingPieces.Add(PlayerSummonKnownObjectNpcSkillAttackCycleReadinessPiece.SchedulerCallbackOutcome);

		return missingPieces.Count == 0
			? PlayerSummonKnownObjectNpcSkillAttackCycleReadiness.Ready(cycleSnapshot)
			: PlayerSummonKnownObjectNpcSkillAttackCycleReadiness.MissingRequiredMetadata(cycleSnapshot, missingPieces);
	}

	public PlayerSummonKnownObjectNpcSkillAttackCycleLiveInvocation ProjectMercenaryNpcSkillAttackCycleLiveInvocation(
		PlayerSummonKnownObjectNpcSkillAttackCycleSnapshot? cycleSnapshot)
	{
		// Java parity: placeholder boundary for future SkillAttackManager.performAttack/skillAction live NpcAI invocation.
		var readiness = EvaluateMercenaryNpcSkillAttackCycleReadiness(cycleSnapshot);
		return PlayerSummonKnownObjectNpcSkillAttackCycleLiveInvocation.FromReadiness(readiness);
	}

	public PlayerSummonKnownObjectNpcSkillAttackCycleResultContract ProjectMercenaryNpcSkillAttackCycleResultContract(
		PlayerSummonKnownObjectNpcSkillAttackCycleLiveInvocation? liveInvocation)
	{
		// Java parity: represented contract for future SkillAttackManager live side effects; it does not execute them.
		return PlayerSummonKnownObjectNpcSkillAttackCycleResultContract.FromLiveInvocation(liveInvocation);
	}

	public PlayerSummonKnownObjectNpcSkillAttackCycleOperationReadiness ProjectMercenaryNpcSkillAttackCycleOperationReadiness(
		PlayerSummonKnownObjectNpcSkillAttackCycleResultContract? resultContract)
	{
		// Java parity: groups future live operations by currently unsupported runtime dependency.
		return PlayerSummonKnownObjectNpcSkillAttackCycleOperationReadiness.FromResultContract(resultContract);
	}

	public PlayerSummonKnownObjectNpcSkillAttackCycleAdapterSummary ProjectMercenaryNpcSkillAttackCycleAdapterSummary(
		Player player,
		int mercenaryObjectId)
	{
		// Java parity: one represented handoff bundle for future SkillAttackManager live NpcAI integration.
		return ProjectMercenaryNpcSkillAttackCycleAdapterSummary(ProjectMercenaryNpcSkillAttackCycle(player, mercenaryObjectId));
	}

	public PlayerSummonKnownObjectNpcSkillAttackCycleAdapterSummary ProjectMercenaryNpcSkillAttackCycleAdapterSummary(
		PlayerSummonKnownObjectNpcSkillAttackCycleSnapshot? cycleSnapshot)
	{
		// Java parity: composes represented metadata only; live AI/scheduler/controller/packet execution stays unsupported.
		var readiness = EvaluateMercenaryNpcSkillAttackCycleReadiness(cycleSnapshot);
		var liveInvocation = PlayerSummonKnownObjectNpcSkillAttackCycleLiveInvocation.FromReadiness(readiness);
		var resultContract = PlayerSummonKnownObjectNpcSkillAttackCycleResultContract.FromLiveInvocation(liveInvocation);
		var operationReadiness = PlayerSummonKnownObjectNpcSkillAttackCycleOperationReadiness.FromResultContract(resultContract);

		return PlayerSummonKnownObjectNpcSkillAttackCycleAdapterSummary.FromComponents(
			cycleSnapshot,
			readiness,
			liveInvocation,
			resultContract,
			operationReadiness);
	}

	public PlayerSummonKnownObjectNpcSkillAttackCycleLiveAdapterContract ProjectMercenaryNpcSkillAttackCycleLiveAdapterContract(
		PlayerSummonKnownObjectNpcSkillAttackCycleAdapterSummary? adapterSummary)
	{
		// Java parity: declarative contract for a future SkillAttackManager.performAttack adapter; still non-executing.
		return PlayerSummonKnownObjectNpcSkillAttackCycleLiveAdapterContract.FromSummary(adapterSummary);
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
			template.IsPostSpawn,
			template.SpawnTemplate);
	}

	public PlayerSummonKnownObjectNpcSkillTemplateMetadata ProjectMercenaryNpcSkillTemplateMetadata(
		NpcSkillTemplateSummary template)
	{
		// Java parity: adapts model/templates/npcskill/NpcSkillTemplate JAXB fields before NpcSkillTemplateEntry wraps them.
		return new PlayerSummonKnownObjectNpcSkillTemplateMetadata(
			SkillId: template.SkillId,
			SkillLevel: template.SkillLevel,
			Probability: template.Probability,
			MinHpPercentage: template.MinHp,
			MaxHpPercentage: template.MaxHp,
			MaxTimeMilliseconds: template.MaxTime,
			MinTimeMilliseconds: template.MinTime,
			ConjunctionType: ResolveMercenaryNpcSkillConjunction(template.Conjunction),
			CooldownMilliseconds: template.Cooldown,
			IsPostSpawn: template.IsPostSpawn,
			Priority: template.Priority,
			NextSkillTimeMilliseconds: template.NextSkillTime,
			ConditionTemplate: template.Condition == null
				? null
				: new PlayerSummonKnownObjectNpcSkillConditionMetadata(
					ResolveMercenaryNpcSkillCondition(template.Condition.ConditionType),
					template.Condition.HpBelow,
					template.Condition.Range,
					template.Condition.NpcId,
					template.Condition.Delay,
					template.Condition.CanDie,
					template.Condition.DespawnTime),
			NextChainId: template.NextChainId,
			ChainId: template.ChainId,
			MaxChainTimeMilliseconds: template.MaxChainTime,
			SpawnTemplate: template.Spawn == null
				? null
				: new PlayerSummonKnownObjectNpcSkillSpawnMetadata(
					template.Spawn.NpcId,
					template.Spawn.Delay,
					template.Spawn.MinDistance,
					template.Spawn.MaxDistance,
					template.Spawn.MinCount,
					template.Spawn.MaxCount),
			Target: ResolveMercenaryNpcSkillTargetAttribute(template.Target));
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

	public PlayerSummonKnownObjectNpcSkillCandidate ProjectMercenaryNpcSkillCandidate(
		PlayerSummonKnownObjectNpcSkillCandidateMetadata candidate,
		int hpPercentage,
		long elapsedFightTimeMilliseconds,
		long currentTimeMilliseconds,
		bool ownerExists = true,
		bool ownerIsDead = false,
		bool ownerIsAboutToDie = false)
	{
		// Java parity: adapts NpcSkillTemplateEntry template/lastTimeUsed state into the represented chooseNextSkill candidate.
		var projection = ProjectMercenaryNpcSkillTemplate(
			candidate.Template,
			candidate.LastTimeUsedMilliseconds);
		var entryTimingReadiness = EvaluateMercenaryNpcSkillEntryReadiness(
			projection.EntryTiming,
			hpPercentage,
			elapsedFightTimeMilliseconds,
			currentTimeMilliseconds,
			candidate.ChanceReady);
		var entryConditionReadiness = EvaluateMercenaryNpcSkillConditionReadiness(
			projection.ConditionTemplate,
			candidate.ConditionTarget,
			ownerExists,
			ownerIsDead,
			ownerIsAboutToDie,
			skillTemplateSignetBurstStacks: candidate.SkillTemplateSignetBurstStacks,
			helpFriendCandidates: candidate.HelpFriendCandidates);

		return new PlayerSummonKnownObjectNpcSkillCandidate(
			candidate.Position,
			projection,
			entryTimingReadiness,
			entryConditionReadiness,
			candidate.TargetRangeReadiness);
	}

	public IReadOnlyList<PlayerSummonKnownObjectNpcSkillCandidateMetadata> ProjectMercenaryNpcSkillCandidateMetadata(
		NpcSkillTable? npcSkills,
		int npcId)
	{
		// Java parity: NpcSkillList.initSkillList pulls static NpcSkillTemplates by npc id and materializes entries in XML order.
		var skillList = npcSkills?.GetNpcSkillList(npcId);
		if (skillList?.Skills.Count is null or 0)
			return Array.Empty<PlayerSummonKnownObjectNpcSkillCandidateMetadata>();

		return skillList.Skills
			.Select((template, index) => new PlayerSummonKnownObjectNpcSkillCandidateMetadata(
				index,
				ProjectMercenaryNpcSkillTemplateMetadata(template)))
			.ToArray();
	}

	public PlayerSummonKnownObjectNpcSkillCandidateMetadataProjection ProjectMercenaryNpcSkillCandidateMetadata(
		NpcSkillTable? npcSkills,
		SkillTemplateTable skillTemplates,
		int npcId)
	{
		// Java parity: NpcSkillList.initSkillList removes entries missing from DataManager.SKILL_DATA before wrapping them.
		var skillList = npcSkills?.GetNpcSkillList(npcId);
		if (skillList?.Skills.Count is null or 0)
			return PlayerSummonKnownObjectNpcSkillCandidateMetadataProjection.Empty(npcId);

		var candidates = new List<PlayerSummonKnownObjectNpcSkillCandidateMetadata>();
		var missingSkillIds = new List<int>();
		foreach (var template in skillList.Skills)
		{
			var skillTemplate = skillTemplates.GetSkillTemplate(template.SkillId);
			if (skillTemplate == null)
			{
				missingSkillIds.Add(template.SkillId);
				continue;
			}

			candidates.Add(new PlayerSummonKnownObjectNpcSkillCandidateMetadata(
				candidates.Count,
				ProjectMercenaryNpcSkillTemplateMetadata(template),
				SkillTemplateSignetBurstStacks: skillTemplate.SignetBurst.Select(effect => effect.Signet).ToArray()));
		}

		return new PlayerSummonKnownObjectNpcSkillCandidateMetadataProjection(
			npcId,
			skillList.Skills.Count,
			candidates.ToArray(),
			missingSkillIds.ToArray());
	}

	public PlayerSummonKnownObjectNpcSkillCandidateListProjection ProjectMercenaryNpcSkillCandidateList(
		NpcSkillTable? npcSkills,
		int npcId,
		int hpPercentage,
		long elapsedFightTimeMilliseconds,
		long currentTimeMilliseconds,
		bool ownerExists = true,
		bool ownerIsDead = false,
		bool ownerIsAboutToDie = false)
	{
		// Java parity: represents NpcSkillList.initSkillList projection from NPC_SKILL_DATA before live SKILL_DATA pruning is available.
		return ProjectMercenaryNpcSkillCandidateList(
			ProjectMercenaryNpcSkillCandidateMetadata(npcSkills, npcId),
			hpPercentage,
			elapsedFightTimeMilliseconds,
			currentTimeMilliseconds,
			ownerExists,
			ownerIsDead,
			ownerIsAboutToDie);
	}

	public PlayerSummonKnownObjectNpcSkillCandidateListProjection ProjectMercenaryNpcSkillCandidateList(
		NpcSkillTable? npcSkills,
		SkillTemplateTable skillTemplates,
		int npcId,
		int hpPercentage,
		long elapsedFightTimeMilliseconds,
		long currentTimeMilliseconds,
		bool ownerExists = true,
		bool ownerIsDead = false,
		bool ownerIsAboutToDie = false)
	{
		return ProjectMercenaryNpcSkillCandidateList(
			ProjectMercenaryNpcSkillCandidateMetadata(npcSkills, skillTemplates, npcId).Candidates,
			hpPercentage,
			elapsedFightTimeMilliseconds,
			currentTimeMilliseconds,
			ownerExists,
			ownerIsDead,
			ownerIsAboutToDie);
	}

	public PlayerSummonKnownObjectNpcSkillCandidateListProjection ProjectMercenaryNpcSkillCandidateList(
		IEnumerable<PlayerSummonKnownObjectNpcSkillCandidateMetadata> candidates,
		int hpPercentage,
		long elapsedFightTimeMilliseconds,
		long currentTimeMilliseconds,
		bool ownerExists = true,
		bool ownerIsDead = false,
		bool ownerIsAboutToDie = false)
	{
		// Java parity: mirrors NpcSkillList.initSkillList priority extraction after static template entries are materialized.
		var projectedCandidates = candidates
			.Select(candidate => ProjectMercenaryNpcSkillCandidate(
				candidate,
				hpPercentage,
				elapsedFightTimeMilliseconds,
				currentTimeMilliseconds,
				ownerExists,
				ownerIsDead,
				ownerIsAboutToDie))
			.ToList();
		var priorities = projectedCandidates
			.Select(candidate => candidate.Projection.Priority)
			.Distinct()
			.OrderByDescending(priority => priority)
			.ToList();
		var postSpawnCandidates = projectedCandidates
			.Where(candidate => candidate.Projection.IsPostSpawn)
			.ToList();

		return new PlayerSummonKnownObjectNpcSkillCandidateListProjection(
			projectedCandidates,
			priorities,
			postSpawnCandidates);
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

	public PlayerSummonKnownObjectNpcSkillConjunction ResolveMercenaryNpcSkillConjunction(string conjunction)
	{
		return conjunction.ToUpperInvariant() switch
		{
			"OR" => PlayerSummonKnownObjectNpcSkillConjunction.Or,
			"XOR" => PlayerSummonKnownObjectNpcSkillConjunction.Xor,
			_ => PlayerSummonKnownObjectNpcSkillConjunction.And,
		};
	}

	public PlayerSummonKnownObjectNpcSkillTargetAttribute ResolveMercenaryNpcSkillTargetAttribute(string target)
	{
		return target.ToUpperInvariant() switch
		{
			"FRIEND" => PlayerSummonKnownObjectNpcSkillTargetAttribute.Friend,
			"ME" => PlayerSummonKnownObjectNpcSkillTargetAttribute.Me,
			"SECOND_MOST_HATED" => PlayerSummonKnownObjectNpcSkillTargetAttribute.SecondMostHated,
			"THIRD_MOST_HATED" => PlayerSummonKnownObjectNpcSkillTargetAttribute.ThirdMostHated,
			"RANDOM" => PlayerSummonKnownObjectNpcSkillTargetAttribute.Random,
			"RANDOM_EXCEPT_CURRENT_TARGET" => PlayerSummonKnownObjectNpcSkillTargetAttribute.RandomExceptCurrentTarget,
			"NONE" => PlayerSummonKnownObjectNpcSkillTargetAttribute.None,
			_ => PlayerSummonKnownObjectNpcSkillTargetAttribute.MostHated,
		};
	}

	public PlayerSummonKnownObjectNpcSkillCondition ResolveMercenaryNpcSkillCondition(string condition)
	{
		return condition.ToUpperInvariant() switch
		{
			"HELP_FRIEND" => PlayerSummonKnownObjectNpcSkillCondition.HelpFriend,
			"TARGET_IS_IN_ANY_STUN" => PlayerSummonKnownObjectNpcSkillCondition.TargetIsInAnyStun,
			"TARGET_IS_IN_RANGE" => PlayerSummonKnownObjectNpcSkillCondition.TargetIsInRange,
			"TARGET_IS_IN_STUMBLE" => PlayerSummonKnownObjectNpcSkillCondition.TargetIsInStumble,
			"TARGET_IS_STUNNED" => PlayerSummonKnownObjectNpcSkillCondition.TargetIsStunned,
			"TARGET_IS_SLEEPING" => PlayerSummonKnownObjectNpcSkillCondition.TargetIsSleeping,
			"TARGET_IS_AETHERS_HOLD" => PlayerSummonKnownObjectNpcSkillCondition.TargetIsAethersHold,
			"TARGET_IS_POISONED" => PlayerSummonKnownObjectNpcSkillCondition.TargetIsPoisoned,
			"TARGET_IS_BLEEDING" => PlayerSummonKnownObjectNpcSkillCondition.TargetIsBleeding,
			"TARGET_IS_FLYING" => PlayerSummonKnownObjectNpcSkillCondition.TargetIsFlying,
			"TARGET_IS_GATE" => PlayerSummonKnownObjectNpcSkillCondition.TargetIsGate,
			"TARGET_IS_PLAYER" => PlayerSummonKnownObjectNpcSkillCondition.TargetIsPlayer,
			"TARGET_IS_NPC" => PlayerSummonKnownObjectNpcSkillCondition.TargetIsNpc,
			"TARGET_IS_PHYSICAL_CLASS" => PlayerSummonKnownObjectNpcSkillCondition.TargetIsPhysicalClass,
			"TARGET_IS_MAGICAL_CLASS" => PlayerSummonKnownObjectNpcSkillCondition.TargetIsMagicalClass,
			"TARGET_HAS_CARVED_SIGNET" => PlayerSummonKnownObjectNpcSkillCondition.TargetHasCarvedSignet,
			"TARGET_HAS_CARVED_SIGNET_LEVEL_II" => PlayerSummonKnownObjectNpcSkillCondition.TargetHasCarvedSignetLevelIi,
			"TARGET_HAS_CARVED_SIGNET_LEVEL_III" => PlayerSummonKnownObjectNpcSkillCondition.TargetHasCarvedSignetLevelIii,
			"TARGET_HAS_CARVED_SIGNET_LEVEL_IV" => PlayerSummonKnownObjectNpcSkillCondition.TargetHasCarvedSignetLevelIv,
			"TARGET_HAS_CARVED_SIGNET_LEVEL_V" => PlayerSummonKnownObjectNpcSkillCondition.TargetHasCarvedSignetLevelV,
			"NPC_IS_ALIVE" => PlayerSummonKnownObjectNpcSkillCondition.NpcIsAlive,
			_ => PlayerSummonKnownObjectNpcSkillCondition.None,
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

	public PlayerSummonKnownObjectNpcSkillActionResult ProjectMercenaryNpcSkillActionResult(
		PlayerSummonKnownObjectNpcSkillActionPreview? actionPreview)
	{
		if (actionPreview == null)
			return PlayerSummonKnownObjectNpcSkillActionResult.MissingPreview();

		// Java parity: SkillAttackManager.skillAction branches into AI events, abortCast, target mutation, useSkill, and afterUseSkill.
		return actionPreview.Status switch
		{
			PlayerSummonKnownObjectNpcSkillActionPreviewStatus.NotInCastSubState => PlayerSummonKnownObjectNpcSkillActionResult.NoAction(actionPreview),
			PlayerSummonKnownObjectNpcSkillActionPreviewStatus.ResumeFightAfterInterruptedCast => PlayerSummonKnownObjectNpcSkillActionResult.ResumeFight(actionPreview),
			PlayerSummonKnownObjectNpcSkillActionPreviewStatus.TargetGiveUp => PlayerSummonKnownObjectNpcSkillActionResult.TargetGiveUp(actionPreview),
			PlayerSummonKnownObjectNpcSkillActionPreviewStatus.TargetTooFar => PlayerSummonKnownObjectNpcSkillActionResult.TargetTooFar(actionPreview),
			PlayerSummonKnownObjectNpcSkillActionPreviewStatus.AfterUseSkillBlocked => PlayerSummonKnownObjectNpcSkillActionResult.AfterUseSkill(actionPreview),
			PlayerSummonKnownObjectNpcSkillActionPreviewStatus.AfterUseSkillUseFailed => PlayerSummonKnownObjectNpcSkillActionResult.UseSkillFailed(actionPreview),
			PlayerSummonKnownObjectNpcSkillActionPreviewStatus.WouldSetTargetAndUseSkill => PlayerSummonKnownObjectNpcSkillActionResult.UseSkill(actionPreview),
			PlayerSummonKnownObjectNpcSkillActionPreviewStatus.WouldUseSkill => PlayerSummonKnownObjectNpcSkillActionResult.UseSkill(actionPreview),
			_ => PlayerSummonKnownObjectNpcSkillActionResult.NoAction(actionPreview),
		};
	}

	public PlayerSummonKnownObjectNpcSkillActionSideEffectTrace ProjectMercenaryNpcSkillActionSideEffectTrace(
		PlayerSummonKnownObjectNpcSkillActionResult? actionResult)
	{
		// Java parity: SkillAttackManager.skillAction mutates target before controller useSkill, then afterUseSkill only on blocked/failed paths.
		return PlayerSummonKnownObjectNpcSkillActionSideEffectTrace.FromActionResult(actionResult);
	}

	public PlayerSummonKnownObjectNpcSkillEndCastSideEffectTrace ProjectMercenaryNpcSkillEndCastSideEffectTrace(
		PlayerSummonKnownObjectNpcSkillActionResult? actionResult,
		PlayerSummonKnownObjectNpcSkillPostSpawnPreview? postSpawnPreview,
		bool isInstantSkill,
		bool skillMethodSendsCastSpellEnd = true,
		bool isNpcEffector = true,
		bool isCastSkillMethod = true)
	{
		// Java parity: Skill.endCast applies/schedules effects, sends cast result, fires NPC post-spawn, then afterUseSkill.
		return PlayerSummonKnownObjectNpcSkillEndCastSideEffectTrace.FromActionResult(
			actionResult,
			postSpawnPreview,
			isInstantSkill,
			skillMethodSendsCastSpellEnd,
			isNpcEffector,
			isCastSkillMethod);
	}

	public PlayerSummonKnownObjectNpcSkillEndCastBranchTrace ProjectMercenaryNpcSkillEndCastBranchTrace(
		PlayerSummonKnownObjectNpcSkillActionResult? actionResult,
		bool effectorStillCasting = true,
		bool endCastValidationPasses = true,
		bool isItemSkill = false,
		bool itemAvailable = true,
		bool itemConsumed = true,
		bool actionsSucceed = true,
		bool hasEffects = true,
		bool allEffectsResistedOrDodged = false,
		bool isPlayerEffector = false,
		bool isMultiCast = false,
		bool hasChainCategory = false,
		bool chainSucceeds = true,
		bool isInstantSkill = true,
		bool sendsCastResult = true,
		bool isNpcEffector = true,
		bool isCastSkillMethod = true)
	{
		// Java parity: Skill.endCast validates again, ignores endCondCheck's result, actions short-circuit, then cooldown/penalty/effects/packets/hooks run in order.
		return PlayerSummonKnownObjectNpcSkillEndCastBranchTrace.FromActionResult(
			actionResult,
			effectorStillCasting,
			endCastValidationPasses,
			isItemSkill,
			itemAvailable,
			itemConsumed,
			actionsSucceed,
			hasEffects,
			allEffectsResistedOrDodged,
			isPlayerEffector,
			isMultiCast,
			hasChainCategory,
			chainSucceeds,
			isInstantSkill,
			sendsCastResult,
			isNpcEffector,
			isCastSkillMethod);
	}

	public PlayerSummonKnownObjectNpcSkillValidationMutationTrace ProjectMercenaryNpcSkillValidationMutationTrace(
		PlayerSummonKnownObjectNpcSkillActionResult? actionResult,
		bool hasProperties = true,
		bool isEndCastValidation = true,
		bool hasFirstTargetRange = true,
		bool firstTargetRangePasses = true,
		bool hasTargetType = true,
		bool targetRangePasses = true,
		bool targetRangeMayAppendTargets = true,
		bool hasTargetRelation = true,
		bool targetRelationPasses = true,
		bool targetRelationMayReplaceFirstTarget = true,
		bool hasTargetStatus = false,
		bool targetStatusPasses = true,
		bool hasTargetSpecies = false,
		bool targetSpeciesPasses = true,
		bool hasMaxCount = true,
		bool maxCountPasses = true,
		bool isPlayerEffector = false,
		bool playerCanUseSkill = true,
		bool playerTargetFilterKeepsAny = true,
		bool targetTypeZero = false,
		bool isAreaTarget = false)
	{
		// Java parity: Properties.endCastValidate clears/re-seeds effectedList, property filters may mutate first target, then Skill.validateEffectedList can filter or reject it.
		return PlayerSummonKnownObjectNpcSkillValidationMutationTrace.FromActionResult(
			actionResult,
			hasProperties,
			isEndCastValidation,
			hasFirstTargetRange,
			firstTargetRangePasses,
			hasTargetType,
			targetRangePasses,
			targetRangeMayAppendTargets,
			hasTargetRelation,
			targetRelationPasses,
			targetRelationMayReplaceFirstTarget,
			hasTargetStatus,
			targetStatusPasses,
			hasTargetSpecies,
			targetSpeciesPasses,
			hasMaxCount,
			maxCountPasses,
			isPlayerEffector,
			playerCanUseSkill,
			playerTargetFilterKeepsAny,
			targetTypeZero,
			isAreaTarget);
	}

	public PlayerSummonKnownObjectNpcSkillTemplateActionTrace ProjectMercenaryNpcSkillTemplateActionTrace(
		PlayerSummonKnownObjectNpcSkillActionResult? actionResult,
		IReadOnlyList<PlayerSummonKnownObjectNpcSkillTemplateActionKind>? actionOrder = null,
		PlayerSummonKnownObjectNpcSkillTemplateActionKind? failingAction = null,
		bool effectorIsPlayer = true,
		bool mpRatio = false,
		bool hpRatio = false,
		bool boostSkillCostPresent = false)
	{
		// Java parity: Skill.endCast executes JAXB Actions live-list order; first Action.act(false) returns before effects/cooldowns/penalty.
		return PlayerSummonKnownObjectNpcSkillTemplateActionTrace.FromActionResult(
			actionResult,
			actionOrder,
			failingAction,
			effectorIsPlayer,
			mpRatio,
			hpRatio,
			boostSkillCostPresent);
	}

	public PlayerSummonKnownObjectNpcSkillEffectInitializationTrace ProjectMercenaryNpcSkillEffectInitializationTrace(
		PlayerSummonKnownObjectNpcSkillActionResult? actionResult,
		bool hasSkillEffects = true,
		int effectedCount = 1,
		bool isPointPointSkill = false,
		bool hasConflictOnPlayerTarget = false,
		bool allEffectsResistedOrDodged = false,
		bool firstTargetReceivesDashStatus = true,
		bool successEffectsEmpty = false,
		bool emptySuccessFromPhysicalEffect = false,
		bool launchesSubEffect = false,
		bool mayCreateCriticalSubEffect = false,
		bool isInstantSkill = true,
		bool hasResistedTauntHate = false)
	{
		// Java parity: Skill.endCast creates Effect objects, initializes attack/result fields, stores world position, then applies immediately or by hitTime.
		return PlayerSummonKnownObjectNpcSkillEffectInitializationTrace.FromActionResult(
			actionResult,
			hasSkillEffects,
			effectedCount,
			isPointPointSkill,
			hasConflictOnPlayerTarget,
			allEffectsResistedOrDodged,
			firstTargetReceivesDashStatus,
			successEffectsEmpty,
			emptySuccessFromPhysicalEffect,
			launchesSubEffect,
			mayCreateCriticalSubEffect,
			isInstantSkill,
			hasResistedTauntHate);
	}

	public PlayerSummonKnownObjectNpcSkillCastResultPacketTrace ProjectMercenaryNpcSkillCastResultPacketTrace(
		PlayerSummonKnownObjectNpcSkillActionResult? actionResult,
		bool skillMethodSendsCastSpellEnd = true,
		bool hasItemTemplate = false,
		bool itemTemplateIsCombatActivated = false,
		bool skillSubTypeIsAttack = true,
		int targetType = 0,
		int effectsCount = 1,
		bool chainSuccess = true,
		bool dashStatusWritesLocation = false,
		bool isPenaltySkillMethod = false,
		bool isItemSkillMethod = false,
		bool effectorIsPlayer = true,
		bool effectHasOriginalTarget = true,
		bool spellStatusWritesLocation = false,
		bool subEffectWritesLocation = false,
		bool hasReservedEffects = true,
		PlayerSummonKnownObjectNpcSkillCastResultShieldBranch shieldBranch = PlayerSummonKnownObjectNpcSkillCastResultShieldBranch.None)
	{
		// Java parity: Skill.sendCastSpellEnd selects SM_CASTSPELL_RESULT branches before item-method use messages; packet bytes remain unwired.
		return PlayerSummonKnownObjectNpcSkillCastResultPacketTrace.FromActionResult(
			actionResult,
			skillMethodSendsCastSpellEnd,
			hasItemTemplate,
			itemTemplateIsCombatActivated,
			skillSubTypeIsAttack,
			targetType,
			effectsCount,
			chainSuccess,
			dashStatusWritesLocation,
			isPenaltySkillMethod,
			isItemSkillMethod,
			effectorIsPlayer,
			effectHasOriginalTarget,
			spellStatusWritesLocation,
			subEffectWritesLocation,
			hasReservedEffects,
			shieldBranch);
	}

	public PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaGolden ProjectMercenaryNpcSkillCastResultPacketSchemaGolden(
		PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaSample sample =
			PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaSample.ObjectTargetWithReservedProtect)
	{
		// Java parity: SM_CASTSPELL_RESULT.writeImpl writes to AConnection.writeBuffer, whose ByteBuffer is LITTLE_ENDIAN.
		return PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaGolden.FromSample(sample);
	}

	public PlayerSummonKnownObjectNpcSkillEffectReservedPacketProjection ProjectMercenaryNpcSkillEffectReservedPacketProjection(
		IReadOnlyList<PlayerSummonKnownObjectNpcSkillEffectReservedInput>? reservedEffects,
		int shieldDefense = 0)
	{
		// Java parity: Effect.getReservedEffectsToSend filters EffectReserved rows, TreeSet-sorts by position/hashCode, then SM_CASTSPELL_RESULT writes exact shield branches.
		return PlayerSummonKnownObjectNpcSkillEffectReservedPacketProjection.FromJavaEffectReservedRows(reservedEffects, shieldDefense);
	}

	public PlayerSummonKnownObjectNpcSkillEffectReservedProducerCatalog ProjectMercenaryNpcSkillEffectReservedProducerCatalog()
	{
		// Java parity: concrete EffectTemplate/AttackUtil/AttackShieldObserver producers feed EffectReserved rows and shieldDefense fields before SM_CASTSPELL_RESULT.
		return PlayerSummonKnownObjectNpcSkillEffectReservedProducerCatalog.JavaSourceReviewed();
	}

	public PlayerSummonKnownObjectNpcSkillUseSkillStartTrace ProjectMercenaryNpcSkillUseSkillStartTrace(
		PlayerSummonKnownObjectNpcSkillActionResult? actionResult,
		bool canUseSkillAtCastStart = true,
		bool isChargeSkill = false,
		bool castDurationGreaterThanZero = false,
		bool isNpcEffector = true,
		bool notifiesCastObservers = true)
	{
		// Java parity: Skill.useSkill resets boost cost, validates CAST_START conditions, starts CAST, updates NPC delay, then schedules or invokes endCast.
		return PlayerSummonKnownObjectNpcSkillUseSkillStartTrace.FromActionResult(
			actionResult,
			canUseSkillAtCastStart,
			isChargeSkill,
			castDurationGreaterThanZero,
			isNpcEffector,
			notifiesCastObservers);
	}

	public PlayerSummonKnownObjectNpcSkillPerformAttackPreview PreviewMercenaryNpcSkillPerformAttack(
		bool ownerUsesMeleeAggroRange,
		bool hasCurrentTarget,
		bool currentTargetInAggroRange,
		bool canEnterCastSubState,
		int delayMilliseconds,
		PlayerSummonKnownObjectNpcSkillActionWorkflowPreview? actionWorkflowPreview = null)
	{
		// Java parity: SkillAttackManager.performAttack aborts before entering CAST when melee NPC target is outside aggro range.
		if (ownerUsesMeleeAggroRange && hasCurrentTarget && !currentTargetInAggroRange)
			return PlayerSummonKnownObjectNpcSkillPerformAttackPreview.TargetTooFar(delayMilliseconds, actionWorkflowPreview);

		if (!canEnterCastSubState)
			return PlayerSummonKnownObjectNpcSkillPerformAttackPreview.CastSubStateUnchanged(delayMilliseconds, actionWorkflowPreview);

		return delayMilliseconds > 0
			? PlayerSummonKnownObjectNpcSkillPerformAttackPreview.Scheduled(delayMilliseconds, actionWorkflowPreview)
			: PlayerSummonKnownObjectNpcSkillPerformAttackPreview.Immediate(actionWorkflowPreview);
	}

	public PlayerSummonKnownObjectNpcSkillPerformAttackExecutionPreview PreviewMercenaryNpcSkillPerformAttackExecution(
		PlayerSummonKnownObjectNpcSkillPerformAttackPreview? performAttackPreview,
		long performAttackTimeMilliseconds,
		long currentTimeMilliseconds)
	{
		if (performAttackPreview == null)
			return PlayerSummonKnownObjectNpcSkillPerformAttackExecutionPreview.MissingPreview(currentTimeMilliseconds);

		if (performAttackPreview.Status == PlayerSummonKnownObjectNpcSkillPerformAttackPreviewStatus.TargetTooFar
			|| performAttackPreview.Status == PlayerSummonKnownObjectNpcSkillPerformAttackPreviewStatus.CastSubStateUnchanged)
		{
			return PlayerSummonKnownObjectNpcSkillPerformAttackExecutionPreview.NoAction(
				performAttackPreview,
				currentTimeMilliseconds);
		}

		if (performAttackPreview.ActionWorkflowPreview == null)
		{
			return PlayerSummonKnownObjectNpcSkillPerformAttackExecutionPreview.MissingWorkflow(
				performAttackPreview,
				currentTimeMilliseconds);
		}

		if (performAttackPreview.Status == PlayerSummonKnownObjectNpcSkillPerformAttackPreviewStatus.ImmediateSkillAction)
		{
			return PlayerSummonKnownObjectNpcSkillPerformAttackExecutionPreview.Immediate(
				performAttackPreview,
				currentTimeMilliseconds);
		}

		var dueAt = performAttackTimeMilliseconds + performAttackPreview.DelayMilliseconds;
		return currentTimeMilliseconds >= dueAt
			? PlayerSummonKnownObjectNpcSkillPerformAttackExecutionPreview.ScheduledDue(
				performAttackPreview,
				currentTimeMilliseconds,
				dueAt)
			: PlayerSummonKnownObjectNpcSkillPerformAttackExecutionPreview.ScheduledPending(
				performAttackPreview,
				currentTimeMilliseconds,
				dueAt);
	}

	public PlayerSummonKnownObjectNpcSkillSchedulerCallbackOutcome ProjectMercenaryNpcSkillSchedulerCallbackOutcome(
		PlayerSummonKnownObjectNpcSkillPerformAttackExecutionPreview? executionPreview)
	{
		if (executionPreview == null)
			return PlayerSummonKnownObjectNpcSkillSchedulerCallbackOutcome.MissingExecutionPreview();

		if (executionPreview.Status == PlayerSummonKnownObjectNpcSkillPerformAttackExecutionPreviewStatus.MissingWorkflow)
			return PlayerSummonKnownObjectNpcSkillSchedulerCallbackOutcome.MissingWorkflow(executionPreview);

		if (executionPreview.Status == PlayerSummonKnownObjectNpcSkillPerformAttackExecutionPreviewStatus.ScheduledWorkflowPending)
			return PlayerSummonKnownObjectNpcSkillSchedulerCallbackOutcome.Pending(executionPreview);

		if (!executionPreview.ShouldInvokeSkillActionWorkflow)
			return PlayerSummonKnownObjectNpcSkillSchedulerCallbackOutcome.NoAction(executionPreview);

		var workflow = executionPreview.ActionWorkflowPreview;
		if (workflow == null)
			return PlayerSummonKnownObjectNpcSkillSchedulerCallbackOutcome.MissingWorkflow(executionPreview);

		return PlayerSummonKnownObjectNpcSkillSchedulerCallbackOutcome.WorkflowInvoked(
			executionPreview,
			workflow,
			workflow.ActionResult);
	}

	public PlayerSummonKnownObjectNpcSkillActionWorkflowPreview PreviewMercenaryNpcSkillActionWorkflow(
		PlayerSummonKnownObject knownObject,
		PlayerSummonKnownObjectNpcSkillSelectionPreview? selectionPreview,
		SkillTemplateSummary? selectedSkillTemplate,
		PlayerSummonKnownObject? currentTarget,
		Player? player = null,
		int? mercenaryObjectId = null,
		bool canSeeCurrentTarget = true,
		bool selectedSkillFirstTargetIsSelf = false,
		bool selectedSkillTargetIsArea = false,
		bool currentTargetInFirstTargetRange = true,
		PlayerSummonKnownObjectNpcSkillTargetAttribute selectedNpcSkillTarget = PlayerSummonKnownObjectNpcSkillTargetAttribute.MostHated,
		bool hasFriendTarget = false,
		bool hasMostHatedTarget = false,
		bool hasSecondMostHatedTarget = false,
		bool hasThirdMostHatedTarget = false,
		bool hasRandomTarget = false,
		bool hasRandomExceptCurrentTarget = false,
		bool isInCastSubState = true,
		bool shouldResumeFightAfterInterruptedCast = false,
		bool ownerUsesMeleeAggroRange = false,
		bool currentTargetInAggroRange = true,
		bool controllerUseSkillSucceeded = true)
	{
		if (selectionPreview == null)
			return PlayerSummonKnownObjectNpcSkillActionWorkflowPreview.MissingSelectionPreview(knownObject);

		var selectedCandidate = selectionPreview.Selection.Candidate;
		if (selectionPreview.Selection.Status != PlayerSummonKnownObjectNpcSkillSelectionStatus.Ready || selectedCandidate == null)
		{
			return PlayerSummonKnownObjectNpcSkillActionWorkflowPreview.NoSelectedCandidate(
				knownObject,
				selectionPreview);
		}

		// Java parity: chooseNextSkill applies targetTooFar before skillAction can use the selected entry.
		var targetRangeReadiness = EvaluateMercenaryTargetRange(
			knownObject,
			selectedCandidate.Projection.TargetMode,
			currentTarget,
			canSeeCurrentTarget,
			selectedSkillTargetIsArea,
			currentTargetInFirstTargetRange);
		var targetRangeDelay = player == null
			? null
			: ApplyMercenaryTargetRangeDelay(
				player,
				mercenaryObjectId ?? knownObject.ObjectId,
				targetRangeReadiness);
		if (targetRangeReadiness.Status is not PlayerSummonKnownObjectTargetRangeReadinessStatus.Ready
			and not PlayerSummonKnownObjectTargetRangeReadinessStatus.NotRequired)
		{
			return PlayerSummonKnownObjectNpcSkillActionWorkflowPreview.TargetRangeNotReady(
				knownObject,
				selectionPreview,
				selectedCandidate,
				targetRangeReadiness,
				targetRangeDelay);
		}

		var skillReadiness = EvaluateMercenarySkillReadiness(
			knownObject,
			selectedSkillTemplate,
			selectedCandidate.EntryTimingReadiness,
			selectedCandidate.EntryConditionReadiness);
		var targetSelection = SelectMercenaryNpcSkillActionTarget(
			selectedSkillFirstTargetIsSelf,
			selectedNpcSkillTarget,
			hasFriendTarget,
			hasMostHatedTarget,
			hasSecondMostHatedTarget,
			hasThirdMostHatedTarget,
			hasRandomTarget,
			hasRandomExceptCurrentTarget);
		var actionPreview = PreviewMercenaryNpcSkillAction(
			isInCastSubState,
			shouldResumeFightAfterInterruptedCast,
			currentTarget?.IsCreature == true,
			currentTarget?.IsDead == true,
			hasLastSkill: true,
			ownerUsesMeleeAggroRange,
			currentTargetInAggroRange,
			skillReadiness,
			targetSelection,
			controllerUseSkillSucceeded);
		var actionResult = ProjectMercenaryNpcSkillActionResult(actionPreview);

		return PlayerSummonKnownObjectNpcSkillActionWorkflowPreview.Projected(
			knownObject,
			selectionPreview,
			selectedCandidate,
			targetRangeReadiness,
			targetRangeDelay,
			skillReadiness,
			targetSelection,
			actionPreview,
			actionResult);
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
		bool ownerIsAboutToDie = false,
		bool? npcIsAliveInWorld = null,
		IEnumerable<PlayerSummonKnownObjectNpcSkillHelpFriendCandidate>? helpFriendCandidates = null,
		IEnumerable<string>? skillTemplateSignetBurstStacks = null)
	{
		if (!ownerExists || ownerIsDead || ownerIsAboutToDie)
			return PlayerSummonKnownObjectNpcSkillConditionReadiness.OwnerNotReady(conditionMetadata.Condition, target);

		if (IsCarvedSignetCondition(conditionMetadata.Condition))
		{
			// Java parity: hasCarvedSignet scans this skill template's SignetBurstEffect signet stacks and target abnormal effects.
			if (skillTemplateSignetBurstStacks == null)
				return PlayerSummonKnownObjectNpcSkillConditionReadiness.Unsupported(conditionMetadata.Condition, target);

			return MatchCarvedSignetCondition(conditionMetadata.Condition, target, skillTemplateSignetBurstStacks);
		}

		if (conditionMetadata.Condition == PlayerSummonKnownObjectNpcSkillCondition.HelpFriend)
		{
			// Java parity: NpcSkillTemplateEntry.conditionReady scans KnownList.findObject and setTarget(first valid support/friend).
			if (helpFriendCandidates == null)
				return PlayerSummonKnownObjectNpcSkillConditionReadiness.Unsupported(conditionMetadata.Condition, target);

			var validTarget = helpFriendCandidates.FirstOrDefault(candidate => IsMercenaryHelpFriendCandidateReady(conditionMetadata, candidate));
			return validTarget == null
				? PlayerSummonKnownObjectNpcSkillConditionReadiness.NotReady(
					conditionMetadata.Condition,
					target ?? PlayerSummonKnownObjectNpcSkillConditionTarget.HelpFriendSearch())
				: PlayerSummonKnownObjectNpcSkillConditionReadiness.Ready(
					conditionMetadata.Condition,
					PlayerSummonKnownObjectNpcSkillConditionTarget.HelpFriendTarget(validTarget, conditionMetadata),
					validTarget);
		}

		if (conditionMetadata.Condition == PlayerSummonKnownObjectNpcSkillCondition.NpcIsAlive)
		{
			// Java parity: NpcSkillTemplateEntry.conditionReady checks worldMapInstance.getNpcs(condTemp.getNpcId()).anyMatch(!dead).
			if (npcIsAliveInWorld == null)
				return PlayerSummonKnownObjectNpcSkillConditionReadiness.Unsupported(conditionMetadata.Condition, target);

			return npcIsAliveInWorld.Value
				? PlayerSummonKnownObjectNpcSkillConditionReadiness.Ready(conditionMetadata.Condition, target)
				: PlayerSummonKnownObjectNpcSkillConditionReadiness.NotReady(conditionMetadata.Condition, target ?? PlayerSummonKnownObjectNpcSkillConditionTarget.WorldNpcPresence(conditionMetadata.NpcId));
		}

		return EvaluateMercenaryNpcSkillConditionReadiness(
			conditionMetadata.Condition,
			target,
			ownerExists: true,
			ownerIsDead: false,
			ownerIsAboutToDie: false);
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

	public PlayerSummonKnownObjectNpcSkillConditionTarget ProjectMercenaryNpcSkillConditionTarget(
		PlayerSummonKnownObject knownObject,
		PlayerSummonKnownObjectNpcSkillConditionMetadata conditionMetadata,
		double distanceMeters,
		bool? geoCanSee = null,
		bool isSupport = false,
		bool isFriend = false)
	{
		// Java parity: NpcSkillTemplateEntry.conditionReady reads the current VisibleObject/Creature target and its EffectController state.
		return new PlayerSummonKnownObjectNpcSkillConditionTarget(
			ProjectMercenaryNpcSkillConditionTargetKind(knownObject),
			knownObject.AbnormalState,
			knownObject.IsFlying,
			knownObject.IsPhysicalClass,
			distanceMeters <= conditionMetadata.RangeMeters,
			ObjectId: knownObject.ObjectId,
			IsVisible: knownObject.IsVisible,
			IsCreature: knownObject.IsCreature,
			IsDead: knownObject.IsDead,
			IsAboutToDie: knownObject.IsAboutToDie,
			IsSupport: isSupport,
			IsFriend: isFriend,
			HpPercentage: knownObject.HpPercentage,
			GeoCanSee: geoCanSee,
			CarvedSignets: knownObject.ActiveCarvedSignets);
	}

	public PlayerSummonKnownObjectNpcSkillHelpFriendCandidate ProjectMercenaryNpcSkillHelpFriendCandidate(
		PlayerSummonKnownObject knownObject,
		double distanceMeters,
		bool geoCanSee,
		bool isSupport = false,
		bool isFriend = false)
	{
		// Java parity: HELP_FRIEND tests KnownObject visibility, Creature state, relation flags, HP, range, and GeoService visibility.
		return new PlayerSummonKnownObjectNpcSkillHelpFriendCandidate(
			knownObject.ObjectId,
			ProjectMercenaryNpcSkillConditionTargetKind(knownObject),
			knownObject.IsVisible,
			knownObject.IsCreature,
			knownObject.IsDead,
			knownObject.IsAboutToDie,
			isSupport,
			isFriend,
			knownObject.HpPercentage,
			distanceMeters,
			geoCanSee);
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

	public PlayerSummonKnownObjectNpcSkillSelectionPreview PreviewMercenaryNextNpcSkillSelectionFromCandidateMetadata(
		PlayerSummonKnownObject knownObject,
		long fightStartingTimeMilliseconds,
		int initialSkillDelayMilliseconds,
		long currentTimeMilliseconds,
		bool isInCastSubState,
		IEnumerable<PlayerSummonKnownObjectNpcSkillCandidateMetadata> candidates,
		int hpPercentage,
		PlayerSummonKnownObjectNpcSkillCandidateMetadata? queuedCandidate = null,
		PlayerSummonKnownObjectNpcSkillTemplateMetadata? lastSkill = null,
		long lastSkillLastTimeUsedMilliseconds = 0,
		bool ownerExists = true,
		bool ownerIsDead = false,
		bool ownerIsAboutToDie = false)
	{
		var elapsedFightTime = currentTimeMilliseconds - fightStartingTimeMilliseconds;
		var projectedCandidateList = ProjectMercenaryNpcSkillCandidateList(
			candidates,
			hpPercentage,
			elapsedFightTime,
			currentTimeMilliseconds,
			ownerExists,
			ownerIsDead,
			ownerIsAboutToDie);
		var projectedQueuedCandidate = queuedCandidate == null
			? null
			: ProjectMercenaryNpcSkillCandidate(
				queuedCandidate,
				hpPercentage,
				elapsedFightTime,
				currentTimeMilliseconds,
				ownerExists,
				ownerIsDead,
				ownerIsAboutToDie);
		var projectedLastSkill = lastSkill == null
			? null
			: ProjectMercenaryNpcSkillTemplate(lastSkill, lastSkillLastTimeUsedMilliseconds);

		return PreviewMercenaryNextNpcSkillSelection(
			knownObject,
			fightStartingTimeMilliseconds,
			initialSkillDelayMilliseconds,
			currentTimeMilliseconds,
			isInCastSubState,
			projectedQueuedCandidate,
			projectedLastSkill,
			projectedCandidateList.Candidates);
	}

	public PlayerSummonKnownObjectNpcSkillSelectionPreview PreviewMercenaryNextNpcSkillSelectionFromRepresentedCurrentTarget(
		PlayerSummonKnownObject knownObject,
		PlayerSummonKnownObject currentTarget,
		double currentTargetDistanceMeters,
		long fightStartingTimeMilliseconds,
		int initialSkillDelayMilliseconds,
		long currentTimeMilliseconds,
		bool isInCastSubState,
		IEnumerable<PlayerSummonKnownObjectNpcSkillCandidateMetadata> candidates,
		int hpPercentage,
		bool? currentTargetGeoCanSee = null,
		bool currentTargetIsSupport = false,
		bool currentTargetIsFriend = false,
		IEnumerable<PlayerSummonKnownObjectNpcSkillHelpFriendCandidate>? helpFriendCandidates = null,
		PlayerSummonKnownObjectNpcSkillCandidateMetadata? queuedCandidate = null,
		PlayerSummonKnownObjectNpcSkillTemplateMetadata? lastSkill = null,
		long lastSkillLastTimeUsedMilliseconds = 0,
		bool ownerExists = true,
		bool ownerIsDead = false,
		bool ownerIsAboutToDie = false)
	{
		// Java parity: represents the curTarget facts read by NpcSkillTemplateEntry.conditionReady before live creature.getTarget() exists.
		var projectedCandidates = candidates
			.Select(candidate => ApplyRepresentedCurrentTarget(
				candidate,
				currentTarget,
				currentTargetDistanceMeters,
				currentTargetGeoCanSee,
				currentTargetIsSupport,
				currentTargetIsFriend,
				helpFriendCandidates))
			.ToArray();
		var projectedQueuedCandidate = queuedCandidate == null
			? null
			: ApplyRepresentedCurrentTarget(
				queuedCandidate,
				currentTarget,
				currentTargetDistanceMeters,
				currentTargetGeoCanSee,
				currentTargetIsSupport,
				currentTargetIsFriend,
				helpFriendCandidates);

		return PreviewMercenaryNextNpcSkillSelectionFromCandidateMetadata(
			knownObject,
			fightStartingTimeMilliseconds,
			initialSkillDelayMilliseconds,
			currentTimeMilliseconds,
			isInCastSubState,
			projectedCandidates,
			hpPercentage,
			projectedQueuedCandidate,
			lastSkill,
			lastSkillLastTimeUsedMilliseconds,
			ownerExists,
			ownerIsDead,
			ownerIsAboutToDie);
	}

	public PlayerSummonKnownObjectNpcSkillPostSpawnPreview PreviewMercenaryNpcSkillPostSpawn(
		PlayerSummonKnownObjectNpcSkillTemplateProjection? skill,
		bool ownerIsDead = false,
		bool ownerIsAboutToDie = false)
	{
		// Java parity: NpcSkillTemplateEntry.fireOnEndCastEvents returns when spawn is absent or owner cannot spawn safely.
		if (skill?.SpawnTemplate == null)
			return PlayerSummonKnownObjectNpcSkillPostSpawnPreview.NoSpawnTemplate(skill);

		if (ownerIsDead || ownerIsAboutToDie)
			return PlayerSummonKnownObjectNpcSkillPostSpawnPreview.OwnerNotReady(skill, skill.SpawnTemplate);

		return skill.SpawnTemplate.DelayMilliseconds == 0
			? PlayerSummonKnownObjectNpcSkillPostSpawnPreview.ImmediateSpawn(skill, skill.SpawnTemplate)
			: PlayerSummonKnownObjectNpcSkillPostSpawnPreview.DelayedSpawn(skill, skill.SpawnTemplate);
	}

	public PlayerSummonKnownObjectNpcSkillPostSpawnScheduleResult PreviewMercenaryNpcSkillPostSpawnSchedule(
		PlayerSummonKnownObjectNpcSkillPostSpawnPreview? postSpawnPreview,
		long currentTimeMilliseconds)
	{
		if (postSpawnPreview == null)
			return PlayerSummonKnownObjectNpcSkillPostSpawnScheduleResult.MissingPreview(currentTimeMilliseconds);

		// Java parity: ThreadPoolManager.schedule is only used for delayed fireOnEndCastEvents spawn previews.
		if (!postSpawnPreview.ShouldScheduleSpawn || postSpawnPreview.SpawnTemplate == null)
			return PlayerSummonKnownObjectNpcSkillPostSpawnScheduleResult.NotScheduled(postSpawnPreview, currentTimeMilliseconds);

		return PlayerSummonKnownObjectNpcSkillPostSpawnScheduleResult.Scheduled(
			postSpawnPreview,
			currentTimeMilliseconds,
			currentTimeMilliseconds + postSpawnPreview.SpawnTemplate.DelayMilliseconds);
	}

	public PlayerSummonKnownObjectNpcSkillSpawnExecutionPreview PreviewMercenaryNpcSkillPostSpawnExecution(
		PlayerSummonKnownObjectNpcSkillPostSpawnPreview? postSpawnPreview,
		PlayerSummonKnownObjectNpcSkillSpawnOrigin? origin,
		bool ownerIsDead = false,
		bool ownerIsAboutToDie = false)
	{
		if (postSpawnPreview == null)
			return PlayerSummonKnownObjectNpcSkillSpawnExecutionPreview.MissingPreview();

		if (postSpawnPreview.SpawnTemplate == null
			|| postSpawnPreview.Status == PlayerSummonKnownObjectNpcSkillPostSpawnPreviewStatus.NoSpawnTemplate)
		{
			return PlayerSummonKnownObjectNpcSkillSpawnExecutionPreview.NoSpawn(postSpawnPreview);
		}

		if (ownerIsDead
			|| ownerIsAboutToDie
			|| postSpawnPreview.Status == PlayerSummonKnownObjectNpcSkillPostSpawnPreviewStatus.OwnerNotReady)
		{
			return PlayerSummonKnownObjectNpcSkillSpawnExecutionPreview.OwnerNotReady(postSpawnPreview);
		}

		if (origin == null)
			return PlayerSummonKnownObjectNpcSkillSpawnExecutionPreview.MissingOrigin(postSpawnPreview);

		// Java parity: NpcSkillTemplateEntry.spawnNpc uses the owner's world/instance/position/heading
		// to create SpawnEngine.newSingleTimeSpawn before SpawnEngine.spawnObject.
		return PlayerSummonKnownObjectNpcSkillSpawnExecutionPreview.Spawnable(postSpawnPreview, origin);
	}

	public PlayerSummonKnownObjectNpcSkillSpawnLocationPreview PreviewMercenaryNpcSkillPostSpawnLocation(
		PlayerSummonKnownObjectNpcSkillSpawnExecutionPreview? executionPreview,
		float? randomAngleDegrees = null,
		int? randomDistance = null)
	{
		if (executionPreview == null)
			return PlayerSummonKnownObjectNpcSkillSpawnLocationPreview.MissingExecution();

		if (!executionPreview.WouldSpawn
			|| executionPreview.Origin == null
			|| executionPreview.SpawnTemplate == null)
		{
			return PlayerSummonKnownObjectNpcSkillSpawnLocationPreview.NotSpawnable(executionPreview);
		}

		var origin = executionPreview.Origin;
		var spawn = executionPreview.SpawnTemplate;
		var headingAngleDegrees = ConvertJavaHeadingToAngle(origin.Heading);
		if (spawn.MinDistance <= 0)
		{
			return PlayerSummonKnownObjectNpcSkillSpawnLocationPreview.Projected(
				executionPreview,
				origin,
				headingAngleDegrees,
				randomAngleDegrees: null,
				distance: 0,
				offsetX: 0,
				offsetY: 0,
				origin.X,
				origin.Y,
				origin.Z);
		}

		if (randomAngleDegrees == null)
			return PlayerSummonKnownObjectNpcSkillSpawnLocationPreview.MissingRandomAngle(executionPreview, headingAngleDegrees);

		if (spawn.MaxDistance > 0 && randomDistance == null)
			return PlayerSummonKnownObjectNpcSkillSpawnLocationPreview.MissingRandomDistance(executionPreview, headingAngleDegrees, randomAngleDegrees.Value);

		var distance = spawn.MaxDistance > 0 ? randomDistance!.Value : spawn.MinDistance;
		var radian = Math.PI / 180d * (headingAngleDegrees + randomAngleDegrees.Value);
		var offsetX = (float)(Math.Cos(radian) * distance);
		var offsetY = (float)(Math.Sin(radian) * distance);

		// Java parity: NpcSkillTemplateEntry.spawnNpc passes npc.getZ() unchanged and keeps npc.getHeading().
		return PlayerSummonKnownObjectNpcSkillSpawnLocationPreview.Projected(
			executionPreview,
			origin,
			headingAngleDegrees,
			randomAngleDegrees,
			distance,
			offsetX,
			offsetY,
			origin.X + offsetX,
			origin.Y + offsetY,
			origin.Z);
	}

	public PlayerSummonKnownObjectNpcSkillSpawnTemplatePreview PreviewMercenaryNpcSkillSpawnTemplate(
		PlayerSummonKnownObjectNpcSkillSpawnLocationPreview? locationPreview,
		bool creatorHasSpawn = false,
		bool creatorSpawnHasEventTemplate = false)
	{
		if (locationPreview == null)
			return PlayerSummonKnownObjectNpcSkillSpawnTemplatePreview.MissingLocation();

		if (!locationPreview.HasProjectedLocation
			|| locationPreview.Origin == null
			|| locationPreview.ExecutionPreview?.NpcId == null
			|| locationPreview.SpawnX == null
			|| locationPreview.SpawnY == null
			|| locationPreview.SpawnZ == null
			|| locationPreview.Heading == null)
		{
			return PlayerSummonKnownObjectNpcSkillSpawnTemplatePreview.NotReady(locationPreview);
		}

		// Java parity: SpawnEngine.newSingleTimeSpawn(..., creator, null) copies creator id,
		// carries creator.getSpawn().getEventTemplate() when present, and builds a no-respawn SpawnTemplate.
		return PlayerSummonKnownObjectNpcSkillSpawnTemplatePreview.Created(
			locationPreview,
			locationPreview.WorldId!.Value,
			locationPreview.ExecutionPreview.NpcId.Value,
			locationPreview.SpawnX.Value,
			locationPreview.SpawnY.Value,
			locationPreview.SpawnZ.Value,
			locationPreview.Heading.Value,
			locationPreview.InstanceId!.Value,
			locationPreview.Origin.CreatorObjectId,
			creatorHasSpawn && creatorSpawnHasEventTemplate);
	}

	public PlayerSummonKnownObjectNpcSkillSpawnObjectDispatchPreview PreviewMercenaryNpcSkillSpawnObjectDispatch(
		PlayerSummonKnownObjectNpcSkillSpawnTemplatePreview? spawnTemplatePreview,
		PlayerSummonKnownObjectNpcSkillSpawnTemplateKind templateKind = PlayerSummonKnownObjectNpcSkillSpawnTemplateKind.Generic)
	{
		if (spawnTemplatePreview == null)
			return PlayerSummonKnownObjectNpcSkillSpawnObjectDispatchPreview.MissingTemplate();

		if (!spawnTemplatePreview.WouldCreateSpawnTemplate
			|| spawnTemplatePreview.NpcId == null
			|| spawnTemplatePreview.InstanceId == null)
		{
			return PlayerSummonKnownObjectNpcSkillSpawnObjectDispatchPreview.NotReady(spawnTemplatePreview, templateKind);
		}

		// Java parity: SpawnEngine.getSpawnedObject checks gatherable NPC ids before template subtype dispatch.
		var branch = spawnTemplatePreview.NpcId is > 400000 and < 499999
			? PlayerSummonKnownObjectNpcSkillSpawnObjectDispatchBranch.Gatherable
			: templateKind switch
			{
				PlayerSummonKnownObjectNpcSkillSpawnTemplateKind.Rift => PlayerSummonKnownObjectNpcSkillSpawnObjectDispatchBranch.RiftNpc,
				PlayerSummonKnownObjectNpcSkillSpawnTemplateKind.Siege => PlayerSummonKnownObjectNpcSkillSpawnObjectDispatchBranch.SiegeNpc,
				PlayerSummonKnownObjectNpcSkillSpawnTemplateKind.Vortex => PlayerSummonKnownObjectNpcSkillSpawnObjectDispatchBranch.InvasionNpc,
				_ => PlayerSummonKnownObjectNpcSkillSpawnObjectDispatchBranch.Npc,
			};

		return PlayerSummonKnownObjectNpcSkillSpawnObjectDispatchPreview.Dispatch(
			spawnTemplatePreview,
			templateKind,
			branch,
			spawnTemplatePreview.NpcId.Value,
			spawnTemplatePreview.InstanceId.Value);
	}

	public PlayerSummonKnownObjectNpcSkillNpcCreationPreview PreviewMercenaryNpcSkillOrdinaryNpcCreation(
		PlayerSummonKnownObjectNpcSkillSpawnObjectDispatchPreview? dispatchPreview,
		bool npcTemplateExists,
		bool npcTemplateIsFlag = false,
		bool walkerFormatorBroughtIntoWorld = false)
	{
		if (dispatchPreview == null)
			return PlayerSummonKnownObjectNpcSkillNpcCreationPreview.MissingDispatch();

		if (dispatchPreview.Branch != PlayerSummonKnownObjectNpcSkillSpawnObjectDispatchBranch.Npc
			|| dispatchPreview.SpawnTemplatePreview == null
			|| dispatchPreview.NpcId == null
			|| dispatchPreview.InstanceId == null)
		{
			return PlayerSummonKnownObjectNpcSkillNpcCreationPreview.NotOrdinaryNpc(dispatchPreview);
		}

		if (!npcTemplateExists)
			return PlayerSummonKnownObjectNpcSkillNpcCreationPreview.MissingNpcTemplate(dispatchPreview);

		// Java parity: VisibleObjectSpawner.spawnNpc creates NpcController/Npc, copies creator id,
		// chooses FlagKnownList for FLAG templates otherwise NpcKnownList, then installs EffectController.
		return PlayerSummonKnownObjectNpcSkillNpcCreationPreview.Created(
			dispatchPreview,
			dispatchPreview.NpcId.Value,
			dispatchPreview.InstanceId.Value,
			dispatchPreview.SpawnTemplatePreview.CreatorObjectId,
			npcTemplateIsFlag
				? PlayerSummonKnownObjectNpcSkillNpcKnownListKind.FlagKnownList
				: PlayerSummonKnownObjectNpcSkillNpcKnownListKind.NpcKnownList,
			walkerFormatorBroughtIntoWorld);
	}

	public PlayerSummonKnownObjectNpcSkillWorldInsertionPreview PreviewMercenaryNpcSkillBringIntoWorld(
		PlayerSummonKnownObjectNpcSkillNpcCreationPreview? npcCreationPreview,
		bool mapExists = true,
		bool instanceExists = true,
		bool regionExists = true,
		bool objectAlreadySpawned = false)
	{
		if (npcCreationPreview == null)
			return PlayerSummonKnownObjectNpcSkillWorldInsertionPreview.MissingCreation();

		var spawnTemplate = npcCreationPreview.DispatchPreview?.SpawnTemplatePreview;
		if (!npcCreationPreview.WouldCreateNpc
			|| !npcCreationPreview.RequiresBringIntoWorld
			|| spawnTemplate == null
			|| spawnTemplate.WorldId == null
			|| spawnTemplate.InstanceId == null
			|| spawnTemplate.X == null
			|| spawnTemplate.Y == null
			|| spawnTemplate.Z == null
			|| spawnTemplate.Heading == null)
		{
			return PlayerSummonKnownObjectNpcSkillWorldInsertionPreview.NotReady(npcCreationPreview);
		}

		if (!mapExists)
			return PlayerSummonKnownObjectNpcSkillWorldInsertionPreview.InvalidMap(npcCreationPreview, spawnTemplate.WorldId.Value);

		if (!instanceExists)
			return PlayerSummonKnownObjectNpcSkillWorldInsertionPreview.InvalidInstance(
				npcCreationPreview,
				spawnTemplate.WorldId.Value,
				spawnTemplate.InstanceId.Value);

		if (!regionExists)
			return PlayerSummonKnownObjectNpcSkillWorldInsertionPreview.InvalidRegion(
				npcCreationPreview,
				spawnTemplate.WorldId.Value,
				spawnTemplate.InstanceId.Value,
				spawnTemplate.X.Value,
				spawnTemplate.Y.Value,
				spawnTemplate.Z.Value,
				spawnTemplate.Heading.Value);

		if (objectAlreadySpawned)
			return PlayerSummonKnownObjectNpcSkillWorldInsertionPreview.AlreadySpawned(npcCreationPreview);

		// Java parity: SpawnEngine.bringIntoWorld calls World.storeObject, World.setPosition, then World.spawn.
		return PlayerSummonKnownObjectNpcSkillWorldInsertionPreview.WouldInsert(
			npcCreationPreview,
			spawnTemplate.WorldId.Value,
			spawnTemplate.InstanceId.Value,
			spawnTemplate.X.Value,
			spawnTemplate.Y.Value,
			spawnTemplate.Z.Value,
			spawnTemplate.Heading.Value);
	}

	public PlayerSummonKnownObjectNpcSkillPostSpawnCallbackPreview PreviewMercenaryNpcSkillPostSpawnCallbacks(
		PlayerSummonKnownObjectNpcSkillWorldInsertionPreview? worldInsertionPreview,
		bool spawnedObjectReturned,
		bool spawnedObjectHasSpawn = true,
		bool spawnIsTemporary = false,
		bool spawnedObjectIsSpawned = true)
	{
		if (worldInsertionPreview == null)
			return PlayerSummonKnownObjectNpcSkillPostSpawnCallbackPreview.MissingWorldInsertion();

		if (!worldInsertionPreview.WouldSpawn)
			return PlayerSummonKnownObjectNpcSkillPostSpawnCallbackPreview.NotReady(worldInsertionPreview);

		if (!spawnedObjectReturned)
			return PlayerSummonKnownObjectNpcSkillPostSpawnCallbackPreview.NoSpawnedObject(worldInsertionPreview);

		// Java parity: SpawnEngine.spawnObject post-processing only runs for non-null visObj.
		return PlayerSummonKnownObjectNpcSkillPostSpawnCallbackPreview.Callbacks(
			worldInsertionPreview,
			shouldRegisterTemporarySpawn: spawnedObjectHasSpawn && spawnIsTemporary,
			shouldInvokeInstanceOnSpawn: spawnedObjectIsSpawned,
			spawnedObjectHasSpawn,
			spawnIsTemporary,
			spawnedObjectIsSpawned);
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

	private PlayerSummonKnownObjectNpcSkillCandidateMetadata ApplyRepresentedCurrentTarget(
		PlayerSummonKnownObjectNpcSkillCandidateMetadata candidate,
		PlayerSummonKnownObject currentTarget,
		double currentTargetDistanceMeters,
		bool? currentTargetGeoCanSee,
		bool currentTargetIsSupport,
		bool currentTargetIsFriend,
		IEnumerable<PlayerSummonKnownObjectNpcSkillHelpFriendCandidate>? helpFriendCandidates = null)
	{
		var condition = candidate.Template.ConditionTemplate ?? new PlayerSummonKnownObjectNpcSkillConditionMetadata();
		var projectedHelpFriendCandidates = candidate.HelpFriendCandidates
			?? (condition.Condition == PlayerSummonKnownObjectNpcSkillCondition.HelpFriend
				? helpFriendCandidates?.ToArray()
				: null);
		if (candidate.ConditionTarget != null)
		{
			return projectedHelpFriendCandidates == candidate.HelpFriendCandidates
				? candidate
				: candidate with { HelpFriendCandidates = projectedHelpFriendCandidates };
		}

		if (condition.Condition == PlayerSummonKnownObjectNpcSkillCondition.HelpFriend)
		{
			return candidate with { HelpFriendCandidates = projectedHelpFriendCandidates };
		}

		if (condition.Condition == PlayerSummonKnownObjectNpcSkillCondition.NpcIsAlive)
			return candidate;
		var target = ProjectMercenaryNpcSkillConditionTarget(
			currentTarget,
			condition,
			currentTargetDistanceMeters,
			currentTargetGeoCanSee,
			currentTargetIsSupport,
			currentTargetIsFriend);
		return candidate with { ConditionTarget = target };
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

	private static PlayerSummonKnownObjectNpcSkillConditionReadiness MatchCarvedSignetCondition(
		PlayerSummonKnownObjectNpcSkillCondition condition,
		PlayerSummonKnownObjectNpcSkillConditionTarget? target,
		IEnumerable<string> skillTemplateSignetBurstStacks)
	{
		if (target == null)
			return PlayerSummonKnownObjectNpcSkillConditionReadiness.MissingTarget(condition);

		var requiredLevelExclusive = GetCarvedSignetRequiredLevelExclusive(condition);
		var burstStacks = skillTemplateSignetBurstStacks.ToArray();
		var targetSignets = target.CarvedSignets ?? Array.Empty<PlayerSummonKnownObjectNpcSkillCarvedSignetState>();
		var ready = target.IsCreature
			&& !target.IsDead
			&& !target.IsAboutToDie
			&& burstStacks.Any(signet => targetSignets.Any(effect => effect.SkillLevel > requiredLevelExclusive && string.Equals(effect.Signet, signet, StringComparison.Ordinal)));

		return ready
			? PlayerSummonKnownObjectNpcSkillConditionReadiness.Ready(condition, target)
			: PlayerSummonKnownObjectNpcSkillConditionReadiness.NotReady(condition, target);
	}

	private static bool IsCarvedSignetCondition(PlayerSummonKnownObjectNpcSkillCondition condition)
	{
		return condition
			is PlayerSummonKnownObjectNpcSkillCondition.TargetHasCarvedSignet
			or PlayerSummonKnownObjectNpcSkillCondition.TargetHasCarvedSignetLevelIi
			or PlayerSummonKnownObjectNpcSkillCondition.TargetHasCarvedSignetLevelIii
			or PlayerSummonKnownObjectNpcSkillCondition.TargetHasCarvedSignetLevelIv
			or PlayerSummonKnownObjectNpcSkillCondition.TargetHasCarvedSignetLevelV;
	}

	private static int GetCarvedSignetRequiredLevelExclusive(PlayerSummonKnownObjectNpcSkillCondition condition)
	{
		return condition switch
		{
			PlayerSummonKnownObjectNpcSkillCondition.TargetHasCarvedSignet => 0,
			PlayerSummonKnownObjectNpcSkillCondition.TargetHasCarvedSignetLevelIi => 1,
			PlayerSummonKnownObjectNpcSkillCondition.TargetHasCarvedSignetLevelIii => 2,
			PlayerSummonKnownObjectNpcSkillCondition.TargetHasCarvedSignetLevelIv => 3,
			PlayerSummonKnownObjectNpcSkillCondition.TargetHasCarvedSignetLevelV => 4,
			_ => 0,
		};
	}

	private static bool IsMercenaryHelpFriendCandidateReady(
		PlayerSummonKnownObjectNpcSkillConditionMetadata conditionMetadata,
		PlayerSummonKnownObjectNpcSkillHelpFriendCandidate candidate)
	{
		// Java parity: HELP_FRIEND requires visible Creature, alive, support/friend relation, hp threshold, range, and GeoService.canSee.
		return candidate.IsVisible
			&& candidate.IsCreature
			&& !candidate.IsDead
			&& !candidate.IsAboutToDie
			&& (candidate.IsSupport || candidate.IsFriend)
			&& candidate.HpPercentage <= conditionMetadata.HpBelowPercentage
			&& candidate.DistanceMeters <= conditionMetadata.RangeMeters
			&& candidate.GeoCanSee;
	}

	private static PlayerSummonKnownObjectNpcSkillConditionTargetKind ProjectMercenaryNpcSkillConditionTargetKind(
		PlayerSummonKnownObject knownObject)
	{
		return knownObject.Kind == PlayerSummonKnownObjectKind.Creature
			? PlayerSummonKnownObjectNpcSkillConditionTargetKind.Npc
			: PlayerSummonKnownObjectNpcSkillConditionTargetKind.Unknown;
	}

	private static float ConvertJavaHeadingToAngle(byte heading)
	{
		var angle = heading * 3f;
		if (angle >= 360)
			angle %= 360;
		else if (angle < 0)
			angle += 360;
		return angle;
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

	public PlayerSummonKnownObjectTargetRangeReadiness EvaluateMercenaryTargetRange(
		PlayerSummonKnownObject knownObject,
		PlayerSummonKnownObjectSkillTargetMode targetMode,
		PlayerSummonKnownObject? currentTarget,
		bool canSeeTarget = true,
		bool isAreaTarget = false,
		bool isInRange = true)
	{
		if (targetMode != PlayerSummonKnownObjectSkillTargetMode.CreatureTarget)
			return PlayerSummonKnownObjectTargetRangeReadiness.NotRequired(knownObject);

		// Java parity: SkillAttackManager.targetTooFar requires owner.getTarget() instanceof Creature.
		if (currentTarget == null || !currentTarget.IsCreature)
			return PlayerSummonKnownObjectTargetRangeReadiness.MissingCreatureTarget(knownObject, TargetTooFarNextSkillDelayMilliseconds);

		if (currentTarget.IsDead)
			return PlayerSummonKnownObjectTargetRangeReadiness.TargetDead(knownObject, TargetTooFarNextSkillDelayMilliseconds);

		// Java parity: targetTooFar delegates visibility to owner.canSee(target); represented visibility is kept as an explicit input fact.
		if (!currentTarget.IsVisible || !canSeeTarget)
			return PlayerSummonKnownObjectTargetRangeReadiness.CannotSeeTarget(knownObject, TargetTooFarNextSkillDelayMilliseconds);

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

public sealed record PlayerSummonKnownObjectNpcSkillPreviewCaptureResult(
	PlayerSummonKnownObjectNpcSkillPreviewCaptureStatus Status,
	int MercenaryObjectId)
{
	public static PlayerSummonKnownObjectNpcSkillPreviewCaptureResult Captured(int mercenaryObjectId)
	{
		return new PlayerSummonKnownObjectNpcSkillPreviewCaptureResult(
			PlayerSummonKnownObjectNpcSkillPreviewCaptureStatus.Captured,
			mercenaryObjectId);
	}

	public static PlayerSummonKnownObjectNpcSkillPreviewCaptureResult MissingKnownObject(int mercenaryObjectId)
	{
		return new PlayerSummonKnownObjectNpcSkillPreviewCaptureResult(
			PlayerSummonKnownObjectNpcSkillPreviewCaptureStatus.MissingKnownObject,
			mercenaryObjectId);
	}
}

public enum PlayerSummonKnownObjectNpcSkillPreviewCaptureStatus
{
	Captured,
	MissingKnownObject,
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
	PlayerSummonKnownObjectNpcSkillSpawnMetadata? SpawnTemplate = null,
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
	bool IsPostSpawn,
	PlayerSummonKnownObjectNpcSkillSpawnMetadata? SpawnTemplate);

public sealed record PlayerSummonKnownObjectNpcSkillSpawnMetadata(
	int NpcId = 0,
	int DelayMilliseconds = 0,
	int MinDistance = 0,
	int MaxDistance = 0,
	int MinCount = 1,
	int MaxCount = 0);

public sealed record PlayerSummonKnownObjectNpcSkillPostSpawnPreview(
	PlayerSummonKnownObjectNpcSkillPostSpawnPreviewStatus Status,
	PlayerSummonKnownObjectNpcSkillTemplateProjection? Skill,
	PlayerSummonKnownObjectNpcSkillSpawnMetadata? SpawnTemplate = null)
{
	public bool ShouldScheduleSpawn => Status == PlayerSummonKnownObjectNpcSkillPostSpawnPreviewStatus.DelayedSpawn;
	public bool ShouldSpawnImmediately => Status == PlayerSummonKnownObjectNpcSkillPostSpawnPreviewStatus.ImmediateSpawn;
	public bool RequiresOwnerAliveRecheck => ShouldScheduleSpawn;
	public bool RequiresRandomCount => SpawnTemplate is { MaxCount: > 1 };
	public bool RequiresRandomDistance => SpawnTemplate is { MinDistance: > 0, MaxDistance: > 0 };
	public bool RequiresRandomAngle => SpawnTemplate is { MinDistance: > 0 };
	public int? EffectiveMinCount => SpawnTemplate?.MinCount;
	public int? EffectiveMaxCount => SpawnTemplate == null
		? null
		: RequiresRandomCount ? SpawnTemplate.MaxCount : SpawnTemplate.MinCount;
	public int? EffectiveMinDistance => SpawnTemplate?.MinDistance;
	public int? EffectiveMaxDistance => SpawnTemplate == null
		? null
		: RequiresRandomDistance ? SpawnTemplate.MaxDistance : SpawnTemplate.MinDistance;

	public static PlayerSummonKnownObjectNpcSkillPostSpawnPreview NoSpawnTemplate(
		PlayerSummonKnownObjectNpcSkillTemplateProjection? skill)
	{
		return new PlayerSummonKnownObjectNpcSkillPostSpawnPreview(
			PlayerSummonKnownObjectNpcSkillPostSpawnPreviewStatus.NoSpawnTemplate,
			skill);
	}

	public static PlayerSummonKnownObjectNpcSkillPostSpawnPreview OwnerNotReady(
		PlayerSummonKnownObjectNpcSkillTemplateProjection skill,
		PlayerSummonKnownObjectNpcSkillSpawnMetadata spawnTemplate)
	{
		return new PlayerSummonKnownObjectNpcSkillPostSpawnPreview(
			PlayerSummonKnownObjectNpcSkillPostSpawnPreviewStatus.OwnerNotReady,
			skill,
			spawnTemplate);
	}

	public static PlayerSummonKnownObjectNpcSkillPostSpawnPreview ImmediateSpawn(
		PlayerSummonKnownObjectNpcSkillTemplateProjection skill,
		PlayerSummonKnownObjectNpcSkillSpawnMetadata spawnTemplate)
	{
		return new PlayerSummonKnownObjectNpcSkillPostSpawnPreview(
			PlayerSummonKnownObjectNpcSkillPostSpawnPreviewStatus.ImmediateSpawn,
			skill,
			spawnTemplate);
	}

	public static PlayerSummonKnownObjectNpcSkillPostSpawnPreview DelayedSpawn(
		PlayerSummonKnownObjectNpcSkillTemplateProjection skill,
		PlayerSummonKnownObjectNpcSkillSpawnMetadata spawnTemplate)
	{
		return new PlayerSummonKnownObjectNpcSkillPostSpawnPreview(
			PlayerSummonKnownObjectNpcSkillPostSpawnPreviewStatus.DelayedSpawn,
			skill,
			spawnTemplate);
	}
}

public enum PlayerSummonKnownObjectNpcSkillPostSpawnPreviewStatus
{
	NoSpawnTemplate,
	OwnerNotReady,
	ImmediateSpawn,
	DelayedSpawn,
}

public sealed record PlayerSummonKnownObjectNpcSkillPostSpawnScheduleResult(
	PlayerSummonKnownObjectNpcSkillPostSpawnScheduleStatus Status,
	long CurrentTimeMilliseconds,
	PlayerSummonKnownObjectNpcSkillPostSpawnPreview? PostSpawnPreview = null,
	long? ScheduledAtMilliseconds = null)
{
	public bool WouldScheduleTask => Status == PlayerSummonKnownObjectNpcSkillPostSpawnScheduleStatus.Scheduled;
	public bool RequiresOwnerAliveRecheck => WouldScheduleTask && PostSpawnPreview is { RequiresOwnerAliveRecheck: true };
	public int? DelayMilliseconds => PostSpawnPreview?.SpawnTemplate?.DelayMilliseconds;

	public static PlayerSummonKnownObjectNpcSkillPostSpawnScheduleResult MissingPreview(long currentTimeMilliseconds)
	{
		return new PlayerSummonKnownObjectNpcSkillPostSpawnScheduleResult(
			PlayerSummonKnownObjectNpcSkillPostSpawnScheduleStatus.MissingPreview,
			currentTimeMilliseconds);
	}

	public static PlayerSummonKnownObjectNpcSkillPostSpawnScheduleResult NotScheduled(
		PlayerSummonKnownObjectNpcSkillPostSpawnPreview postSpawnPreview,
		long currentTimeMilliseconds)
	{
		return new PlayerSummonKnownObjectNpcSkillPostSpawnScheduleResult(
			PlayerSummonKnownObjectNpcSkillPostSpawnScheduleStatus.NotScheduled,
			currentTimeMilliseconds,
			postSpawnPreview);
	}

	public static PlayerSummonKnownObjectNpcSkillPostSpawnScheduleResult Scheduled(
		PlayerSummonKnownObjectNpcSkillPostSpawnPreview postSpawnPreview,
		long currentTimeMilliseconds,
		long scheduledAtMilliseconds)
	{
		return new PlayerSummonKnownObjectNpcSkillPostSpawnScheduleResult(
			PlayerSummonKnownObjectNpcSkillPostSpawnScheduleStatus.Scheduled,
			currentTimeMilliseconds,
			postSpawnPreview,
			scheduledAtMilliseconds);
	}
}

public enum PlayerSummonKnownObjectNpcSkillPostSpawnScheduleStatus
{
	MissingPreview,
	NotScheduled,
	Scheduled,
}

public sealed record PlayerSummonKnownObjectNpcSkillSpawnOrigin(
	int WorldId,
	int InstanceId,
	float X,
	float Y,
	float Z,
	byte Heading,
	int CreatorObjectId = 0);

public sealed record PlayerSummonKnownObjectNpcSkillSpawnExecutionPreview(
	PlayerSummonKnownObjectNpcSkillSpawnExecutionPreviewStatus Status,
	PlayerSummonKnownObjectNpcSkillPostSpawnPreview? PostSpawnPreview = null,
	PlayerSummonKnownObjectNpcSkillSpawnOrigin? Origin = null)
{
	public PlayerSummonKnownObjectNpcSkillSpawnMetadata? SpawnTemplate => PostSpawnPreview?.SpawnTemplate;
	public bool WouldSpawn => Status == PlayerSummonKnownObjectNpcSkillSpawnExecutionPreviewStatus.WouldSpawn;
	public bool RequiresSpawnEngine => WouldSpawn;
	public bool RequiresInstanceSpawn => WouldSpawn;
	public bool RequiresRandomCount => WouldSpawn && PostSpawnPreview is { RequiresRandomCount: true };
	public bool RequiresRandomDistance => WouldSpawn && PostSpawnPreview is { RequiresRandomDistance: true };
	public bool RequiresRandomAngle => WouldSpawn && PostSpawnPreview is { RequiresRandomAngle: true };
	public bool UsesOwnerPosition => WouldSpawn && Origin != null;
	public bool RequiresOwnerAliveRecheck => WouldSpawn && PostSpawnPreview is { RequiresOwnerAliveRecheck: true };
	public int? NpcId => SpawnTemplate?.NpcId;
	public int? EffectiveMinCount => WouldSpawn ? PostSpawnPreview?.EffectiveMinCount : null;
	public int? EffectiveMaxCount => WouldSpawn ? PostSpawnPreview?.EffectiveMaxCount : null;
	public int? EffectiveMinDistance => WouldSpawn ? PostSpawnPreview?.EffectiveMinDistance : null;
	public int? EffectiveMaxDistance => WouldSpawn ? PostSpawnPreview?.EffectiveMaxDistance : null;

	public static PlayerSummonKnownObjectNpcSkillSpawnExecutionPreview MissingPreview()
	{
		return new PlayerSummonKnownObjectNpcSkillSpawnExecutionPreview(
			PlayerSummonKnownObjectNpcSkillSpawnExecutionPreviewStatus.MissingPreview);
	}

	public static PlayerSummonKnownObjectNpcSkillSpawnExecutionPreview NoSpawn(
		PlayerSummonKnownObjectNpcSkillPostSpawnPreview postSpawnPreview)
	{
		return new PlayerSummonKnownObjectNpcSkillSpawnExecutionPreview(
			PlayerSummonKnownObjectNpcSkillSpawnExecutionPreviewStatus.NoSpawn,
			postSpawnPreview);
	}

	public static PlayerSummonKnownObjectNpcSkillSpawnExecutionPreview OwnerNotReady(
		PlayerSummonKnownObjectNpcSkillPostSpawnPreview postSpawnPreview)
	{
		return new PlayerSummonKnownObjectNpcSkillSpawnExecutionPreview(
			PlayerSummonKnownObjectNpcSkillSpawnExecutionPreviewStatus.OwnerNotReady,
			postSpawnPreview);
	}

	public static PlayerSummonKnownObjectNpcSkillSpawnExecutionPreview MissingOrigin(
		PlayerSummonKnownObjectNpcSkillPostSpawnPreview postSpawnPreview)
	{
		return new PlayerSummonKnownObjectNpcSkillSpawnExecutionPreview(
			PlayerSummonKnownObjectNpcSkillSpawnExecutionPreviewStatus.MissingOrigin,
			postSpawnPreview);
	}

	public static PlayerSummonKnownObjectNpcSkillSpawnExecutionPreview Spawnable(
		PlayerSummonKnownObjectNpcSkillPostSpawnPreview postSpawnPreview,
		PlayerSummonKnownObjectNpcSkillSpawnOrigin origin)
	{
		return new PlayerSummonKnownObjectNpcSkillSpawnExecutionPreview(
			PlayerSummonKnownObjectNpcSkillSpawnExecutionPreviewStatus.WouldSpawn,
			postSpawnPreview,
			origin);
	}
}

public enum PlayerSummonKnownObjectNpcSkillSpawnExecutionPreviewStatus
{
	MissingPreview,
	NoSpawn,
	OwnerNotReady,
	MissingOrigin,
	WouldSpawn,
}

public sealed record PlayerSummonKnownObjectNpcSkillSpawnLocationPreview(
	PlayerSummonKnownObjectNpcSkillSpawnLocationPreviewStatus Status,
	PlayerSummonKnownObjectNpcSkillSpawnExecutionPreview? ExecutionPreview = null,
	PlayerSummonKnownObjectNpcSkillSpawnOrigin? Origin = null,
	float? HeadingAngleDegrees = null,
	float? RandomAngleDegrees = null,
	int? Distance = null,
	float? OffsetX = null,
	float? OffsetY = null,
	float? SpawnX = null,
	float? SpawnY = null,
	float? SpawnZ = null)
{
	public bool HasProjectedLocation => Status == PlayerSummonKnownObjectNpcSkillSpawnLocationPreviewStatus.Projected;
	public bool RequiresJavaRandomAngle => Status == PlayerSummonKnownObjectNpcSkillSpawnLocationPreviewStatus.MissingRandomAngle;
	public bool RequiresJavaRandomDistance => Status == PlayerSummonKnownObjectNpcSkillSpawnLocationPreviewStatus.MissingRandomDistance;
	public byte? Heading => HasProjectedLocation ? Origin?.Heading : null;
	public int? WorldId => HasProjectedLocation ? Origin?.WorldId : null;
	public int? InstanceId => HasProjectedLocation ? Origin?.InstanceId : null;

	public static PlayerSummonKnownObjectNpcSkillSpawnLocationPreview MissingExecution()
	{
		return new PlayerSummonKnownObjectNpcSkillSpawnLocationPreview(
			PlayerSummonKnownObjectNpcSkillSpawnLocationPreviewStatus.MissingExecution);
	}

	public static PlayerSummonKnownObjectNpcSkillSpawnLocationPreview NotSpawnable(
		PlayerSummonKnownObjectNpcSkillSpawnExecutionPreview executionPreview)
	{
		return new PlayerSummonKnownObjectNpcSkillSpawnLocationPreview(
			PlayerSummonKnownObjectNpcSkillSpawnLocationPreviewStatus.NotSpawnable,
			executionPreview);
	}

	public static PlayerSummonKnownObjectNpcSkillSpawnLocationPreview MissingRandomAngle(
		PlayerSummonKnownObjectNpcSkillSpawnExecutionPreview executionPreview,
		float headingAngleDegrees)
	{
		return new PlayerSummonKnownObjectNpcSkillSpawnLocationPreview(
			PlayerSummonKnownObjectNpcSkillSpawnLocationPreviewStatus.MissingRandomAngle,
			executionPreview,
			executionPreview.Origin,
			headingAngleDegrees);
	}

	public static PlayerSummonKnownObjectNpcSkillSpawnLocationPreview MissingRandomDistance(
		PlayerSummonKnownObjectNpcSkillSpawnExecutionPreview executionPreview,
		float headingAngleDegrees,
		float randomAngleDegrees)
	{
		return new PlayerSummonKnownObjectNpcSkillSpawnLocationPreview(
			PlayerSummonKnownObjectNpcSkillSpawnLocationPreviewStatus.MissingRandomDistance,
			executionPreview,
			executionPreview.Origin,
			headingAngleDegrees,
			randomAngleDegrees);
	}

	public static PlayerSummonKnownObjectNpcSkillSpawnLocationPreview Projected(
		PlayerSummonKnownObjectNpcSkillSpawnExecutionPreview executionPreview,
		PlayerSummonKnownObjectNpcSkillSpawnOrigin origin,
		float headingAngleDegrees,
		float? randomAngleDegrees,
		int distance,
		float offsetX,
		float offsetY,
		float spawnX,
		float spawnY,
		float spawnZ)
	{
		return new PlayerSummonKnownObjectNpcSkillSpawnLocationPreview(
			PlayerSummonKnownObjectNpcSkillSpawnLocationPreviewStatus.Projected,
			executionPreview,
			origin,
			headingAngleDegrees,
			randomAngleDegrees,
			distance,
			offsetX,
			offsetY,
			spawnX,
			spawnY,
			spawnZ);
	}
}

public enum PlayerSummonKnownObjectNpcSkillSpawnLocationPreviewStatus
{
	MissingExecution,
	NotSpawnable,
	MissingRandomAngle,
	MissingRandomDistance,
	Projected,
}

public sealed record PlayerSummonKnownObjectNpcSkillSpawnTemplatePreview(
	PlayerSummonKnownObjectNpcSkillSpawnTemplatePreviewStatus Status,
	PlayerSummonKnownObjectNpcSkillSpawnLocationPreview? LocationPreview = null,
	int? WorldId = null,
	int? NpcId = null,
	float? X = null,
	float? Y = null,
	float? Z = null,
	byte? Heading = null,
	int? InstanceId = null,
	int RespawnTimeSeconds = 0,
	int CreatorObjectId = 0,
	bool CarriesCreatorEventTemplate = false,
	int RandomWalkRange = 0,
	string? WalkerId = null,
	int StaticId = 0,
	string? AiName = null)
{
	public bool WouldCreateSpawnTemplate => Status == PlayerSummonKnownObjectNpcSkillSpawnTemplatePreviewStatus.Created;
	public bool WouldSpawnObject => WouldCreateSpawnTemplate;
	public bool RequiresSpawnObjectCall => WouldCreateSpawnTemplate;
	public bool IsSingleTimeSpawn => WouldCreateSpawnTemplate && RespawnTimeSeconds == 0;
	public bool HasCreator => CreatorObjectId != 0;

	public static PlayerSummonKnownObjectNpcSkillSpawnTemplatePreview MissingLocation()
	{
		return new PlayerSummonKnownObjectNpcSkillSpawnTemplatePreview(
			PlayerSummonKnownObjectNpcSkillSpawnTemplatePreviewStatus.MissingLocation);
	}

	public static PlayerSummonKnownObjectNpcSkillSpawnTemplatePreview NotReady(
		PlayerSummonKnownObjectNpcSkillSpawnLocationPreview locationPreview)
	{
		return new PlayerSummonKnownObjectNpcSkillSpawnTemplatePreview(
			PlayerSummonKnownObjectNpcSkillSpawnTemplatePreviewStatus.NotReady,
			locationPreview);
	}

	public static PlayerSummonKnownObjectNpcSkillSpawnTemplatePreview Created(
		PlayerSummonKnownObjectNpcSkillSpawnLocationPreview locationPreview,
		int worldId,
		int npcId,
		float x,
		float y,
		float z,
		byte heading,
		int instanceId,
		int creatorObjectId,
		bool carriesCreatorEventTemplate)
	{
		return new PlayerSummonKnownObjectNpcSkillSpawnTemplatePreview(
			PlayerSummonKnownObjectNpcSkillSpawnTemplatePreviewStatus.Created,
			locationPreview,
			worldId,
			npcId,
			x,
			y,
			z,
			heading,
			instanceId,
			0,
			creatorObjectId,
			carriesCreatorEventTemplate,
			RandomWalkRange: 0,
			WalkerId: null,
			StaticId: 0,
			AiName: null);
	}
}

public enum PlayerSummonKnownObjectNpcSkillSpawnTemplatePreviewStatus
{
	MissingLocation,
	NotReady,
	Created,
}

public enum PlayerSummonKnownObjectNpcSkillSpawnTemplateKind
{
	Generic,
	Rift,
	Siege,
	Vortex,
}

public sealed record PlayerSummonKnownObjectNpcSkillSpawnObjectDispatchPreview(
	PlayerSummonKnownObjectNpcSkillSpawnObjectDispatchPreviewStatus Status,
	PlayerSummonKnownObjectNpcSkillSpawnTemplatePreview? SpawnTemplatePreview = null,
	PlayerSummonKnownObjectNpcSkillSpawnTemplateKind TemplateKind = PlayerSummonKnownObjectNpcSkillSpawnTemplateKind.Generic,
	PlayerSummonKnownObjectNpcSkillSpawnObjectDispatchBranch? Branch = null,
	int? NpcId = null,
	int? InstanceId = null)
{
	public bool WouldCallVisibleObjectSpawner => Status == PlayerSummonKnownObjectNpcSkillSpawnObjectDispatchPreviewStatus.Dispatch;
	public bool RequiresNpcTemplateLookup => Branch is
		PlayerSummonKnownObjectNpcSkillSpawnObjectDispatchBranch.Npc
		or PlayerSummonKnownObjectNpcSkillSpawnObjectDispatchBranch.RiftNpc
		or PlayerSummonKnownObjectNpcSkillSpawnObjectDispatchBranch.SiegeNpc
		or PlayerSummonKnownObjectNpcSkillSpawnObjectDispatchBranch.InvasionNpc;
	public bool RequiresGatherableSpawner => Branch == PlayerSummonKnownObjectNpcSkillSpawnObjectDispatchBranch.Gatherable;
	public bool RequiresRiftEnabledCheck => Branch == PlayerSummonKnownObjectNpcSkillSpawnObjectDispatchBranch.RiftNpc;
	public bool RequiresSiegeEnabledCheck => Branch == PlayerSummonKnownObjectNpcSkillSpawnObjectDispatchBranch.SiegeNpc;
	public bool RequiresVortexEnabledCheck => Branch == PlayerSummonKnownObjectNpcSkillSpawnObjectDispatchBranch.InvasionNpc;
	public bool RequiresBringIntoWorld => WouldCallVisibleObjectSpawner;
	public bool RequiresTemporarySpawnRegistrationCheck => WouldCallVisibleObjectSpawner;
	public bool RequiresInstanceOnSpawnCheck => WouldCallVisibleObjectSpawner;
	public bool MayReturnNull => Branch is
		PlayerSummonKnownObjectNpcSkillSpawnObjectDispatchBranch.Npc
		or PlayerSummonKnownObjectNpcSkillSpawnObjectDispatchBranch.RiftNpc
		or PlayerSummonKnownObjectNpcSkillSpawnObjectDispatchBranch.SiegeNpc
		or PlayerSummonKnownObjectNpcSkillSpawnObjectDispatchBranch.InvasionNpc;

	public static PlayerSummonKnownObjectNpcSkillSpawnObjectDispatchPreview MissingTemplate()
	{
		return new PlayerSummonKnownObjectNpcSkillSpawnObjectDispatchPreview(
			PlayerSummonKnownObjectNpcSkillSpawnObjectDispatchPreviewStatus.MissingTemplate);
	}

	public static PlayerSummonKnownObjectNpcSkillSpawnObjectDispatchPreview NotReady(
		PlayerSummonKnownObjectNpcSkillSpawnTemplatePreview spawnTemplatePreview,
		PlayerSummonKnownObjectNpcSkillSpawnTemplateKind templateKind)
	{
		return new PlayerSummonKnownObjectNpcSkillSpawnObjectDispatchPreview(
			PlayerSummonKnownObjectNpcSkillSpawnObjectDispatchPreviewStatus.NotReady,
			spawnTemplatePreview,
			templateKind);
	}

	public static PlayerSummonKnownObjectNpcSkillSpawnObjectDispatchPreview Dispatch(
		PlayerSummonKnownObjectNpcSkillSpawnTemplatePreview spawnTemplatePreview,
		PlayerSummonKnownObjectNpcSkillSpawnTemplateKind templateKind,
		PlayerSummonKnownObjectNpcSkillSpawnObjectDispatchBranch branch,
		int npcId,
		int instanceId)
	{
		return new PlayerSummonKnownObjectNpcSkillSpawnObjectDispatchPreview(
			PlayerSummonKnownObjectNpcSkillSpawnObjectDispatchPreviewStatus.Dispatch,
			spawnTemplatePreview,
			templateKind,
			branch,
			npcId,
			instanceId);
	}
}

public enum PlayerSummonKnownObjectNpcSkillSpawnObjectDispatchPreviewStatus
{
	MissingTemplate,
	NotReady,
	Dispatch,
}

public enum PlayerSummonKnownObjectNpcSkillSpawnObjectDispatchBranch
{
	Gatherable,
	RiftNpc,
	SiegeNpc,
	InvasionNpc,
	Npc,
}

public sealed record PlayerSummonKnownObjectNpcSkillNpcCreationPreview(
	PlayerSummonKnownObjectNpcSkillNpcCreationPreviewStatus Status,
	PlayerSummonKnownObjectNpcSkillSpawnObjectDispatchPreview? DispatchPreview = null,
	int? NpcId = null,
	int? InstanceId = null,
	int CreatorObjectId = 0,
	PlayerSummonKnownObjectNpcSkillNpcKnownListKind? KnownListKind = null,
	bool WalkerFormatorBroughtIntoWorld = false)
{
	public bool WouldCreateNpc => Status == PlayerSummonKnownObjectNpcSkillNpcCreationPreviewStatus.Created;
	public bool RequiresNpcController => WouldCreateNpc;
	public bool RequiresNpcObject => WouldCreateNpc;
	public bool RequiresCreatorIdCopy => WouldCreateNpc;
	public bool RequiresKnownList => WouldCreateNpc;
	public bool RequiresEffectController => WouldCreateNpc;
	public bool RequiresWalkerFormator => WouldCreateNpc;
	public bool RequiresBringIntoWorld => WouldCreateNpc && !WalkerFormatorBroughtIntoWorld;
	public bool RequiresControllerDeleteOnBringIntoWorldFailure => RequiresBringIntoWorld;
	public bool MayReturnNull => Status == PlayerSummonKnownObjectNpcSkillNpcCreationPreviewStatus.MissingNpcTemplate;
	public bool HasCreator => CreatorObjectId != 0;

	public static PlayerSummonKnownObjectNpcSkillNpcCreationPreview MissingDispatch()
	{
		return new PlayerSummonKnownObjectNpcSkillNpcCreationPreview(
			PlayerSummonKnownObjectNpcSkillNpcCreationPreviewStatus.MissingDispatch);
	}

	public static PlayerSummonKnownObjectNpcSkillNpcCreationPreview NotOrdinaryNpc(
		PlayerSummonKnownObjectNpcSkillSpawnObjectDispatchPreview dispatchPreview)
	{
		return new PlayerSummonKnownObjectNpcSkillNpcCreationPreview(
			PlayerSummonKnownObjectNpcSkillNpcCreationPreviewStatus.NotOrdinaryNpc,
			dispatchPreview);
	}

	public static PlayerSummonKnownObjectNpcSkillNpcCreationPreview MissingNpcTemplate(
		PlayerSummonKnownObjectNpcSkillSpawnObjectDispatchPreview dispatchPreview)
	{
		return new PlayerSummonKnownObjectNpcSkillNpcCreationPreview(
			PlayerSummonKnownObjectNpcSkillNpcCreationPreviewStatus.MissingNpcTemplate,
			dispatchPreview,
			dispatchPreview.NpcId,
			dispatchPreview.InstanceId);
	}

	public static PlayerSummonKnownObjectNpcSkillNpcCreationPreview Created(
		PlayerSummonKnownObjectNpcSkillSpawnObjectDispatchPreview dispatchPreview,
		int npcId,
		int instanceId,
		int creatorObjectId,
		PlayerSummonKnownObjectNpcSkillNpcKnownListKind knownListKind,
		bool walkerFormatorBroughtIntoWorld)
	{
		return new PlayerSummonKnownObjectNpcSkillNpcCreationPreview(
			PlayerSummonKnownObjectNpcSkillNpcCreationPreviewStatus.Created,
			dispatchPreview,
			npcId,
			instanceId,
			creatorObjectId,
			knownListKind,
			walkerFormatorBroughtIntoWorld);
	}
}

public enum PlayerSummonKnownObjectNpcSkillNpcCreationPreviewStatus
{
	MissingDispatch,
	NotOrdinaryNpc,
	MissingNpcTemplate,
	Created,
}

public enum PlayerSummonKnownObjectNpcSkillNpcKnownListKind
{
	NpcKnownList,
	FlagKnownList,
}

public sealed record PlayerSummonKnownObjectNpcSkillWorldInsertionPreview(
	PlayerSummonKnownObjectNpcSkillWorldInsertionPreviewStatus Status,
	PlayerSummonKnownObjectNpcSkillNpcCreationPreview? NpcCreationPreview = null,
	int? WorldId = null,
	int? InstanceId = null,
	float? X = null,
	float? Y = null,
	float? Z = null,
	byte? Heading = null)
{
	public bool WouldStoreObject => Status == PlayerSummonKnownObjectNpcSkillWorldInsertionPreviewStatus.WouldInsert;
	public bool WouldSetPosition => WouldStoreObject;
	public bool WouldSpawn => WouldStoreObject;
	public bool RequiresDuplicateObjectCheck => WouldStoreObject;
	public bool RequiresMapLookup => Status is not PlayerSummonKnownObjectNpcSkillWorldInsertionPreviewStatus.MissingCreation
		and not PlayerSummonKnownObjectNpcSkillWorldInsertionPreviewStatus.NotReady;
	public bool RequiresInstanceLookup => RequiresMapLookup && Status != PlayerSummonKnownObjectNpcSkillWorldInsertionPreviewStatus.InvalidMap;
	public bool RequiresRegionLookup => RequiresInstanceLookup && Status != PlayerSummonKnownObjectNpcSkillWorldInsertionPreviewStatus.InvalidInstance;
	public bool RequiresControllerBeforeAfterSpawn => WouldSpawn;
	public bool RequiresMapRegionAdd => WouldSpawn;
	public bool RequiresKnownListUpdate => WouldSpawn;
	public bool MayThrow => Status is
		PlayerSummonKnownObjectNpcSkillWorldInsertionPreviewStatus.InvalidMap
		or PlayerSummonKnownObjectNpcSkillWorldInsertionPreviewStatus.InvalidInstance
		or PlayerSummonKnownObjectNpcSkillWorldInsertionPreviewStatus.InvalidRegion
		or PlayerSummonKnownObjectNpcSkillWorldInsertionPreviewStatus.AlreadySpawned;

	public static PlayerSummonKnownObjectNpcSkillWorldInsertionPreview MissingCreation()
	{
		return new PlayerSummonKnownObjectNpcSkillWorldInsertionPreview(
			PlayerSummonKnownObjectNpcSkillWorldInsertionPreviewStatus.MissingCreation);
	}

	public static PlayerSummonKnownObjectNpcSkillWorldInsertionPreview NotReady(
		PlayerSummonKnownObjectNpcSkillNpcCreationPreview npcCreationPreview)
	{
		return new PlayerSummonKnownObjectNpcSkillWorldInsertionPreview(
			PlayerSummonKnownObjectNpcSkillWorldInsertionPreviewStatus.NotReady,
			npcCreationPreview);
	}

	public static PlayerSummonKnownObjectNpcSkillWorldInsertionPreview InvalidMap(
		PlayerSummonKnownObjectNpcSkillNpcCreationPreview npcCreationPreview,
		int worldId)
	{
		return new PlayerSummonKnownObjectNpcSkillWorldInsertionPreview(
			PlayerSummonKnownObjectNpcSkillWorldInsertionPreviewStatus.InvalidMap,
			npcCreationPreview,
			worldId);
	}

	public static PlayerSummonKnownObjectNpcSkillWorldInsertionPreview InvalidInstance(
		PlayerSummonKnownObjectNpcSkillNpcCreationPreview npcCreationPreview,
		int worldId,
		int instanceId)
	{
		return new PlayerSummonKnownObjectNpcSkillWorldInsertionPreview(
			PlayerSummonKnownObjectNpcSkillWorldInsertionPreviewStatus.InvalidInstance,
			npcCreationPreview,
			worldId,
			instanceId);
	}

	public static PlayerSummonKnownObjectNpcSkillWorldInsertionPreview InvalidRegion(
		PlayerSummonKnownObjectNpcSkillNpcCreationPreview npcCreationPreview,
		int worldId,
		int instanceId,
		float x,
		float y,
		float z,
		byte heading)
	{
		return new PlayerSummonKnownObjectNpcSkillWorldInsertionPreview(
			PlayerSummonKnownObjectNpcSkillWorldInsertionPreviewStatus.InvalidRegion,
			npcCreationPreview,
			worldId,
			instanceId,
			x,
			y,
			z,
			heading);
	}

	public static PlayerSummonKnownObjectNpcSkillWorldInsertionPreview AlreadySpawned(
		PlayerSummonKnownObjectNpcSkillNpcCreationPreview npcCreationPreview)
	{
		return new PlayerSummonKnownObjectNpcSkillWorldInsertionPreview(
			PlayerSummonKnownObjectNpcSkillWorldInsertionPreviewStatus.AlreadySpawned,
			npcCreationPreview);
	}

	public static PlayerSummonKnownObjectNpcSkillWorldInsertionPreview WouldInsert(
		PlayerSummonKnownObjectNpcSkillNpcCreationPreview npcCreationPreview,
		int worldId,
		int instanceId,
		float x,
		float y,
		float z,
		byte heading)
	{
		return new PlayerSummonKnownObjectNpcSkillWorldInsertionPreview(
			PlayerSummonKnownObjectNpcSkillWorldInsertionPreviewStatus.WouldInsert,
			npcCreationPreview,
			worldId,
			instanceId,
			x,
			y,
			z,
			heading);
	}
}

public enum PlayerSummonKnownObjectNpcSkillWorldInsertionPreviewStatus
{
	MissingCreation,
	NotReady,
	InvalidMap,
	InvalidInstance,
	InvalidRegion,
	AlreadySpawned,
	WouldInsert,
}

public sealed record PlayerSummonKnownObjectNpcSkillPostSpawnCallbackPreview(
	PlayerSummonKnownObjectNpcSkillPostSpawnCallbackPreviewStatus Status,
	PlayerSummonKnownObjectNpcSkillWorldInsertionPreview? WorldInsertionPreview = null,
	bool SpawnedObjectHasSpawn = false,
	bool SpawnIsTemporary = false,
	bool SpawnedObjectIsSpawned = false,
	bool ShouldRegisterTemporarySpawn = false,
	bool ShouldInvokeInstanceOnSpawn = false)
{
	public bool HasSpawnedObject => Status == PlayerSummonKnownObjectNpcSkillPostSpawnCallbackPreviewStatus.Callbacks;
	public bool RequiresTemporarySpawnCheck => HasSpawnedObject;
	public bool RequiresInstanceOnSpawnCheck => HasSpawnedObject;
	public bool RequiresWorldMapInstance => ShouldInvokeInstanceOnSpawn;
	public bool RequiresInstanceHandler => ShouldInvokeInstanceOnSpawn;

	public static PlayerSummonKnownObjectNpcSkillPostSpawnCallbackPreview MissingWorldInsertion()
	{
		return new PlayerSummonKnownObjectNpcSkillPostSpawnCallbackPreview(
			PlayerSummonKnownObjectNpcSkillPostSpawnCallbackPreviewStatus.MissingWorldInsertion);
	}

	public static PlayerSummonKnownObjectNpcSkillPostSpawnCallbackPreview NotReady(
		PlayerSummonKnownObjectNpcSkillWorldInsertionPreview worldInsertionPreview)
	{
		return new PlayerSummonKnownObjectNpcSkillPostSpawnCallbackPreview(
			PlayerSummonKnownObjectNpcSkillPostSpawnCallbackPreviewStatus.NotReady,
			worldInsertionPreview);
	}

	public static PlayerSummonKnownObjectNpcSkillPostSpawnCallbackPreview NoSpawnedObject(
		PlayerSummonKnownObjectNpcSkillWorldInsertionPreview worldInsertionPreview)
	{
		return new PlayerSummonKnownObjectNpcSkillPostSpawnCallbackPreview(
			PlayerSummonKnownObjectNpcSkillPostSpawnCallbackPreviewStatus.NoSpawnedObject,
			worldInsertionPreview);
	}

	public static PlayerSummonKnownObjectNpcSkillPostSpawnCallbackPreview Callbacks(
		PlayerSummonKnownObjectNpcSkillWorldInsertionPreview worldInsertionPreview,
		bool shouldRegisterTemporarySpawn,
		bool shouldInvokeInstanceOnSpawn,
		bool spawnedObjectHasSpawn,
		bool spawnIsTemporary,
		bool spawnedObjectIsSpawned)
	{
		return new PlayerSummonKnownObjectNpcSkillPostSpawnCallbackPreview(
			PlayerSummonKnownObjectNpcSkillPostSpawnCallbackPreviewStatus.Callbacks,
			worldInsertionPreview,
			spawnedObjectHasSpawn,
			spawnIsTemporary,
			spawnedObjectIsSpawned,
			shouldRegisterTemporarySpawn,
			shouldInvokeInstanceOnSpawn);
	}
}

public enum PlayerSummonKnownObjectNpcSkillPostSpawnCallbackPreviewStatus
{
	MissingWorldInsertion,
	NotReady,
	NoSpawnedObject,
	Callbacks,
}

public sealed record PlayerSummonKnownObjectNpcSkillCandidateMetadata(
	int Position,
	PlayerSummonKnownObjectNpcSkillTemplateMetadata Template,
	long LastTimeUsedMilliseconds = 0,
	bool ChanceReady = true,
	PlayerSummonKnownObjectNpcSkillConditionTarget? ConditionTarget = null,
	PlayerSummonKnownObjectTargetRangeReadiness? TargetRangeReadiness = null,
	IReadOnlyList<string>? SkillTemplateSignetBurstStacks = null,
	IReadOnlyList<PlayerSummonKnownObjectNpcSkillHelpFriendCandidate>? HelpFriendCandidates = null);

public sealed record PlayerSummonKnownObjectNpcSkillCandidateMetadataProjection(
	int NpcId,
	int OriginalSkillCount,
	IReadOnlyList<PlayerSummonKnownObjectNpcSkillCandidateMetadata> Candidates,
	IReadOnlyList<int> MissingSkillIds)
{
	public bool IsEmpty => Candidates.Count == 0;
	public bool RequiresSkillTemplateLookup => OriginalSkillCount > 0;
	public bool WouldPruneMissingSkills => MissingSkillIds.Count > 0;
	public bool JavaWouldWarnMissingSkills => WouldPruneMissingSkills;
	public bool JavaWouldMutateSourceTemplateList => WouldPruneMissingSkills;

	public static PlayerSummonKnownObjectNpcSkillCandidateMetadataProjection Empty(int npcId)
	{
		return new PlayerSummonKnownObjectNpcSkillCandidateMetadataProjection(
			npcId,
			0,
			Array.Empty<PlayerSummonKnownObjectNpcSkillCandidateMetadata>(),
			Array.Empty<int>());
	}
}

public sealed record PlayerSummonKnownObjectNpcSkillCandidateListProjection(
	IReadOnlyList<PlayerSummonKnownObjectNpcSkillCandidate> Candidates,
	IReadOnlyList<int> Priorities,
	IReadOnlyList<PlayerSummonKnownObjectNpcSkillCandidate> PostSpawnCandidates)
{
	public bool IsEmpty => Candidates.Count == 0;
}

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

public sealed record PlayerSummonKnownObjectNpcSkillPerformAttackPreview(
	PlayerSummonKnownObjectNpcSkillPerformAttackPreviewStatus Status,
	int DelayMilliseconds,
	PlayerSummonKnownObjectNpcSkillActionWorkflowPreview? ActionWorkflowPreview = null,
	PlayerSummonKnownObjectNpcSkillAiEvent AiEvent = PlayerSummonKnownObjectNpcSkillAiEvent.None)
{
	public bool ShouldAbortCast => Status == PlayerSummonKnownObjectNpcSkillPerformAttackPreviewStatus.TargetTooFar;

	public bool ShouldDispatchAiEvent => AiEvent != PlayerSummonKnownObjectNpcSkillAiEvent.None;

	public bool ShouldEnterCastSubState =>
		Status is PlayerSummonKnownObjectNpcSkillPerformAttackPreviewStatus.ImmediateSkillAction
			or PlayerSummonKnownObjectNpcSkillPerformAttackPreviewStatus.ScheduledSkillAction;

	public bool ShouldInvokeSkillActionNow => Status == PlayerSummonKnownObjectNpcSkillPerformAttackPreviewStatus.ImmediateSkillAction;

	public bool ShouldScheduleSkillAction => Status == PlayerSummonKnownObjectNpcSkillPerformAttackPreviewStatus.ScheduledSkillAction;

	public static PlayerSummonKnownObjectNpcSkillPerformAttackPreview TargetTooFar(
		int delayMilliseconds,
		PlayerSummonKnownObjectNpcSkillActionWorkflowPreview? actionWorkflowPreview)
	{
		return new PlayerSummonKnownObjectNpcSkillPerformAttackPreview(
			PlayerSummonKnownObjectNpcSkillPerformAttackPreviewStatus.TargetTooFar,
			delayMilliseconds,
			actionWorkflowPreview,
			PlayerSummonKnownObjectNpcSkillAiEvent.TargetTooFar);
	}

	public static PlayerSummonKnownObjectNpcSkillPerformAttackPreview CastSubStateUnchanged(
		int delayMilliseconds,
		PlayerSummonKnownObjectNpcSkillActionWorkflowPreview? actionWorkflowPreview)
	{
		return new PlayerSummonKnownObjectNpcSkillPerformAttackPreview(
			PlayerSummonKnownObjectNpcSkillPerformAttackPreviewStatus.CastSubStateUnchanged,
			delayMilliseconds,
			actionWorkflowPreview);
	}

	public static PlayerSummonKnownObjectNpcSkillPerformAttackPreview Scheduled(
		int delayMilliseconds,
		PlayerSummonKnownObjectNpcSkillActionWorkflowPreview? actionWorkflowPreview)
	{
		return new PlayerSummonKnownObjectNpcSkillPerformAttackPreview(
			PlayerSummonKnownObjectNpcSkillPerformAttackPreviewStatus.ScheduledSkillAction,
			delayMilliseconds,
			actionWorkflowPreview);
	}

	public static PlayerSummonKnownObjectNpcSkillPerformAttackPreview Immediate(
		PlayerSummonKnownObjectNpcSkillActionWorkflowPreview? actionWorkflowPreview)
	{
		return new PlayerSummonKnownObjectNpcSkillPerformAttackPreview(
			PlayerSummonKnownObjectNpcSkillPerformAttackPreviewStatus.ImmediateSkillAction,
			0,
			actionWorkflowPreview);
	}
}

public enum PlayerSummonKnownObjectNpcSkillPerformAttackPreviewStatus
{
	TargetTooFar,
	CastSubStateUnchanged,
	ScheduledSkillAction,
	ImmediateSkillAction,
}

public sealed record PlayerSummonKnownObjectNpcSkillPerformAttackExecutionPreview(
	PlayerSummonKnownObjectNpcSkillPerformAttackExecutionPreviewStatus Status,
	long CurrentTimeMilliseconds,
	PlayerSummonKnownObjectNpcSkillPerformAttackPreview? PerformAttackPreview = null,
	long? ScheduledDueTimeMilliseconds = null)
{
	public bool ShouldInvokeSkillActionWorkflow =>
		Status is PlayerSummonKnownObjectNpcSkillPerformAttackExecutionPreviewStatus.ImmediateWorkflow
			or PlayerSummonKnownObjectNpcSkillPerformAttackExecutionPreviewStatus.ScheduledWorkflowDue;

	public bool IsScheduledPending => Status == PlayerSummonKnownObjectNpcSkillPerformAttackExecutionPreviewStatus.ScheduledWorkflowPending;

	public PlayerSummonKnownObjectNpcSkillActionWorkflowPreview? ActionWorkflowPreview => PerformAttackPreview?.ActionWorkflowPreview;

	public static PlayerSummonKnownObjectNpcSkillPerformAttackExecutionPreview MissingPreview(long currentTimeMilliseconds)
	{
		return new PlayerSummonKnownObjectNpcSkillPerformAttackExecutionPreview(
			PlayerSummonKnownObjectNpcSkillPerformAttackExecutionPreviewStatus.MissingPreview,
			currentTimeMilliseconds);
	}

	public static PlayerSummonKnownObjectNpcSkillPerformAttackExecutionPreview NoAction(
		PlayerSummonKnownObjectNpcSkillPerformAttackPreview performAttackPreview,
		long currentTimeMilliseconds)
	{
		return new PlayerSummonKnownObjectNpcSkillPerformAttackExecutionPreview(
			PlayerSummonKnownObjectNpcSkillPerformAttackExecutionPreviewStatus.NoAction,
			currentTimeMilliseconds,
			performAttackPreview);
	}

	public static PlayerSummonKnownObjectNpcSkillPerformAttackExecutionPreview MissingWorkflow(
		PlayerSummonKnownObjectNpcSkillPerformAttackPreview performAttackPreview,
		long currentTimeMilliseconds)
	{
		return new PlayerSummonKnownObjectNpcSkillPerformAttackExecutionPreview(
			PlayerSummonKnownObjectNpcSkillPerformAttackExecutionPreviewStatus.MissingWorkflow,
			currentTimeMilliseconds,
			performAttackPreview);
	}

	public static PlayerSummonKnownObjectNpcSkillPerformAttackExecutionPreview Immediate(
		PlayerSummonKnownObjectNpcSkillPerformAttackPreview performAttackPreview,
		long currentTimeMilliseconds)
	{
		return new PlayerSummonKnownObjectNpcSkillPerformAttackExecutionPreview(
			PlayerSummonKnownObjectNpcSkillPerformAttackExecutionPreviewStatus.ImmediateWorkflow,
			currentTimeMilliseconds,
			performAttackPreview,
			currentTimeMilliseconds);
	}

	public static PlayerSummonKnownObjectNpcSkillPerformAttackExecutionPreview ScheduledPending(
		PlayerSummonKnownObjectNpcSkillPerformAttackPreview performAttackPreview,
		long currentTimeMilliseconds,
		long scheduledDueTimeMilliseconds)
	{
		return new PlayerSummonKnownObjectNpcSkillPerformAttackExecutionPreview(
			PlayerSummonKnownObjectNpcSkillPerformAttackExecutionPreviewStatus.ScheduledWorkflowPending,
			currentTimeMilliseconds,
			performAttackPreview,
			scheduledDueTimeMilliseconds);
	}

	public static PlayerSummonKnownObjectNpcSkillPerformAttackExecutionPreview ScheduledDue(
		PlayerSummonKnownObjectNpcSkillPerformAttackPreview performAttackPreview,
		long currentTimeMilliseconds,
		long scheduledDueTimeMilliseconds)
	{
		return new PlayerSummonKnownObjectNpcSkillPerformAttackExecutionPreview(
			PlayerSummonKnownObjectNpcSkillPerformAttackExecutionPreviewStatus.ScheduledWorkflowDue,
			currentTimeMilliseconds,
			performAttackPreview,
			scheduledDueTimeMilliseconds);
	}
}

public enum PlayerSummonKnownObjectNpcSkillPerformAttackExecutionPreviewStatus
{
	MissingPreview,
	NoAction,
	MissingWorkflow,
	ImmediateWorkflow,
	ScheduledWorkflowPending,
	ScheduledWorkflowDue,
}

public sealed record PlayerSummonKnownObjectNpcSkillSchedulerCallbackOutcome(
	PlayerSummonKnownObjectNpcSkillSchedulerCallbackOutcomeStatus Status,
	PlayerSummonKnownObjectNpcSkillPerformAttackExecutionPreview? ExecutionPreview = null,
	PlayerSummonKnownObjectNpcSkillActionWorkflowPreview? ActionWorkflowPreview = null,
	PlayerSummonKnownObjectNpcSkillActionResult? ActionResult = null)
{
	public bool ShouldInvokeSkillActionWorkflow => Status == PlayerSummonKnownObjectNpcSkillSchedulerCallbackOutcomeStatus.WorkflowInvoked;

	public bool IsPending => Status == PlayerSummonKnownObjectNpcSkillSchedulerCallbackOutcomeStatus.Pending;

	public static PlayerSummonKnownObjectNpcSkillSchedulerCallbackOutcome MissingExecutionPreview()
	{
		return new PlayerSummonKnownObjectNpcSkillSchedulerCallbackOutcome(
			PlayerSummonKnownObjectNpcSkillSchedulerCallbackOutcomeStatus.MissingExecutionPreview);
	}

	public static PlayerSummonKnownObjectNpcSkillSchedulerCallbackOutcome NoAction(
		PlayerSummonKnownObjectNpcSkillPerformAttackExecutionPreview executionPreview)
	{
		return new PlayerSummonKnownObjectNpcSkillSchedulerCallbackOutcome(
			PlayerSummonKnownObjectNpcSkillSchedulerCallbackOutcomeStatus.NoAction,
			executionPreview);
	}

	public static PlayerSummonKnownObjectNpcSkillSchedulerCallbackOutcome Pending(
		PlayerSummonKnownObjectNpcSkillPerformAttackExecutionPreview executionPreview)
	{
		return new PlayerSummonKnownObjectNpcSkillSchedulerCallbackOutcome(
			PlayerSummonKnownObjectNpcSkillSchedulerCallbackOutcomeStatus.Pending,
			executionPreview);
	}

	public static PlayerSummonKnownObjectNpcSkillSchedulerCallbackOutcome MissingWorkflow(
		PlayerSummonKnownObjectNpcSkillPerformAttackExecutionPreview executionPreview)
	{
		return new PlayerSummonKnownObjectNpcSkillSchedulerCallbackOutcome(
			PlayerSummonKnownObjectNpcSkillSchedulerCallbackOutcomeStatus.MissingWorkflow,
			executionPreview);
	}

	public static PlayerSummonKnownObjectNpcSkillSchedulerCallbackOutcome WorkflowInvoked(
		PlayerSummonKnownObjectNpcSkillPerformAttackExecutionPreview executionPreview,
		PlayerSummonKnownObjectNpcSkillActionWorkflowPreview actionWorkflowPreview,
		PlayerSummonKnownObjectNpcSkillActionResult? actionResult)
	{
		return new PlayerSummonKnownObjectNpcSkillSchedulerCallbackOutcome(
			PlayerSummonKnownObjectNpcSkillSchedulerCallbackOutcomeStatus.WorkflowInvoked,
			executionPreview,
			actionWorkflowPreview,
			actionResult);
	}
}

public enum PlayerSummonKnownObjectNpcSkillSchedulerCallbackOutcomeStatus
{
	MissingExecutionPreview,
	NoAction,
	Pending,
	MissingWorkflow,
	WorkflowInvoked,
}

public sealed record PlayerSummonKnownObjectNpcSkillAttackCycleSnapshot(
	PlayerSummonKnownObjectNpcSkillAttackCycleSnapshotStatus Status,
	int MercenaryObjectId,
	PlayerSummonKnownObject? KnownObject = null,
	PlayerSummonKnownObjectNpcSkillCandidateListProjection? SkillListProjection = null,
	PlayerSummonKnownObjectNpcSkillSelectionPreview? SelectionPreview = null,
	PlayerSummonKnownObjectNpcSkillActionPreview? ActionPreview = null,
	PlayerSummonKnownObjectNpcSkillPostSpawnPreview? PostSpawnPreview = null,
	PlayerSummonKnownObjectNpcSkillActionWorkflowPreview? ActionWorkflowPreview = null,
	PlayerSummonKnownObjectNpcSkillPerformAttackPreview? PerformAttackPreview = null,
	PlayerSummonKnownObjectNpcSkillPerformAttackExecutionPreview? PerformAttackExecutionPreview = null,
	PlayerSummonKnownObjectNpcSkillSchedulerCallbackOutcome? SchedulerCallbackOutcome = null)
{
	public bool IsCaptured => Status == PlayerSummonKnownObjectNpcSkillAttackCycleSnapshotStatus.Captured;

	public bool HasSkillListProjection => SkillListProjection != null;

	public bool HasSelectionPreview => SelectionPreview != null;

	public bool HasActionPreview => ActionPreview != null;

	public bool HasPostSpawnPreview => PostSpawnPreview != null;

	public bool HasActionWorkflowPreview => ActionWorkflowPreview != null;

	public bool HasPerformAttackPreview => PerformAttackPreview != null;

	public bool HasPerformAttackExecutionPreview => PerformAttackExecutionPreview != null;

	public bool HasSchedulerCallbackOutcome => SchedulerCallbackOutcome != null;

	public bool WouldInvokeSkillActionWorkflow => SchedulerCallbackOutcome?.ShouldInvokeSkillActionWorkflow == true;

	public bool HasCompleteRepresentedCycle =>
		HasSkillListProjection
		&& HasSelectionPreview
		&& HasActionWorkflowPreview
		&& HasPerformAttackPreview
		&& HasPerformAttackExecutionPreview
		&& HasSchedulerCallbackOutcome;

	public static PlayerSummonKnownObjectNpcSkillAttackCycleSnapshot MissingKnownObject(int mercenaryObjectId)
	{
		return new PlayerSummonKnownObjectNpcSkillAttackCycleSnapshot(
			PlayerSummonKnownObjectNpcSkillAttackCycleSnapshotStatus.MissingKnownObject,
			mercenaryObjectId);
	}

	public static PlayerSummonKnownObjectNpcSkillAttackCycleSnapshot Captured(
		int mercenaryObjectId,
		PlayerSummonKnownObject knownObject,
		PlayerSummonKnownObjectNpcSkillCandidateListProjection? skillListProjection,
		PlayerSummonKnownObjectNpcSkillSelectionPreview? selectionPreview,
		PlayerSummonKnownObjectNpcSkillActionPreview? actionPreview,
		PlayerSummonKnownObjectNpcSkillPostSpawnPreview? postSpawnPreview,
		PlayerSummonKnownObjectNpcSkillActionWorkflowPreview? actionWorkflowPreview,
		PlayerSummonKnownObjectNpcSkillPerformAttackPreview? performAttackPreview,
		PlayerSummonKnownObjectNpcSkillPerformAttackExecutionPreview? performAttackExecutionPreview,
		PlayerSummonKnownObjectNpcSkillSchedulerCallbackOutcome? schedulerCallbackOutcome)
	{
		return new PlayerSummonKnownObjectNpcSkillAttackCycleSnapshot(
			PlayerSummonKnownObjectNpcSkillAttackCycleSnapshotStatus.Captured,
			mercenaryObjectId,
			knownObject,
			skillListProjection,
			selectionPreview,
			actionPreview,
			postSpawnPreview,
			actionWorkflowPreview,
			performAttackPreview,
			performAttackExecutionPreview,
			schedulerCallbackOutcome);
	}
}

public enum PlayerSummonKnownObjectNpcSkillAttackCycleSnapshotStatus
{
	MissingKnownObject,
	Captured,
}

public sealed record PlayerSummonKnownObjectNpcSkillAttackCycleReadiness(
	PlayerSummonKnownObjectNpcSkillAttackCycleReadinessStatus Status,
	PlayerSummonKnownObjectNpcSkillAttackCycleSnapshot? CycleSnapshot = null,
	IReadOnlyList<PlayerSummonKnownObjectNpcSkillAttackCycleReadinessPiece>? MissingPieces = null)
{
	private static readonly IReadOnlyList<PlayerSummonKnownObjectNpcSkillAttackCycleReadinessPiece> EmptyMissingPieces = [];

	public IReadOnlyList<PlayerSummonKnownObjectNpcSkillAttackCycleReadinessPiece> MissingRequiredPieces =>
		MissingPieces ?? EmptyMissingPieces;

	public bool IsReadyForLiveAiAdapter => Status == PlayerSummonKnownObjectNpcSkillAttackCycleReadinessStatus.Ready;

	public bool HasMissingRequiredMetadata => Status == PlayerSummonKnownObjectNpcSkillAttackCycleReadinessStatus.MissingRequiredMetadata;

	public bool MissingSkillListProjection => MissingRequiredPieces.Contains(PlayerSummonKnownObjectNpcSkillAttackCycleReadinessPiece.SkillListProjection);

	public bool MissingSelectionPreview => MissingRequiredPieces.Contains(PlayerSummonKnownObjectNpcSkillAttackCycleReadinessPiece.SelectionPreview);

	public bool MissingActionPreview => MissingRequiredPieces.Contains(PlayerSummonKnownObjectNpcSkillAttackCycleReadinessPiece.ActionPreview);

	public bool MissingPostSpawnPreview => MissingRequiredPieces.Contains(PlayerSummonKnownObjectNpcSkillAttackCycleReadinessPiece.PostSpawnPreview);

	public bool MissingActionWorkflowPreview => MissingRequiredPieces.Contains(PlayerSummonKnownObjectNpcSkillAttackCycleReadinessPiece.ActionWorkflowPreview);

	public bool MissingPerformAttackPreview => MissingRequiredPieces.Contains(PlayerSummonKnownObjectNpcSkillAttackCycleReadinessPiece.PerformAttackPreview);

	public bool MissingPerformAttackExecutionPreview => MissingRequiredPieces.Contains(PlayerSummonKnownObjectNpcSkillAttackCycleReadinessPiece.PerformAttackExecutionPreview);

	public bool MissingSchedulerCallbackOutcome => MissingRequiredPieces.Contains(PlayerSummonKnownObjectNpcSkillAttackCycleReadinessPiece.SchedulerCallbackOutcome);

	public static PlayerSummonKnownObjectNpcSkillAttackCycleReadiness MissingCycle()
	{
		return new PlayerSummonKnownObjectNpcSkillAttackCycleReadiness(
			PlayerSummonKnownObjectNpcSkillAttackCycleReadinessStatus.MissingCycle);
	}

	public static PlayerSummonKnownObjectNpcSkillAttackCycleReadiness MissingKnownObject(
		PlayerSummonKnownObjectNpcSkillAttackCycleSnapshot cycleSnapshot)
	{
		return new PlayerSummonKnownObjectNpcSkillAttackCycleReadiness(
			PlayerSummonKnownObjectNpcSkillAttackCycleReadinessStatus.MissingKnownObject,
			cycleSnapshot);
	}

	public static PlayerSummonKnownObjectNpcSkillAttackCycleReadiness MissingRequiredMetadata(
		PlayerSummonKnownObjectNpcSkillAttackCycleSnapshot cycleSnapshot,
		IReadOnlyList<PlayerSummonKnownObjectNpcSkillAttackCycleReadinessPiece> missingPieces)
	{
		return new PlayerSummonKnownObjectNpcSkillAttackCycleReadiness(
			PlayerSummonKnownObjectNpcSkillAttackCycleReadinessStatus.MissingRequiredMetadata,
			cycleSnapshot,
			missingPieces);
	}

	public static PlayerSummonKnownObjectNpcSkillAttackCycleReadiness Ready(
		PlayerSummonKnownObjectNpcSkillAttackCycleSnapshot cycleSnapshot)
	{
		return new PlayerSummonKnownObjectNpcSkillAttackCycleReadiness(
			PlayerSummonKnownObjectNpcSkillAttackCycleReadinessStatus.Ready,
			cycleSnapshot,
			EmptyMissingPieces);
	}
}

public enum PlayerSummonKnownObjectNpcSkillAttackCycleReadinessStatus
{
	MissingCycle,
	MissingKnownObject,
	MissingRequiredMetadata,
	Ready,
}

public enum PlayerSummonKnownObjectNpcSkillAttackCycleReadinessPiece
{
	SkillListProjection,
	SelectionPreview,
	ActionPreview,
	PostSpawnPreview,
	ActionWorkflowPreview,
	PerformAttackPreview,
	PerformAttackExecutionPreview,
	SchedulerCallbackOutcome,
}

public sealed record PlayerSummonKnownObjectNpcSkillAttackCycleLiveInvocation(
	PlayerSummonKnownObjectNpcSkillAttackCycleLiveInvocationStatus Status,
	PlayerSummonKnownObjectNpcSkillAttackCycleReadiness Readiness,
	PlayerSummonKnownObjectNpcSkillAttackCycleSnapshot? CycleSnapshot = null,
	IReadOnlyList<string>? UnsupportedJavaBehaviors = null)
{
	private static readonly IReadOnlyList<string> EmptyUnsupportedBehaviors = [];

	private static readonly IReadOnlyList<string> LiveAiUnsupportedBehaviors =
	[
		"NpcAI state/substate mutation",
		"ThreadPoolManager scheduling and cancellation",
		"CreatureController skill execution",
		"target mutation",
		"effect application",
		"packet fanout",
		"persistence",
		"threading/serialization/date-time runtime parity",
	];

	public IReadOnlyList<string> UnsupportedBehaviors => UnsupportedJavaBehaviors ?? EmptyUnsupportedBehaviors;

	public bool WouldInvokeLiveAi => false;

	public bool IsBlockedByMissingMetadata => Status == PlayerSummonKnownObjectNpcSkillAttackCycleLiveInvocationStatus.MissingRequiredMetadata;

	public bool IsLiveAiUnsupported => Status == PlayerSummonKnownObjectNpcSkillAttackCycleLiveInvocationStatus.LiveAiNotWired;

	public static PlayerSummonKnownObjectNpcSkillAttackCycleLiveInvocation FromReadiness(
		PlayerSummonKnownObjectNpcSkillAttackCycleReadiness readiness)
	{
		return readiness.Status switch
		{
			PlayerSummonKnownObjectNpcSkillAttackCycleReadinessStatus.MissingCycle =>
				new PlayerSummonKnownObjectNpcSkillAttackCycleLiveInvocation(
					PlayerSummonKnownObjectNpcSkillAttackCycleLiveInvocationStatus.MissingCycle,
					readiness),
			PlayerSummonKnownObjectNpcSkillAttackCycleReadinessStatus.MissingKnownObject =>
				new PlayerSummonKnownObjectNpcSkillAttackCycleLiveInvocation(
					PlayerSummonKnownObjectNpcSkillAttackCycleLiveInvocationStatus.MissingKnownObject,
					readiness,
					readiness.CycleSnapshot),
			PlayerSummonKnownObjectNpcSkillAttackCycleReadinessStatus.MissingRequiredMetadata =>
				new PlayerSummonKnownObjectNpcSkillAttackCycleLiveInvocation(
					PlayerSummonKnownObjectNpcSkillAttackCycleLiveInvocationStatus.MissingRequiredMetadata,
					readiness,
					readiness.CycleSnapshot),
			_ => new PlayerSummonKnownObjectNpcSkillAttackCycleLiveInvocation(
				PlayerSummonKnownObjectNpcSkillAttackCycleLiveInvocationStatus.LiveAiNotWired,
				readiness,
				readiness.CycleSnapshot,
				LiveAiUnsupportedBehaviors),
		};
	}
}

public enum PlayerSummonKnownObjectNpcSkillAttackCycleLiveInvocationStatus
{
	MissingCycle,
	MissingKnownObject,
	MissingRequiredMetadata,
	LiveAiNotWired,
}

public sealed record PlayerSummonKnownObjectNpcSkillAttackCycleResultContract(
	PlayerSummonKnownObjectNpcSkillAttackCycleResultContractStatus Status,
	PlayerSummonKnownObjectNpcSkillAttackCycleLiveInvocation? LiveInvocation = null,
	IReadOnlyList<PlayerSummonKnownObjectNpcSkillAttackCycleExpectedSideEffect>? ExpectedSideEffects = null,
	IReadOnlyList<PlayerSummonKnownObjectNpcSkillAttackCycleOutcomeBranch>? OutcomeBranches = null,
	IReadOnlyList<PlayerSummonKnownObjectNpcSkillAttackCycleLiveOperation>? LiveOperations = null,
	PlayerSummonKnownObjectNpcSkillActionSideEffectTrace? ActionSideEffectTrace = null,
	PlayerSummonKnownObjectNpcSkillUseSkillStartTrace? UseSkillStartTrace = null,
	PlayerSummonKnownObjectNpcSkillValidationMutationTrace? ValidationMutationTrace = null,
	PlayerSummonKnownObjectNpcSkillTemplateActionTrace? TemplateActionTrace = null,
	PlayerSummonKnownObjectNpcSkillEffectInitializationTrace? EffectInitializationTrace = null,
	PlayerSummonKnownObjectNpcSkillCastResultPacketTrace? CastResultPacketTrace = null,
	PlayerSummonKnownObjectNpcSkillEndCastBranchTrace? EndCastBranchTrace = null,
	PlayerSummonKnownObjectNpcSkillEndCastSideEffectTrace? EndCastSideEffectTrace = null)
{
	private static readonly IReadOnlyList<PlayerSummonKnownObjectNpcSkillAttackCycleExpectedSideEffect> EmptySideEffects = [];
	private static readonly IReadOnlyList<PlayerSummonKnownObjectNpcSkillAttackCycleOutcomeBranch> EmptyOutcomeBranches = [];
	private static readonly IReadOnlyList<PlayerSummonKnownObjectNpcSkillAttackCycleLiveOperation> EmptyLiveOperations = [];

	public IReadOnlyList<PlayerSummonKnownObjectNpcSkillAttackCycleExpectedSideEffect> ExpectedJavaSideEffects =>
		ExpectedSideEffects ?? EmptySideEffects;

	public IReadOnlyList<PlayerSummonKnownObjectNpcSkillAttackCycleOutcomeBranch> ExpectedJavaOutcomeBranches =>
		OutcomeBranches ?? EmptyOutcomeBranches;

	public IReadOnlyList<PlayerSummonKnownObjectNpcSkillAttackCycleLiveOperation> FutureLiveAdapterOperations =>
		LiveOperations ?? EmptyLiveOperations;

	public bool IsLiveAiUnsupported => Status == PlayerSummonKnownObjectNpcSkillAttackCycleResultContractStatus.LiveAiNotWired;

	public bool IsBlockedByInvocationReadiness => Status == PlayerSummonKnownObjectNpcSkillAttackCycleResultContractStatus.BlockedByInvocationReadiness;

	public bool WouldExecuteSideEffects => false;

	public bool HasActionSideEffectTrace => ActionSideEffectTrace != null;

	public bool HasUseSkillStartTrace => UseSkillStartTrace != null;

	public bool HasValidationMutationTrace => ValidationMutationTrace != null;

	public bool HasTemplateActionTrace => TemplateActionTrace != null;

	public bool HasEffectInitializationTrace => EffectInitializationTrace != null;

	public bool HasCastResultPacketTrace => CastResultPacketTrace != null;

	public bool HasEndCastBranchTrace => EndCastBranchTrace != null;

	public bool HasEndCastSideEffectTrace => EndCastSideEffectTrace != null;

	public bool ExpectsAiSubStateMutation =>
		ExpectedJavaSideEffects.Contains(PlayerSummonKnownObjectNpcSkillAttackCycleExpectedSideEffect.AiCastSubState)
		|| ExpectedJavaSideEffects.Contains(PlayerSummonKnownObjectNpcSkillAttackCycleExpectedSideEffect.AiNoneSubState);

	public bool ExpectsControllerUseSkill =>
		ExpectedJavaSideEffects.Contains(PlayerSummonKnownObjectNpcSkillAttackCycleExpectedSideEffect.ControllerUseSkill);

	public bool ExpectsPostSpawnAction =>
		ExpectedJavaSideEffects.Contains(PlayerSummonKnownObjectNpcSkillAttackCycleExpectedSideEffect.PostSpawnImmediate)
		|| ExpectedJavaSideEffects.Contains(PlayerSummonKnownObjectNpcSkillAttackCycleExpectedSideEffect.PostSpawnScheduled);

	public bool ExpectsPackets =>
		ExpectedJavaSideEffects.Contains(PlayerSummonKnownObjectNpcSkillAttackCycleExpectedSideEffect.PacketFanout);

	public bool ExpectsSchedulerWork =>
		ExpectedJavaSideEffects.Contains(PlayerSummonKnownObjectNpcSkillAttackCycleExpectedSideEffect.SchedulerSkillActionCallback);

	public bool HasOutcomeBranch(PlayerSummonKnownObjectNpcSkillAttackCycleOutcomeBranch branch)
	{
		return ExpectedJavaOutcomeBranches.Contains(branch);
	}

	public bool HasLiveOperation(PlayerSummonKnownObjectNpcSkillAttackCycleLiveOperation operation)
	{
		return FutureLiveAdapterOperations.Contains(operation);
	}

	public static PlayerSummonKnownObjectNpcSkillAttackCycleResultContract FromLiveInvocation(
		PlayerSummonKnownObjectNpcSkillAttackCycleLiveInvocation? liveInvocation)
	{
		if (liveInvocation == null)
		{
			return new PlayerSummonKnownObjectNpcSkillAttackCycleResultContract(
				PlayerSummonKnownObjectNpcSkillAttackCycleResultContractStatus.MissingInvocation,
				OutcomeBranches: [PlayerSummonKnownObjectNpcSkillAttackCycleOutcomeBranch.MissingInvocation]);
		}

		if (liveInvocation.Status != PlayerSummonKnownObjectNpcSkillAttackCycleLiveInvocationStatus.LiveAiNotWired)
		{
			return new PlayerSummonKnownObjectNpcSkillAttackCycleResultContract(
				PlayerSummonKnownObjectNpcSkillAttackCycleResultContractStatus.BlockedByInvocationReadiness,
				liveInvocation,
				OutcomeBranches: [PlayerSummonKnownObjectNpcSkillAttackCycleOutcomeBranch.BlockedByInvocationReadiness]);
		}

		var sideEffects = BuildExpectedSideEffects(liveInvocation.CycleSnapshot);
		var outcomeBranches = BuildExpectedOutcomeBranches(liveInvocation.CycleSnapshot);
		var actionTrace = BuildActionSideEffectTrace(liveInvocation.CycleSnapshot);
		var useSkillStartTrace = BuildUseSkillStartTrace(liveInvocation.CycleSnapshot, actionTrace);
		var validationMutationTrace = BuildValidationMutationTrace(actionTrace);
		var templateActionTrace = BuildTemplateActionTrace(actionTrace);
		var effectInitializationTrace = BuildEffectInitializationTrace(
			liveInvocation.CycleSnapshot,
			actionTrace);
		var castResultPacketTrace = BuildCastResultPacketTrace(actionTrace);
		var endCastBranchTrace = BuildEndCastBranchTrace(liveInvocation.CycleSnapshot, actionTrace);
		return new PlayerSummonKnownObjectNpcSkillAttackCycleResultContract(
			PlayerSummonKnownObjectNpcSkillAttackCycleResultContractStatus.LiveAiNotWired,
			liveInvocation,
			sideEffects,
			outcomeBranches,
			BuildFutureLiveOperations(outcomeBranches, sideEffects),
			actionTrace,
			useSkillStartTrace,
			validationMutationTrace,
			templateActionTrace,
			effectInitializationTrace,
			castResultPacketTrace,
			endCastBranchTrace,
			BuildEndCastSideEffectTrace(liveInvocation.CycleSnapshot, actionTrace));
	}

	private static PlayerSummonKnownObjectNpcSkillActionSideEffectTrace? BuildActionSideEffectTrace(
		PlayerSummonKnownObjectNpcSkillAttackCycleSnapshot? cycleSnapshot)
	{
		var actionResult = cycleSnapshot?.SchedulerCallbackOutcome?.ActionResult
			?? cycleSnapshot?.ActionWorkflowPreview?.ActionResult;

		return actionResult == null
			? null
			: PlayerSummonKnownObjectNpcSkillActionSideEffectTrace.FromActionResult(actionResult);
	}

	private static PlayerSummonKnownObjectNpcSkillUseSkillStartTrace? BuildUseSkillStartTrace(
		PlayerSummonKnownObjectNpcSkillAttackCycleSnapshot? cycleSnapshot,
		PlayerSummonKnownObjectNpcSkillActionSideEffectTrace? actionTrace)
	{
		var actionResult = actionTrace?.ActionResult;
		if (actionResult == null)
			return null;

		return PlayerSummonKnownObjectNpcSkillUseSkillStartTrace.FromActionResult(
			actionResult,
			canUseSkillAtCastStart: true,
			isChargeSkill: false,
			castDurationGreaterThanZero: cycleSnapshot?.PerformAttackExecutionPreview?.Status
				== PlayerSummonKnownObjectNpcSkillPerformAttackExecutionPreviewStatus.ScheduledWorkflowDue,
			isNpcEffector: true,
			notifiesCastObservers: true);
	}

	private static PlayerSummonKnownObjectNpcSkillValidationMutationTrace? BuildValidationMutationTrace(
		PlayerSummonKnownObjectNpcSkillActionSideEffectTrace? actionTrace)
	{
		var actionResult = actionTrace?.ActionResult;
		if (actionResult == null)
			return null;

		return PlayerSummonKnownObjectNpcSkillValidationMutationTrace.FromActionResult(
			actionResult,
			hasProperties: true,
			isEndCastValidation: true,
			hasFirstTargetRange: true,
			firstTargetRangePasses: true,
			hasTargetType: true,
			targetRangePasses: true,
			targetRangeMayAppendTargets: true,
			hasTargetRelation: true,
			targetRelationPasses: true,
			targetRelationMayReplaceFirstTarget: true,
			hasTargetStatus: false,
			targetStatusPasses: true,
			hasTargetSpecies: false,
			targetSpeciesPasses: true,
			hasMaxCount: true,
			maxCountPasses: true,
			isPlayerEffector: false,
			playerCanUseSkill: true,
			playerTargetFilterKeepsAny: true,
			targetTypeZero: false,
			isAreaTarget: false);
	}

	private static PlayerSummonKnownObjectNpcSkillTemplateActionTrace? BuildTemplateActionTrace(
		PlayerSummonKnownObjectNpcSkillActionSideEffectTrace? actionTrace)
	{
		var actionResult = actionTrace?.ActionResult;
		if (actionResult == null)
			return null;

		return PlayerSummonKnownObjectNpcSkillTemplateActionTrace.FromActionResult(
			actionResult,
			[
				PlayerSummonKnownObjectNpcSkillTemplateActionKind.MpUse,
				PlayerSummonKnownObjectNpcSkillTemplateActionKind.HpUse,
				PlayerSummonKnownObjectNpcSkillTemplateActionKind.DpUse,
				PlayerSummonKnownObjectNpcSkillTemplateActionKind.ItemUse,
			],
			failingAction: null,
			effectorIsPlayer: true,
			mpRatio: false,
			hpRatio: false,
			boostSkillCostPresent: false);
	}

	private static PlayerSummonKnownObjectNpcSkillEndCastBranchTrace? BuildEndCastBranchTrace(
		PlayerSummonKnownObjectNpcSkillAttackCycleSnapshot? cycleSnapshot,
		PlayerSummonKnownObjectNpcSkillActionSideEffectTrace? actionTrace)
	{
		var actionResult = actionTrace?.ActionResult;
		if (actionResult == null)
			return null;

		return PlayerSummonKnownObjectNpcSkillEndCastBranchTrace.FromActionResult(
			actionResult,
			effectorStillCasting: true,
			endCastValidationPasses: true,
			isItemSkill: false,
			itemAvailable: true,
			itemConsumed: true,
			actionsSucceed: true,
			hasEffects: true,
			allEffectsResistedOrDodged: false,
			isPlayerEffector: false,
			isMultiCast: false,
			hasChainCategory: false,
			chainSucceeds: true,
			isInstantSkill: cycleSnapshot?.PerformAttackExecutionPreview?.Status
				== PlayerSummonKnownObjectNpcSkillPerformAttackExecutionPreviewStatus.ImmediateWorkflow,
			sendsCastResult: true,
			isNpcEffector: true,
			isCastSkillMethod: true);
	}

	private static PlayerSummonKnownObjectNpcSkillEffectInitializationTrace? BuildEffectInitializationTrace(
		PlayerSummonKnownObjectNpcSkillAttackCycleSnapshot? cycleSnapshot,
		PlayerSummonKnownObjectNpcSkillActionSideEffectTrace? actionTrace)
	{
		var actionResult = actionTrace?.ActionResult;
		if (actionResult == null)
			return null;

		return PlayerSummonKnownObjectNpcSkillEffectInitializationTrace.FromActionResult(
			actionResult,
			hasSkillEffects: true,
			effectedCount: 1,
			isPointPointSkill: false,
			hasConflictOnPlayerTarget: false,
			allEffectsResistedOrDodged: false,
			firstTargetReceivesDashStatus: true,
			successEffectsEmpty: false,
			emptySuccessFromPhysicalEffect: false,
			launchesSubEffect: false,
			mayCreateCriticalSubEffect: false,
			isInstantSkill: cycleSnapshot?.PerformAttackExecutionPreview?.Status
				== PlayerSummonKnownObjectNpcSkillPerformAttackExecutionPreviewStatus.ImmediateWorkflow,
			hasResistedTauntHate: false);
	}

	private static PlayerSummonKnownObjectNpcSkillCastResultPacketTrace? BuildCastResultPacketTrace(
		PlayerSummonKnownObjectNpcSkillActionSideEffectTrace? actionTrace)
	{
		var actionResult = actionTrace?.ActionResult;
		if (actionResult == null)
			return null;

		return PlayerSummonKnownObjectNpcSkillCastResultPacketTrace.FromActionResult(
			actionResult,
			skillMethodSendsCastSpellEnd: true,
			hasItemTemplate: false,
			itemTemplateIsCombatActivated: false,
			skillSubTypeIsAttack: true,
			targetType: 0,
			effectsCount: 1,
			chainSuccess: true,
			dashStatusWritesLocation: false,
			isPenaltySkillMethod: false,
			isItemSkillMethod: false,
			effectorIsPlayer: true,
			effectHasOriginalTarget: true,
			spellStatusWritesLocation: false,
			subEffectWritesLocation: false,
			hasReservedEffects: true,
			shieldBranch: PlayerSummonKnownObjectNpcSkillCastResultShieldBranch.None);
	}

	private static PlayerSummonKnownObjectNpcSkillEndCastSideEffectTrace? BuildEndCastSideEffectTrace(
		PlayerSummonKnownObjectNpcSkillAttackCycleSnapshot? cycleSnapshot,
		PlayerSummonKnownObjectNpcSkillActionSideEffectTrace? actionTrace)
	{
		var actionResult = actionTrace?.ActionResult;
		if (actionResult == null)
			return null;

		return PlayerSummonKnownObjectNpcSkillEndCastSideEffectTrace.FromActionResult(
			actionResult,
			cycleSnapshot?.PostSpawnPreview,
			isInstantSkill: cycleSnapshot?.PerformAttackExecutionPreview?.Status
				== PlayerSummonKnownObjectNpcSkillPerformAttackExecutionPreviewStatus.ImmediateWorkflow,
			skillMethodSendsCastSpellEnd: true,
			isNpcEffector: true,
			isCastSkillMethod: true);
	}

	private static IReadOnlyList<PlayerSummonKnownObjectNpcSkillAttackCycleExpectedSideEffect> BuildExpectedSideEffects(
		PlayerSummonKnownObjectNpcSkillAttackCycleSnapshot? cycleSnapshot)
	{
		if (cycleSnapshot == null)
			return EmptySideEffects;

		var sideEffects = new List<PlayerSummonKnownObjectNpcSkillAttackCycleExpectedSideEffect>();
		var performAttack = cycleSnapshot.PerformAttackPreview;
		var actionResult = cycleSnapshot.SchedulerCallbackOutcome?.ActionResult
			?? cycleSnapshot.ActionWorkflowPreview?.ActionResult;

		if (performAttack?.Status is PlayerSummonKnownObjectNpcSkillPerformAttackPreviewStatus.ImmediateSkillAction
			or PlayerSummonKnownObjectNpcSkillPerformAttackPreviewStatus.ScheduledSkillAction)
			AddOnce(sideEffects, PlayerSummonKnownObjectNpcSkillAttackCycleExpectedSideEffect.AiCastSubState);

		if (performAttack?.Status == PlayerSummonKnownObjectNpcSkillPerformAttackPreviewStatus.ScheduledSkillAction)
			AddOnce(sideEffects, PlayerSummonKnownObjectNpcSkillAttackCycleExpectedSideEffect.SchedulerSkillActionCallback);

		if (performAttack?.Status == PlayerSummonKnownObjectNpcSkillPerformAttackPreviewStatus.TargetTooFar
			|| actionResult?.ShouldAbortCast == true)
		{
			AddOnce(sideEffects, PlayerSummonKnownObjectNpcSkillAttackCycleExpectedSideEffect.ControllerAbortCast);
			AddOnce(sideEffects, PlayerSummonKnownObjectNpcSkillAttackCycleExpectedSideEffect.AiEventTargetTooFar);
		}

		if (actionResult != null)
		{
			if (actionResult.ShouldSetSubStateNone)
				AddOnce(sideEffects, PlayerSummonKnownObjectNpcSkillAttackCycleExpectedSideEffect.AiNoneSubState);
			if (actionResult.ShouldResumeFightAfterInterruptedCast)
				AddOnce(sideEffects, PlayerSummonKnownObjectNpcSkillAttackCycleExpectedSideEffect.AiThinkResumeFight);
			if (actionResult.AiEvent == PlayerSummonKnownObjectNpcSkillAiEvent.TargetGiveUp)
				AddOnce(sideEffects, PlayerSummonKnownObjectNpcSkillAttackCycleExpectedSideEffect.AiEventTargetGiveUp);
			if (actionResult.AiEvent == PlayerSummonKnownObjectNpcSkillAiEvent.AttackComplete)
				AddOnce(sideEffects, PlayerSummonKnownObjectNpcSkillAttackCycleExpectedSideEffect.AiEventAttackComplete);
			if (actionResult.ShouldSetOwnerTarget)
				AddOnce(sideEffects, PlayerSummonKnownObjectNpcSkillAttackCycleExpectedSideEffect.OwnerTargetMutation);
			if (actionResult.ShouldInvokeUseSkill || actionResult.DidInvokeUseSkill)
				AddOnce(sideEffects, PlayerSummonKnownObjectNpcSkillAttackCycleExpectedSideEffect.ControllerUseSkill);
			if (actionResult.ShouldInvokeUseSkill)
			{
				AddOnce(sideEffects, PlayerSummonKnownObjectNpcSkillAttackCycleExpectedSideEffect.SkillEffectApplication);
				AddOnce(sideEffects, PlayerSummonKnownObjectNpcSkillAttackCycleExpectedSideEffect.PacketFanout);
			}
		}

		var postSpawn = cycleSnapshot.PostSpawnPreview;
		if (postSpawn?.ShouldSpawnImmediately == true)
			AddOnce(sideEffects, PlayerSummonKnownObjectNpcSkillAttackCycleExpectedSideEffect.PostSpawnImmediate);
		if (postSpawn?.ShouldScheduleSpawn == true)
			AddOnce(sideEffects, PlayerSummonKnownObjectNpcSkillAttackCycleExpectedSideEffect.PostSpawnScheduled);

		return sideEffects;
	}

	private static IReadOnlyList<PlayerSummonKnownObjectNpcSkillAttackCycleOutcomeBranch> BuildExpectedOutcomeBranches(
		PlayerSummonKnownObjectNpcSkillAttackCycleSnapshot? cycleSnapshot)
	{
		if (cycleSnapshot == null)
			return EmptyOutcomeBranches;

		var branches = new List<PlayerSummonKnownObjectNpcSkillAttackCycleOutcomeBranch>();
		var performAttack = cycleSnapshot.PerformAttackPreview;
		var executionPreview = cycleSnapshot.PerformAttackExecutionPreview;
		var callbackOutcome = cycleSnapshot.SchedulerCallbackOutcome;
		var actionResult = callbackOutcome?.ActionResult ?? cycleSnapshot.ActionWorkflowPreview?.ActionResult;

		switch (performAttack?.Status)
		{
			case PlayerSummonKnownObjectNpcSkillPerformAttackPreviewStatus.TargetTooFar:
				AddOnce(branches, PlayerSummonKnownObjectNpcSkillAttackCycleOutcomeBranch.PreActionTargetTooFar);
				break;
			case PlayerSummonKnownObjectNpcSkillPerformAttackPreviewStatus.CastSubStateUnchanged:
				AddOnce(branches, PlayerSummonKnownObjectNpcSkillAttackCycleOutcomeBranch.CastSubStateUnchanged);
				break;
			case PlayerSummonKnownObjectNpcSkillPerformAttackPreviewStatus.ImmediateSkillAction:
				AddOnce(branches, PlayerSummonKnownObjectNpcSkillAttackCycleOutcomeBranch.CastSubStateEntered);
				AddOnce(branches, PlayerSummonKnownObjectNpcSkillAttackCycleOutcomeBranch.ImmediateSkillAction);
				break;
			case PlayerSummonKnownObjectNpcSkillPerformAttackPreviewStatus.ScheduledSkillAction:
				AddOnce(branches, PlayerSummonKnownObjectNpcSkillAttackCycleOutcomeBranch.CastSubStateEntered);
				AddOnce(branches, PlayerSummonKnownObjectNpcSkillAttackCycleOutcomeBranch.ScheduledSkillAction);
				break;
		}

		if (executionPreview?.IsScheduledPending == true || callbackOutcome?.IsPending == true)
			AddOnce(branches, PlayerSummonKnownObjectNpcSkillAttackCycleOutcomeBranch.SchedulerCallbackPending);

		switch (actionResult?.Status)
		{
			case PlayerSummonKnownObjectNpcSkillActionResultStatus.NoAction:
				AddOnce(branches, PlayerSummonKnownObjectNpcSkillAttackCycleOutcomeBranch.NoAction);
				break;
			case PlayerSummonKnownObjectNpcSkillActionResultStatus.ResumeFightAfterInterruptedCast:
				AddOnce(branches, PlayerSummonKnownObjectNpcSkillAttackCycleOutcomeBranch.ResumeFightAfterInterruptedCast);
				break;
			case PlayerSummonKnownObjectNpcSkillActionResultStatus.TargetGiveUp:
				AddOnce(branches, PlayerSummonKnownObjectNpcSkillAttackCycleOutcomeBranch.TargetGiveUp);
				break;
			case PlayerSummonKnownObjectNpcSkillActionResultStatus.TargetTooFar:
				AddOnce(branches, PlayerSummonKnownObjectNpcSkillAttackCycleOutcomeBranch.SkillActionTargetTooFar);
				break;
			case PlayerSummonKnownObjectNpcSkillActionResultStatus.AfterUseSkill:
				AddOnce(branches, PlayerSummonKnownObjectNpcSkillAttackCycleOutcomeBranch.BlockedSkillAfterUse);
				break;
			case PlayerSummonKnownObjectNpcSkillActionResultStatus.UseSkill:
				AddOnce(branches, PlayerSummonKnownObjectNpcSkillAttackCycleOutcomeBranch.SuccessfulUseSkill);
				break;
			case PlayerSummonKnownObjectNpcSkillActionResultStatus.UseSkillFailed:
				AddOnce(branches, PlayerSummonKnownObjectNpcSkillAttackCycleOutcomeBranch.UseSkillFailedAfterUse);
				break;
		}

		var postSpawn = cycleSnapshot.PostSpawnPreview;
		if (postSpawn?.ShouldSpawnImmediately == true)
			AddOnce(branches, PlayerSummonKnownObjectNpcSkillAttackCycleOutcomeBranch.PostSpawnImmediate);
		if (postSpawn?.ShouldScheduleSpawn == true)
			AddOnce(branches, PlayerSummonKnownObjectNpcSkillAttackCycleOutcomeBranch.PostSpawnScheduled);

		return branches;
	}

	private static IReadOnlyList<PlayerSummonKnownObjectNpcSkillAttackCycleLiveOperation> BuildFutureLiveOperations(
		IReadOnlyList<PlayerSummonKnownObjectNpcSkillAttackCycleOutcomeBranch> outcomeBranches,
		IReadOnlyList<PlayerSummonKnownObjectNpcSkillAttackCycleExpectedSideEffect> sideEffects)
	{
		if (outcomeBranches.Count == 0 && sideEffects.Count == 0)
			return EmptyLiveOperations;

		var operations = new List<PlayerSummonKnownObjectNpcSkillAttackCycleLiveOperation>();
		foreach (var branch in outcomeBranches)
		{
			switch (branch)
			{
				case PlayerSummonKnownObjectNpcSkillAttackCycleOutcomeBranch.PreActionTargetTooFar:
				case PlayerSummonKnownObjectNpcSkillAttackCycleOutcomeBranch.SkillActionTargetTooFar:
					AddOnce(operations, PlayerSummonKnownObjectNpcSkillAttackCycleLiveOperation.ControllerAbortCast);
					AddOnce(operations, PlayerSummonKnownObjectNpcSkillAttackCycleLiveOperation.NpcAiOnTargetTooFar);
					break;
				case PlayerSummonKnownObjectNpcSkillAttackCycleOutcomeBranch.CastSubStateEntered:
					AddOnce(operations, PlayerSummonKnownObjectNpcSkillAttackCycleLiveOperation.NpcAiSetCastSubState);
					break;
				case PlayerSummonKnownObjectNpcSkillAttackCycleOutcomeBranch.ScheduledSkillAction:
					AddOnce(operations, PlayerSummonKnownObjectNpcSkillAttackCycleLiveOperation.ThreadPoolScheduleSkillAction);
					break;
				case PlayerSummonKnownObjectNpcSkillAttackCycleOutcomeBranch.SchedulerCallbackPending:
					AddOnce(operations, PlayerSummonKnownObjectNpcSkillAttackCycleLiveOperation.ThreadPoolAwaitScheduledCallback);
					break;
				case PlayerSummonKnownObjectNpcSkillAttackCycleOutcomeBranch.ResumeFightAfterInterruptedCast:
					AddOnce(operations, PlayerSummonKnownObjectNpcSkillAttackCycleLiveOperation.NpcAiThink);
					break;
				case PlayerSummonKnownObjectNpcSkillAttackCycleOutcomeBranch.TargetGiveUp:
					AddOnce(operations, PlayerSummonKnownObjectNpcSkillAttackCycleLiveOperation.NpcAiSetNoneSubState);
					AddOnce(operations, PlayerSummonKnownObjectNpcSkillAttackCycleLiveOperation.NpcAiOnTargetGiveUp);
					break;
				case PlayerSummonKnownObjectNpcSkillAttackCycleOutcomeBranch.BlockedSkillAfterUse:
					AddOnce(operations, PlayerSummonKnownObjectNpcSkillAttackCycleLiveOperation.NpcAiSetNoneSubState);
					AddOnce(operations, PlayerSummonKnownObjectNpcSkillAttackCycleLiveOperation.NpcAiOnAttackComplete);
					break;
				case PlayerSummonKnownObjectNpcSkillAttackCycleOutcomeBranch.SuccessfulUseSkill:
					AddOnce(operations, PlayerSummonKnownObjectNpcSkillAttackCycleLiveOperation.CreatureControllerUseSkill);
					break;
				case PlayerSummonKnownObjectNpcSkillAttackCycleOutcomeBranch.UseSkillFailedAfterUse:
					AddOnce(operations, PlayerSummonKnownObjectNpcSkillAttackCycleLiveOperation.CreatureControllerUseSkill);
					AddOnce(operations, PlayerSummonKnownObjectNpcSkillAttackCycleLiveOperation.NpcAiSetNoneSubState);
					AddOnce(operations, PlayerSummonKnownObjectNpcSkillAttackCycleLiveOperation.NpcAiOnAttackComplete);
					break;
				case PlayerSummonKnownObjectNpcSkillAttackCycleOutcomeBranch.PostSpawnImmediate:
					AddOnce(operations, PlayerSummonKnownObjectNpcSkillAttackCycleLiveOperation.PostSpawnImmediate);
					break;
				case PlayerSummonKnownObjectNpcSkillAttackCycleOutcomeBranch.PostSpawnScheduled:
					AddOnce(operations, PlayerSummonKnownObjectNpcSkillAttackCycleLiveOperation.PostSpawnSchedule);
					break;
			}
		}

		if (sideEffects.Contains(PlayerSummonKnownObjectNpcSkillAttackCycleExpectedSideEffect.OwnerTargetMutation))
			AddOnce(operations, PlayerSummonKnownObjectNpcSkillAttackCycleLiveOperation.OwnerSetTarget);
		if (sideEffects.Contains(PlayerSummonKnownObjectNpcSkillAttackCycleExpectedSideEffect.SkillEffectApplication))
			AddOnce(operations, PlayerSummonKnownObjectNpcSkillAttackCycleLiveOperation.SkillEffectApplication);
		if (sideEffects.Contains(PlayerSummonKnownObjectNpcSkillAttackCycleExpectedSideEffect.PacketFanout))
			AddOnce(operations, PlayerSummonKnownObjectNpcSkillAttackCycleLiveOperation.PacketFanout);

		return operations;
	}

	private static void AddOnce(
		List<PlayerSummonKnownObjectNpcSkillAttackCycleExpectedSideEffect> sideEffects,
		PlayerSummonKnownObjectNpcSkillAttackCycleExpectedSideEffect sideEffect)
	{
		if (!sideEffects.Contains(sideEffect))
			sideEffects.Add(sideEffect);
	}

	private static void AddOnce(
		List<PlayerSummonKnownObjectNpcSkillAttackCycleOutcomeBranch> branches,
		PlayerSummonKnownObjectNpcSkillAttackCycleOutcomeBranch branch)
	{
		if (!branches.Contains(branch))
			branches.Add(branch);
	}

	private static void AddOnce(
		List<PlayerSummonKnownObjectNpcSkillAttackCycleLiveOperation> operations,
		PlayerSummonKnownObjectNpcSkillAttackCycleLiveOperation operation)
	{
		if (!operations.Contains(operation))
			operations.Add(operation);
	}
}

public enum PlayerSummonKnownObjectNpcSkillAttackCycleResultContractStatus
{
	MissingInvocation,
	BlockedByInvocationReadiness,
	LiveAiNotWired,
}

public enum PlayerSummonKnownObjectNpcSkillAttackCycleExpectedSideEffect
{
	AiCastSubState,
	AiNoneSubState,
	AiThinkResumeFight,
	AiEventTargetTooFar,
	AiEventTargetGiveUp,
	AiEventAttackComplete,
	ControllerAbortCast,
	ControllerUseSkill,
	OwnerTargetMutation,
	SkillEffectApplication,
	PostSpawnImmediate,
	PostSpawnScheduled,
	SchedulerSkillActionCallback,
	PacketFanout,
}

public enum PlayerSummonKnownObjectNpcSkillAttackCycleOutcomeBranch
{
	MissingInvocation,
	BlockedByInvocationReadiness,
	PreActionTargetTooFar,
	CastSubStateUnchanged,
	CastSubStateEntered,
	ScheduledSkillAction,
	ImmediateSkillAction,
	SchedulerCallbackPending,
	NoAction,
	ResumeFightAfterInterruptedCast,
	TargetGiveUp,
	SkillActionTargetTooFar,
	BlockedSkillAfterUse,
	SuccessfulUseSkill,
	UseSkillFailedAfterUse,
	PostSpawnImmediate,
	PostSpawnScheduled,
}

public enum PlayerSummonKnownObjectNpcSkillAttackCycleLiveOperation
{
	NpcAiSetCastSubState,
	NpcAiSetNoneSubState,
	NpcAiThink,
	NpcAiOnTargetTooFar,
	NpcAiOnTargetGiveUp,
	NpcAiOnAttackComplete,
	ThreadPoolScheduleSkillAction,
	ThreadPoolAwaitScheduledCallback,
	ControllerAbortCast,
	CreatureControllerUseSkill,
	OwnerSetTarget,
	SkillEffectApplication,
	PostSpawnImmediate,
	PostSpawnSchedule,
	PacketFanout,
}

public sealed record PlayerSummonKnownObjectNpcSkillAttackCycleOperationReadiness(
	PlayerSummonKnownObjectNpcSkillAttackCycleOperationReadinessStatus Status,
	PlayerSummonKnownObjectNpcSkillAttackCycleResultContract? ResultContract = null,
	IReadOnlyList<PlayerSummonKnownObjectNpcSkillAttackCycleDependencyReadiness>? Dependencies = null)
{
	private static readonly IReadOnlyList<PlayerSummonKnownObjectNpcSkillAttackCycleDependencyReadiness> EmptyDependencies = [];

	public IReadOnlyList<PlayerSummonKnownObjectNpcSkillAttackCycleDependencyReadiness> DependencyReadiness =>
		Dependencies ?? EmptyDependencies;

	public bool HasUnsupportedDependencies =>
		DependencyReadiness.Any(dependency => !dependency.IsSupported);

	public bool WouldExecuteOperations => false;

	public bool HasDependency(PlayerSummonKnownObjectNpcSkillAttackCycleDependency dependency)
	{
		return DependencyReadiness.Any(readiness => readiness.Dependency == dependency);
	}

	public static PlayerSummonKnownObjectNpcSkillAttackCycleOperationReadiness FromResultContract(
		PlayerSummonKnownObjectNpcSkillAttackCycleResultContract? resultContract)
	{
		if (resultContract == null)
		{
			return new PlayerSummonKnownObjectNpcSkillAttackCycleOperationReadiness(
				PlayerSummonKnownObjectNpcSkillAttackCycleOperationReadinessStatus.MissingResultContract);
		}

		if (resultContract.Status != PlayerSummonKnownObjectNpcSkillAttackCycleResultContractStatus.LiveAiNotWired)
		{
			return new PlayerSummonKnownObjectNpcSkillAttackCycleOperationReadiness(
				PlayerSummonKnownObjectNpcSkillAttackCycleOperationReadinessStatus.BlockedByResultContract,
				resultContract);
		}

		var dependencies = BuildDependencyReadiness(resultContract.FutureLiveAdapterOperations);
		return dependencies.Count == 0
			? new PlayerSummonKnownObjectNpcSkillAttackCycleOperationReadiness(
				PlayerSummonKnownObjectNpcSkillAttackCycleOperationReadinessStatus.NoFutureOperations,
				resultContract,
				dependencies)
			: new PlayerSummonKnownObjectNpcSkillAttackCycleOperationReadiness(
				PlayerSummonKnownObjectNpcSkillAttackCycleOperationReadinessStatus.UnsupportedDependencies,
				resultContract,
				dependencies);
	}

	private static IReadOnlyList<PlayerSummonKnownObjectNpcSkillAttackCycleDependencyReadiness> BuildDependencyReadiness(
		IReadOnlyList<PlayerSummonKnownObjectNpcSkillAttackCycleLiveOperation> operations)
	{
		if (operations.Count == 0)
			return EmptyDependencies;

		var groups = new Dictionary<PlayerSummonKnownObjectNpcSkillAttackCycleDependency, List<PlayerSummonKnownObjectNpcSkillAttackCycleLiveOperation>>();
		foreach (var operation in operations)
		{
			var dependency = ResolveDependency(operation);
			if (!groups.TryGetValue(dependency, out var groupedOperations))
			{
				groupedOperations = [];
				groups[dependency] = groupedOperations;
			}

			groupedOperations.Add(operation);
		}

		return groups
			.OrderBy(group => group.Key)
			.Select(group => PlayerSummonKnownObjectNpcSkillAttackCycleDependencyReadiness.Unsupported(
				group.Key,
				group.Value))
			.ToArray();
	}

	private static PlayerSummonKnownObjectNpcSkillAttackCycleDependency ResolveDependency(
		PlayerSummonKnownObjectNpcSkillAttackCycleLiveOperation operation)
	{
		return operation switch
		{
			PlayerSummonKnownObjectNpcSkillAttackCycleLiveOperation.NpcAiSetCastSubState
				or PlayerSummonKnownObjectNpcSkillAttackCycleLiveOperation.NpcAiSetNoneSubState
				or PlayerSummonKnownObjectNpcSkillAttackCycleLiveOperation.NpcAiThink
				or PlayerSummonKnownObjectNpcSkillAttackCycleLiveOperation.NpcAiOnTargetTooFar
				or PlayerSummonKnownObjectNpcSkillAttackCycleLiveOperation.NpcAiOnTargetGiveUp
				or PlayerSummonKnownObjectNpcSkillAttackCycleLiveOperation.NpcAiOnAttackComplete
				=> PlayerSummonKnownObjectNpcSkillAttackCycleDependency.NpcAi,
			PlayerSummonKnownObjectNpcSkillAttackCycleLiveOperation.ThreadPoolScheduleSkillAction
				or PlayerSummonKnownObjectNpcSkillAttackCycleLiveOperation.ThreadPoolAwaitScheduledCallback
				=> PlayerSummonKnownObjectNpcSkillAttackCycleDependency.Scheduler,
			PlayerSummonKnownObjectNpcSkillAttackCycleLiveOperation.ControllerAbortCast
				or PlayerSummonKnownObjectNpcSkillAttackCycleLiveOperation.CreatureControllerUseSkill
				or PlayerSummonKnownObjectNpcSkillAttackCycleLiveOperation.OwnerSetTarget
				=> PlayerSummonKnownObjectNpcSkillAttackCycleDependency.Controller,
			_ => PlayerSummonKnownObjectNpcSkillAttackCycleDependency.PacketEffectPostSpawn,
		};
	}
}

public enum PlayerSummonKnownObjectNpcSkillAttackCycleOperationReadinessStatus
{
	MissingResultContract,
	BlockedByResultContract,
	NoFutureOperations,
	UnsupportedDependencies,
}

public sealed record PlayerSummonKnownObjectNpcSkillAttackCycleDependencyReadiness(
	PlayerSummonKnownObjectNpcSkillAttackCycleDependency Dependency,
	IReadOnlyList<PlayerSummonKnownObjectNpcSkillAttackCycleLiveOperation> Operations,
	bool IsSupported)
{
	public bool IsUnsupported => !IsSupported;

	public static PlayerSummonKnownObjectNpcSkillAttackCycleDependencyReadiness Unsupported(
		PlayerSummonKnownObjectNpcSkillAttackCycleDependency dependency,
		IReadOnlyList<PlayerSummonKnownObjectNpcSkillAttackCycleLiveOperation> operations)
	{
		return new PlayerSummonKnownObjectNpcSkillAttackCycleDependencyReadiness(
			dependency,
			operations,
			IsSupported: false);
	}
}

public enum PlayerSummonKnownObjectNpcSkillAttackCycleDependency
{
	NpcAi,
	Scheduler,
	Controller,
	PacketEffectPostSpawn,
}

public sealed record PlayerSummonKnownObjectNpcSkillAttackCycleAdapterSummary(
	PlayerSummonKnownObjectNpcSkillAttackCycleAdapterSummaryStatus Status,
	PlayerSummonKnownObjectNpcSkillAttackCycleSnapshot? CycleSnapshot,
	PlayerSummonKnownObjectNpcSkillAttackCycleReadiness Readiness,
	PlayerSummonKnownObjectNpcSkillAttackCycleLiveInvocation LiveInvocation,
	PlayerSummonKnownObjectNpcSkillAttackCycleResultContract ResultContract,
	PlayerSummonKnownObjectNpcSkillAttackCycleOperationReadiness OperationReadiness)
{
	public bool IsReadyForFutureLiveAdapter =>
		Status == PlayerSummonKnownObjectNpcSkillAttackCycleAdapterSummaryStatus.LiveAiNotWired
		&& Readiness.IsReadyForLiveAiAdapter
		&& LiveInvocation.IsLiveAiUnsupported
		&& ResultContract.IsLiveAiUnsupported
		&& OperationReadiness.Status == PlayerSummonKnownObjectNpcSkillAttackCycleOperationReadinessStatus.UnsupportedDependencies;

	public bool HasUnsupportedDependencies => OperationReadiness.HasUnsupportedDependencies;

	public bool WouldExecuteLiveAdapter => false;

	public IReadOnlyList<PlayerSummonKnownObjectNpcSkillAttackCycleLiveOperation> FutureLiveAdapterOperations =>
		ResultContract.FutureLiveAdapterOperations;

	public IReadOnlyList<PlayerSummonKnownObjectNpcSkillAttackCycleDependencyReadiness> DependencyReadiness =>
		OperationReadiness.DependencyReadiness;

	public static PlayerSummonKnownObjectNpcSkillAttackCycleAdapterSummary FromComponents(
		PlayerSummonKnownObjectNpcSkillAttackCycleSnapshot? cycleSnapshot,
		PlayerSummonKnownObjectNpcSkillAttackCycleReadiness readiness,
		PlayerSummonKnownObjectNpcSkillAttackCycleLiveInvocation liveInvocation,
		PlayerSummonKnownObjectNpcSkillAttackCycleResultContract resultContract,
		PlayerSummonKnownObjectNpcSkillAttackCycleOperationReadiness operationReadiness)
	{
		var status = readiness.Status switch
		{
			PlayerSummonKnownObjectNpcSkillAttackCycleReadinessStatus.MissingCycle =>
				PlayerSummonKnownObjectNpcSkillAttackCycleAdapterSummaryStatus.MissingCycle,
			PlayerSummonKnownObjectNpcSkillAttackCycleReadinessStatus.MissingKnownObject =>
				PlayerSummonKnownObjectNpcSkillAttackCycleAdapterSummaryStatus.MissingKnownObject,
			PlayerSummonKnownObjectNpcSkillAttackCycleReadinessStatus.MissingRequiredMetadata =>
				PlayerSummonKnownObjectNpcSkillAttackCycleAdapterSummaryStatus.MissingRequiredMetadata,
			_ => PlayerSummonKnownObjectNpcSkillAttackCycleAdapterSummaryStatus.LiveAiNotWired,
		};

		return new PlayerSummonKnownObjectNpcSkillAttackCycleAdapterSummary(
			status,
			cycleSnapshot,
			readiness,
			liveInvocation,
			resultContract,
			operationReadiness);
	}
}

public sealed record PlayerSummonKnownObjectNpcSkillAttackCycleLiveAdapterContract(
	PlayerSummonKnownObjectNpcSkillAttackCycleLiveAdapterContractStatus Status,
	PlayerSummonKnownObjectNpcSkillAttackCycleAdapterSummary? AdapterSummary = null,
	IReadOnlyList<PlayerSummonKnownObjectNpcSkillAttackCycleLiveOperation>? LiveOperations = null,
	IReadOnlyList<PlayerSummonKnownObjectNpcSkillAttackCycleDependencyReadiness>? DependencyReadiness = null,
	IReadOnlyList<string>? UnsupportedJavaBehaviors = null,
	PlayerSummonKnownObjectNpcSkillActionSideEffectTrace? ActionSideEffectTrace = null,
	PlayerSummonKnownObjectNpcSkillUseSkillStartTrace? UseSkillStartTrace = null,
	PlayerSummonKnownObjectNpcSkillValidationMutationTrace? ValidationMutationTrace = null,
	PlayerSummonKnownObjectNpcSkillTemplateActionTrace? TemplateActionTrace = null,
	PlayerSummonKnownObjectNpcSkillEffectInitializationTrace? EffectInitializationTrace = null,
	PlayerSummonKnownObjectNpcSkillCastResultPacketTrace? CastResultPacketTrace = null,
	PlayerSummonKnownObjectNpcSkillEndCastBranchTrace? EndCastBranchTrace = null,
	PlayerSummonKnownObjectNpcSkillEndCastSideEffectTrace? EndCastSideEffectTrace = null)
{
	private static readonly IReadOnlyList<PlayerSummonKnownObjectNpcSkillAttackCycleLiveOperation> EmptyOperations = [];
	private static readonly IReadOnlyList<PlayerSummonKnownObjectNpcSkillAttackCycleDependencyReadiness> EmptyDependencies = [];
	private static readonly IReadOnlyList<string> EmptyUnsupportedBehaviors = [];

	public IReadOnlyList<PlayerSummonKnownObjectNpcSkillAttackCycleLiveOperation> FutureLiveOperations =>
		LiveOperations ?? EmptyOperations;

	public IReadOnlyList<PlayerSummonKnownObjectNpcSkillAttackCycleDependencyReadiness> RequiredDependencies =>
		DependencyReadiness ?? EmptyDependencies;

	public IReadOnlyList<string> UnsupportedBehaviors =>
		UnsupportedJavaBehaviors ?? EmptyUnsupportedBehaviors;

	public bool IsBlocked =>
		Status is PlayerSummonKnownObjectNpcSkillAttackCycleLiveAdapterContractStatus.MissingSummary
			or PlayerSummonKnownObjectNpcSkillAttackCycleLiveAdapterContractStatus.BlockedBySummary;

	public bool IsReadyButUnsupported =>
		Status == PlayerSummonKnownObjectNpcSkillAttackCycleLiveAdapterContractStatus.ReadyButUnsupported;

	public bool WouldExecuteLiveAdapter => false;

	public bool PreservesActionSideEffectOrdering => ActionSideEffectTrace != null;

	public bool PreservesUseSkillStartOrdering => UseSkillStartTrace != null;

	public bool PreservesValidationMutationOrdering => ValidationMutationTrace != null;

	public bool PreservesTemplateActionOrdering => TemplateActionTrace != null;

	public bool PreservesEffectInitializationOrdering => EffectInitializationTrace != null;

	public bool PreservesCastResultPacketOrdering => CastResultPacketTrace != null;

	public bool PreservesEndCastBranchOrdering => EndCastBranchTrace != null;

	public bool PreservesEndCastSideEffectOrdering => EndCastSideEffectTrace != null;

	public bool RequiresDependency(PlayerSummonKnownObjectNpcSkillAttackCycleDependency dependency)
	{
		return RequiredDependencies.Any(readiness => readiness.Dependency == dependency);
	}

	public bool RequiresOperation(PlayerSummonKnownObjectNpcSkillAttackCycleLiveOperation operation)
	{
		return FutureLiveOperations.Contains(operation);
	}

	public static PlayerSummonKnownObjectNpcSkillAttackCycleLiveAdapterContract FromSummary(
		PlayerSummonKnownObjectNpcSkillAttackCycleAdapterSummary? adapterSummary)
	{
		if (adapterSummary == null)
		{
			return new PlayerSummonKnownObjectNpcSkillAttackCycleLiveAdapterContract(
				PlayerSummonKnownObjectNpcSkillAttackCycleLiveAdapterContractStatus.MissingSummary);
		}

		if (!adapterSummary.IsReadyForFutureLiveAdapter)
		{
			return new PlayerSummonKnownObjectNpcSkillAttackCycleLiveAdapterContract(
				PlayerSummonKnownObjectNpcSkillAttackCycleLiveAdapterContractStatus.BlockedBySummary,
				adapterSummary);
		}

		return new PlayerSummonKnownObjectNpcSkillAttackCycleLiveAdapterContract(
			PlayerSummonKnownObjectNpcSkillAttackCycleLiveAdapterContractStatus.ReadyButUnsupported,
			adapterSummary,
			adapterSummary.FutureLiveAdapterOperations,
			adapterSummary.DependencyReadiness,
			adapterSummary.LiveInvocation.UnsupportedBehaviors,
			adapterSummary.ResultContract.ActionSideEffectTrace,
			adapterSummary.ResultContract.UseSkillStartTrace,
			adapterSummary.ResultContract.ValidationMutationTrace,
			adapterSummary.ResultContract.TemplateActionTrace,
			adapterSummary.ResultContract.EffectInitializationTrace,
			adapterSummary.ResultContract.CastResultPacketTrace,
			adapterSummary.ResultContract.EndCastBranchTrace,
			adapterSummary.ResultContract.EndCastSideEffectTrace);
	}
}

public enum PlayerSummonKnownObjectNpcSkillAttackCycleLiveAdapterContractStatus
{
	MissingSummary,
	BlockedBySummary,
	ReadyButUnsupported,
}

public enum PlayerSummonKnownObjectNpcSkillAttackCycleAdapterSummaryStatus
{
	MissingCycle,
	MissingKnownObject,
	MissingRequiredMetadata,
	LiveAiNotWired,
}

public sealed record PlayerSummonKnownObjectNpcSkillActionWorkflowPreview(
	PlayerSummonKnownObjectNpcSkillActionWorkflowPreviewStatus Status,
	PlayerSummonKnownObject KnownObject,
	PlayerSummonKnownObjectNpcSkillSelectionPreview? SelectionPreview = null,
	PlayerSummonKnownObjectNpcSkillCandidate? SelectedCandidate = null,
	PlayerSummonKnownObjectTargetRangeReadiness? TargetRangeReadiness = null,
	PlayerSummonKnownObjectTargetRangeDelayResult? TargetRangeDelay = null,
	PlayerSummonKnownObjectSkillReadiness? SkillReadiness = null,
	PlayerSummonKnownObjectNpcSkillActionTargetSelection? TargetSelection = null,
	PlayerSummonKnownObjectNpcSkillActionPreview? ActionPreview = null,
	PlayerSummonKnownObjectNpcSkillActionResult? ActionResult = null)
{
	public bool ShouldSetNextSkillDelay => TargetRangeReadiness?.ShouldSetNextSkillDelay == true;

	public bool DidStoreNextSkillDelay => TargetRangeDelay?.Status == PlayerSummonKnownObjectTargetRangeDelayStatus.Set;

	public bool WouldInvokeSkillAction => Status == PlayerSummonKnownObjectNpcSkillActionWorkflowPreviewStatus.Projected
		&& ActionPreview != null
		&& ActionResult != null;

	public static PlayerSummonKnownObjectNpcSkillActionWorkflowPreview MissingSelectionPreview(
		PlayerSummonKnownObject knownObject)
	{
		return new PlayerSummonKnownObjectNpcSkillActionWorkflowPreview(
			PlayerSummonKnownObjectNpcSkillActionWorkflowPreviewStatus.MissingSelectionPreview,
			knownObject,
			ActionResult: PlayerSummonKnownObjectNpcSkillActionResult.MissingPreview());
	}

	public static PlayerSummonKnownObjectNpcSkillActionWorkflowPreview NoSelectedCandidate(
		PlayerSummonKnownObject knownObject,
		PlayerSummonKnownObjectNpcSkillSelectionPreview selectionPreview)
	{
		return new PlayerSummonKnownObjectNpcSkillActionWorkflowPreview(
			PlayerSummonKnownObjectNpcSkillActionWorkflowPreviewStatus.NoSelectedCandidate,
			knownObject,
			selectionPreview,
			ActionResult: PlayerSummonKnownObjectNpcSkillActionResult.MissingPreview());
	}

	public static PlayerSummonKnownObjectNpcSkillActionWorkflowPreview TargetRangeNotReady(
		PlayerSummonKnownObject knownObject,
		PlayerSummonKnownObjectNpcSkillSelectionPreview selectionPreview,
		PlayerSummonKnownObjectNpcSkillCandidate selectedCandidate,
		PlayerSummonKnownObjectTargetRangeReadiness targetRangeReadiness,
		PlayerSummonKnownObjectTargetRangeDelayResult? targetRangeDelay)
	{
		return new PlayerSummonKnownObjectNpcSkillActionWorkflowPreview(
			PlayerSummonKnownObjectNpcSkillActionWorkflowPreviewStatus.TargetRangeNotReady,
			knownObject,
			selectionPreview,
			selectedCandidate,
			targetRangeReadiness,
			targetRangeDelay,
			ActionResult: PlayerSummonKnownObjectNpcSkillActionResult.MissingPreview());
	}

	public static PlayerSummonKnownObjectNpcSkillActionWorkflowPreview Projected(
		PlayerSummonKnownObject knownObject,
		PlayerSummonKnownObjectNpcSkillSelectionPreview selectionPreview,
		PlayerSummonKnownObjectNpcSkillCandidate selectedCandidate,
		PlayerSummonKnownObjectTargetRangeReadiness targetRangeReadiness,
		PlayerSummonKnownObjectTargetRangeDelayResult? targetRangeDelay,
		PlayerSummonKnownObjectSkillReadiness skillReadiness,
		PlayerSummonKnownObjectNpcSkillActionTargetSelection targetSelection,
		PlayerSummonKnownObjectNpcSkillActionPreview actionPreview,
		PlayerSummonKnownObjectNpcSkillActionResult actionResult)
	{
		return new PlayerSummonKnownObjectNpcSkillActionWorkflowPreview(
			PlayerSummonKnownObjectNpcSkillActionWorkflowPreviewStatus.Projected,
			knownObject,
			selectionPreview,
			selectedCandidate,
			targetRangeReadiness,
			targetRangeDelay,
			skillReadiness,
			targetSelection,
			actionPreview,
			actionResult);
	}
}

public enum PlayerSummonKnownObjectNpcSkillActionWorkflowPreviewStatus
{
	MissingSelectionPreview,
	NoSelectedCandidate,
	TargetRangeNotReady,
	Projected,
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

public sealed record PlayerSummonKnownObjectNpcSkillActionResult(
	PlayerSummonKnownObjectNpcSkillActionResultStatus Status,
	PlayerSummonKnownObjectNpcSkillActionPreview? Preview = null,
	PlayerSummonKnownObjectNpcSkillAiEvent AiEvent = PlayerSummonKnownObjectNpcSkillAiEvent.None)
{
	public bool ShouldSetSubStateNone =>
		AiEvent is PlayerSummonKnownObjectNpcSkillAiEvent.TargetGiveUp
			or PlayerSummonKnownObjectNpcSkillAiEvent.AttackComplete;

	public bool ShouldDispatchAiEvent => AiEvent != PlayerSummonKnownObjectNpcSkillAiEvent.None;

	public bool ShouldAbortCast => Status == PlayerSummonKnownObjectNpcSkillActionResultStatus.TargetTooFar;

	public bool ShouldSetOwnerTarget => Preview?.ShouldSetOwnerTarget == true
		|| (Preview?.TargetSelection?.ShouldSetOwnerTarget == true
			&& Status == PlayerSummonKnownObjectNpcSkillActionResultStatus.UseSkillFailed);

	public bool ShouldInvokeUseSkill => Status == PlayerSummonKnownObjectNpcSkillActionResultStatus.UseSkill;

	public bool DidInvokeUseSkill => Status == PlayerSummonKnownObjectNpcSkillActionResultStatus.UseSkillFailed;

	public bool ShouldResumeFightAfterInterruptedCast => Status == PlayerSummonKnownObjectNpcSkillActionResultStatus.ResumeFightAfterInterruptedCast;

	public static PlayerSummonKnownObjectNpcSkillActionResult MissingPreview()
	{
		return new PlayerSummonKnownObjectNpcSkillActionResult(PlayerSummonKnownObjectNpcSkillActionResultStatus.MissingPreview);
	}

	public static PlayerSummonKnownObjectNpcSkillActionResult NoAction(PlayerSummonKnownObjectNpcSkillActionPreview preview)
	{
		return new PlayerSummonKnownObjectNpcSkillActionResult(
			PlayerSummonKnownObjectNpcSkillActionResultStatus.NoAction,
			preview);
	}

	public static PlayerSummonKnownObjectNpcSkillActionResult ResumeFight(PlayerSummonKnownObjectNpcSkillActionPreview preview)
	{
		return new PlayerSummonKnownObjectNpcSkillActionResult(
			PlayerSummonKnownObjectNpcSkillActionResultStatus.ResumeFightAfterInterruptedCast,
			preview);
	}

	public static PlayerSummonKnownObjectNpcSkillActionResult TargetGiveUp(PlayerSummonKnownObjectNpcSkillActionPreview preview)
	{
		return new PlayerSummonKnownObjectNpcSkillActionResult(
			PlayerSummonKnownObjectNpcSkillActionResultStatus.TargetGiveUp,
			preview,
			PlayerSummonKnownObjectNpcSkillAiEvent.TargetGiveUp);
	}

	public static PlayerSummonKnownObjectNpcSkillActionResult TargetTooFar(PlayerSummonKnownObjectNpcSkillActionPreview preview)
	{
		return new PlayerSummonKnownObjectNpcSkillActionResult(
			PlayerSummonKnownObjectNpcSkillActionResultStatus.TargetTooFar,
			preview,
			PlayerSummonKnownObjectNpcSkillAiEvent.TargetTooFar);
	}

	public static PlayerSummonKnownObjectNpcSkillActionResult AfterUseSkill(PlayerSummonKnownObjectNpcSkillActionPreview preview)
	{
		return new PlayerSummonKnownObjectNpcSkillActionResult(
			PlayerSummonKnownObjectNpcSkillActionResultStatus.AfterUseSkill,
			preview,
			PlayerSummonKnownObjectNpcSkillAiEvent.AttackComplete);
	}

	public static PlayerSummonKnownObjectNpcSkillActionResult UseSkill(PlayerSummonKnownObjectNpcSkillActionPreview preview)
	{
		return new PlayerSummonKnownObjectNpcSkillActionResult(
			PlayerSummonKnownObjectNpcSkillActionResultStatus.UseSkill,
			preview);
	}

	public static PlayerSummonKnownObjectNpcSkillActionResult UseSkillFailed(PlayerSummonKnownObjectNpcSkillActionPreview preview)
	{
		return new PlayerSummonKnownObjectNpcSkillActionResult(
			PlayerSummonKnownObjectNpcSkillActionResultStatus.UseSkillFailed,
			preview,
			PlayerSummonKnownObjectNpcSkillAiEvent.AttackComplete);
	}
}

public sealed record PlayerSummonKnownObjectNpcSkillActionSideEffectTrace(
	PlayerSummonKnownObjectNpcSkillActionSideEffectTraceStatus Status,
	PlayerSummonKnownObjectNpcSkillActionResult? ActionResult = null,
	IReadOnlyList<PlayerSummonKnownObjectNpcSkillActionSideEffectStep>? Steps = null)
{
	private static readonly IReadOnlyList<PlayerSummonKnownObjectNpcSkillActionSideEffectStep> EmptySteps = [];

	public IReadOnlyList<PlayerSummonKnownObjectNpcSkillActionSideEffectStep> OrderedSteps => Steps ?? EmptySteps;

	public bool HasSideEffects => OrderedSteps.Count > 0;

	public bool WouldExecuteSideEffects => false;

	public bool MutatesTargetBeforeControllerUse =>
		IndexOf(PlayerSummonKnownObjectNpcSkillActionSideEffectStep.OwnerSetTarget) is int targetIndex
		&& targetIndex >= 0
		&& IndexOf(PlayerSummonKnownObjectNpcSkillActionSideEffectStep.CreatureControllerUseSkill) is int controllerIndex
		&& controllerIndex > targetIndex;

	public bool InvokesAfterUseSkill =>
		OrderedSteps.Contains(PlayerSummonKnownObjectNpcSkillActionSideEffectStep.NpcAiSetNoneSubState)
		&& OrderedSteps.Contains(PlayerSummonKnownObjectNpcSkillActionSideEffectStep.NpcAiOnAttackComplete);

	public bool ResetsSubState =>
		OrderedSteps.Contains(PlayerSummonKnownObjectNpcSkillActionSideEffectStep.NpcAiSetNoneSubState);

	private int IndexOf(PlayerSummonKnownObjectNpcSkillActionSideEffectStep step)
	{
		for (var i = 0; i < OrderedSteps.Count; i++)
		{
			if (OrderedSteps[i] == step)
				return i;
		}

		return -1;
	}

	public static PlayerSummonKnownObjectNpcSkillActionSideEffectTrace FromActionResult(
		PlayerSummonKnownObjectNpcSkillActionResult? actionResult)
	{
		if (actionResult == null || actionResult.Status == PlayerSummonKnownObjectNpcSkillActionResultStatus.MissingPreview)
		{
			return new PlayerSummonKnownObjectNpcSkillActionSideEffectTrace(
				PlayerSummonKnownObjectNpcSkillActionSideEffectTraceStatus.MissingResult,
				actionResult);
		}

		var steps = new List<PlayerSummonKnownObjectNpcSkillActionSideEffectStep>();
		switch (actionResult.Status)
		{
			case PlayerSummonKnownObjectNpcSkillActionResultStatus.ResumeFightAfterInterruptedCast:
				steps.Add(PlayerSummonKnownObjectNpcSkillActionSideEffectStep.NpcAiThink);
				break;
			case PlayerSummonKnownObjectNpcSkillActionResultStatus.TargetGiveUp:
				steps.Add(PlayerSummonKnownObjectNpcSkillActionSideEffectStep.NpcAiSetNoneSubState);
				steps.Add(PlayerSummonKnownObjectNpcSkillActionSideEffectStep.NpcAiOnTargetGiveUp);
				break;
			case PlayerSummonKnownObjectNpcSkillActionResultStatus.TargetTooFar:
				steps.Add(PlayerSummonKnownObjectNpcSkillActionSideEffectStep.ControllerAbortCast);
				steps.Add(PlayerSummonKnownObjectNpcSkillActionSideEffectStep.NpcAiOnTargetTooFar);
				break;
			case PlayerSummonKnownObjectNpcSkillActionResultStatus.AfterUseSkill:
				steps.Add(PlayerSummonKnownObjectNpcSkillActionSideEffectStep.NpcAiSetNoneSubState);
				steps.Add(PlayerSummonKnownObjectNpcSkillActionSideEffectStep.NpcAiOnAttackComplete);
				break;
			case PlayerSummonKnownObjectNpcSkillActionResultStatus.UseSkill:
				AddUseSkillSteps(actionResult, steps);
				break;
			case PlayerSummonKnownObjectNpcSkillActionResultStatus.UseSkillFailed:
				AddUseSkillSteps(actionResult, steps);
				steps.Add(PlayerSummonKnownObjectNpcSkillActionSideEffectStep.NpcAiSetNoneSubState);
				steps.Add(PlayerSummonKnownObjectNpcSkillActionSideEffectStep.NpcAiOnAttackComplete);
				break;
		}

		return steps.Count == 0
			? new PlayerSummonKnownObjectNpcSkillActionSideEffectTrace(
				PlayerSummonKnownObjectNpcSkillActionSideEffectTraceStatus.NoSideEffects,
				actionResult,
				steps)
			: new PlayerSummonKnownObjectNpcSkillActionSideEffectTrace(
				PlayerSummonKnownObjectNpcSkillActionSideEffectTraceStatus.OrderedSideEffects,
				actionResult,
				steps);
	}

	private static void AddUseSkillSteps(
		PlayerSummonKnownObjectNpcSkillActionResult actionResult,
		List<PlayerSummonKnownObjectNpcSkillActionSideEffectStep> steps)
	{
		if (actionResult.ShouldSetOwnerTarget)
			steps.Add(PlayerSummonKnownObjectNpcSkillActionSideEffectStep.OwnerSetTarget);

		steps.Add(PlayerSummonKnownObjectNpcSkillActionSideEffectStep.CreatureControllerUseSkill);
	}
}

public enum PlayerSummonKnownObjectNpcSkillActionSideEffectTraceStatus
{
	MissingResult,
	NoSideEffects,
	OrderedSideEffects,
}

public enum PlayerSummonKnownObjectNpcSkillActionSideEffectStep
{
	NpcAiThink,
	NpcAiSetNoneSubState,
	NpcAiOnTargetGiveUp,
	ControllerAbortCast,
	NpcAiOnTargetTooFar,
	OwnerSetTarget,
	CreatureControllerUseSkill,
	NpcAiOnAttackComplete,
}

public sealed record PlayerSummonKnownObjectNpcSkillUseSkillStartTrace(
	PlayerSummonKnownObjectNpcSkillUseSkillStartTraceStatus Status,
	PlayerSummonKnownObjectNpcSkillActionResult? ActionResult = null,
	IReadOnlyList<PlayerSummonKnownObjectNpcSkillUseSkillStartStep>? Steps = null)
{
	private static readonly IReadOnlyList<PlayerSummonKnownObjectNpcSkillUseSkillStartStep> EmptySteps = [];

	public IReadOnlyList<PlayerSummonKnownObjectNpcSkillUseSkillStartStep> OrderedSteps => Steps ?? EmptySteps;

	public bool WouldExecuteSideEffects => false;

	public bool StartsCastingBeforeNpcCastSubState =>
		IndexOf(PlayerSummonKnownObjectNpcSkillUseSkillStartStep.EffectorSetCasting) is int castingIndex
		&& castingIndex >= 0
		&& IndexOf(PlayerSummonKnownObjectNpcSkillUseSkillStartStep.NpcAiSetCastSubState) is int castSubStateIndex
		&& castSubStateIndex > castingIndex;

	public bool UpdatesNpcSkillDelayBeforeAiStartUseSkill =>
		IndexOf(PlayerSummonKnownObjectNpcSkillUseSkillStartStep.NpcGameStatsSetNextSkillDelay) is int delayIndex
		&& delayIndex >= 0
		&& IndexOf(PlayerSummonKnownObjectNpcSkillUseSkillStartStep.EffectorAiOnStartUseSkill) is int aiIndex
		&& aiIndex > delayIndex;

	public bool ReachesEndCast =>
		OrderedSteps.Contains(PlayerSummonKnownObjectNpcSkillUseSkillStartStep.InvokeEndCast)
		|| OrderedSteps.Contains(PlayerSummonKnownObjectNpcSkillUseSkillStartStep.ScheduleEndCast);

	private int IndexOf(PlayerSummonKnownObjectNpcSkillUseSkillStartStep step)
	{
		for (var i = 0; i < OrderedSteps.Count; i++)
		{
			if (OrderedSteps[i] == step)
				return i;
		}

		return -1;
	}

	public static PlayerSummonKnownObjectNpcSkillUseSkillStartTrace FromActionResult(
		PlayerSummonKnownObjectNpcSkillActionResult? actionResult,
		bool canUseSkillAtCastStart,
		bool isChargeSkill,
		bool castDurationGreaterThanZero,
		bool isNpcEffector,
		bool notifiesCastObservers)
	{
		if (actionResult == null || actionResult.Status == PlayerSummonKnownObjectNpcSkillActionResultStatus.MissingPreview)
		{
			return new PlayerSummonKnownObjectNpcSkillUseSkillStartTrace(
				PlayerSummonKnownObjectNpcSkillUseSkillStartTraceStatus.MissingResult,
				actionResult);
		}

		if (actionResult.Status != PlayerSummonKnownObjectNpcSkillActionResultStatus.UseSkill)
		{
			return new PlayerSummonKnownObjectNpcSkillUseSkillStartTrace(
				PlayerSummonKnownObjectNpcSkillUseSkillStartTraceStatus.NoUseSkill,
				actionResult);
		}

		var steps = new List<PlayerSummonKnownObjectNpcSkillUseSkillStartStep>
		{
			PlayerSummonKnownObjectNpcSkillUseSkillStartStep.ResetBoostSkillCost,
			PlayerSummonKnownObjectNpcSkillUseSkillStartStep.NotifyBoostSkillCostObservers,
			PlayerSummonKnownObjectNpcSkillUseSkillStartStep.CanUseSkillCastStart,
		};

		if (!canUseSkillAtCastStart)
		{
			return new PlayerSummonKnownObjectNpcSkillUseSkillStartTrace(
				PlayerSummonKnownObjectNpcSkillUseSkillStartTraceStatus.BlockedByCastStartValidation,
				actionResult,
				steps);
		}

		steps.Add(PlayerSummonKnownObjectNpcSkillUseSkillStartStep.UpdateCastDurationAndSpeed);
		steps.Add(PlayerSummonKnownObjectNpcSkillUseSkillStartStep.UpdateHitTime);
		if (notifiesCastObservers)
			steps.Add(PlayerSummonKnownObjectNpcSkillUseSkillStartStep.NotifyStartSkillCastObservers);
		steps.Add(PlayerSummonKnownObjectNpcSkillUseSkillStartStep.EffectorSetCasting);
		if (notifiesCastObservers)
		{
			steps.Add(PlayerSummonKnownObjectNpcSkillUseSkillStartStep.CastStartTimeSet);
			steps.Add(PlayerSummonKnownObjectNpcSkillUseSkillStartStep.StartCast);
		}
		if (isNpcEffector)
			steps.Add(PlayerSummonKnownObjectNpcSkillUseSkillStartStep.NpcAiSetCastSubState);
		steps.Add(PlayerSummonKnownObjectNpcSkillUseSkillStartStep.AttachMoveListener);
		if (isNpcEffector)
		{
			steps.Add(PlayerSummonKnownObjectNpcSkillUseSkillStartStep.NpcSkillEntrySetLastTimeUsed);
			steps.Add(PlayerSummonKnownObjectNpcSkillUseSkillStartStep.NpcGameStatsSetNextSkillDelay);
		}
		steps.Add(PlayerSummonKnownObjectNpcSkillUseSkillStartStep.EffectorAiOnStartUseSkill);
		if (isChargeSkill)
			steps.Add(PlayerSummonKnownObjectNpcSkillUseSkillStartStep.ScheduleCancelCurrentSkillCast);
		else if (castDurationGreaterThanZero)
			steps.Add(PlayerSummonKnownObjectNpcSkillUseSkillStartStep.ScheduleEndCast);
		else
			steps.Add(PlayerSummonKnownObjectNpcSkillUseSkillStartStep.InvokeEndCast);

		return new PlayerSummonKnownObjectNpcSkillUseSkillStartTrace(
			PlayerSummonKnownObjectNpcSkillUseSkillStartTraceStatus.OrderedSideEffects,
			actionResult,
			steps);
	}
}

public enum PlayerSummonKnownObjectNpcSkillUseSkillStartTraceStatus
{
	MissingResult,
	NoUseSkill,
	BlockedByCastStartValidation,
	OrderedSideEffects,
}

public enum PlayerSummonKnownObjectNpcSkillUseSkillStartStep
{
	ResetBoostSkillCost,
	NotifyBoostSkillCostObservers,
	CanUseSkillCastStart,
	UpdateCastDurationAndSpeed,
	UpdateHitTime,
	NotifyStartSkillCastObservers,
	EffectorSetCasting,
	CastStartTimeSet,
	StartCast,
	NpcAiSetCastSubState,
	AttachMoveListener,
	NpcSkillEntrySetLastTimeUsed,
	NpcGameStatsSetNextSkillDelay,
	EffectorAiOnStartUseSkill,
	ScheduleCancelCurrentSkillCast,
	ScheduleEndCast,
	InvokeEndCast,
}

public sealed record PlayerSummonKnownObjectNpcSkillValidationMutationTrace(
	PlayerSummonKnownObjectNpcSkillValidationMutationTraceStatus Status,
	PlayerSummonKnownObjectNpcSkillActionResult? ActionResult = null,
	IReadOnlyList<PlayerSummonKnownObjectNpcSkillValidationMutationStep>? Steps = null)
{
	private static readonly IReadOnlyList<PlayerSummonKnownObjectNpcSkillValidationMutationStep> EmptySteps = [];

	public IReadOnlyList<PlayerSummonKnownObjectNpcSkillValidationMutationStep> OrderedSteps => Steps ?? EmptySteps;

	public bool WouldExecuteMutations => false;

	public bool RebuildsEffectedListBeforePropertyFilters =>
		IndexOf(PlayerSummonKnownObjectNpcSkillValidationMutationStep.EndCastClearEffectedList) is int clearIndex
		&& clearIndex >= 0
		&& IndexOf(PlayerSummonKnownObjectNpcSkillValidationMutationStep.TargetRangeProperty) is int filterIndex
		&& filterIndex > clearIndex;

	public bool SetsFirstTargetAfterPropertyFilters =>
		IndexOf(PlayerSummonKnownObjectNpcSkillValidationMutationStep.SkillSetFirstTargetFromValidationResult) is int setIndex
		&& setIndex >= 0
		&& IndexOf(PlayerSummonKnownObjectNpcSkillValidationMutationStep.TargetRelationProperty) is int relationIndex
		&& setIndex > relationIndex;

	public bool CanRejectEmptyNonAreaTarget =>
		OrderedSteps.Contains(PlayerSummonKnownObjectNpcSkillValidationMutationStep.TargetTypeZeroEmptyNonAreaCheck);

	private int IndexOf(PlayerSummonKnownObjectNpcSkillValidationMutationStep step)
	{
		for (var i = 0; i < OrderedSteps.Count; i++)
		{
			if (OrderedSteps[i] == step)
				return i;
		}

		return -1;
	}

	public static PlayerSummonKnownObjectNpcSkillValidationMutationTrace FromActionResult(
		PlayerSummonKnownObjectNpcSkillActionResult? actionResult,
		bool hasProperties,
		bool isEndCastValidation,
		bool hasFirstTargetRange,
		bool firstTargetRangePasses,
		bool hasTargetType,
		bool targetRangePasses,
		bool targetRangeMayAppendTargets,
		bool hasTargetRelation,
		bool targetRelationPasses,
		bool targetRelationMayReplaceFirstTarget,
		bool hasTargetStatus,
		bool targetStatusPasses,
		bool hasTargetSpecies,
		bool targetSpeciesPasses,
		bool hasMaxCount,
		bool maxCountPasses,
		bool isPlayerEffector,
		bool playerCanUseSkill,
		bool playerTargetFilterKeepsAny,
		bool targetTypeZero,
		bool isAreaTarget)
	{
		if (actionResult == null || actionResult.Status == PlayerSummonKnownObjectNpcSkillActionResultStatus.MissingPreview)
		{
			return new PlayerSummonKnownObjectNpcSkillValidationMutationTrace(
				PlayerSummonKnownObjectNpcSkillValidationMutationTraceStatus.MissingResult,
				actionResult);
		}

		if (actionResult.Status != PlayerSummonKnownObjectNpcSkillActionResultStatus.UseSkill)
		{
			return new PlayerSummonKnownObjectNpcSkillValidationMutationTrace(
				PlayerSummonKnownObjectNpcSkillValidationMutationTraceStatus.NoEndCastValidation,
				actionResult);
		}

		var steps = new List<PlayerSummonKnownObjectNpcSkillValidationMutationStep>();
		if (hasProperties)
		{
			if (isEndCastValidation)
			{
				steps.Add(PlayerSummonKnownObjectNpcSkillValidationMutationStep.EndCastClearEffectedList);
				steps.Add(PlayerSummonKnownObjectNpcSkillValidationMutationStep.EndCastSeedFirstTarget);
			}
			else
			{
				steps.Add(PlayerSummonKnownObjectNpcSkillValidationMutationStep.CastStartFirstTargetProperty);
				steps.Add(PlayerSummonKnownObjectNpcSkillValidationMutationStep.CastStartFirstTargetMayAddEffected);
			}

			if (hasFirstTargetRange)
			{
				steps.Add(PlayerSummonKnownObjectNpcSkillValidationMutationStep.FirstTargetRangeProperty);
				if (!firstTargetRangePasses)
				{
					return new PlayerSummonKnownObjectNpcSkillValidationMutationTrace(
						PlayerSummonKnownObjectNpcSkillValidationMutationTraceStatus.BlockedByFirstTargetRange,
						actionResult,
						steps);
				}
			}

			if (hasTargetType)
			{
				steps.Add(PlayerSummonKnownObjectNpcSkillValidationMutationStep.TargetRangeProperty);
				if (targetRangeMayAppendTargets)
					steps.Add(PlayerSummonKnownObjectNpcSkillValidationMutationStep.TargetRangeMayAddOrClearTargets);
				if (!targetRangePasses)
				{
					return new PlayerSummonKnownObjectNpcSkillValidationMutationTrace(
						PlayerSummonKnownObjectNpcSkillValidationMutationTraceStatus.BlockedByTargetRangeProperty,
						actionResult,
						steps);
				}
			}

			if (hasTargetRelation)
			{
				steps.Add(PlayerSummonKnownObjectNpcSkillValidationMutationStep.TargetRelationProperty);
				if (targetRelationMayReplaceFirstTarget)
					steps.Add(PlayerSummonKnownObjectNpcSkillValidationMutationStep.TargetRelationMayReplaceFirstTarget);
				if (!targetRelationPasses)
				{
					return new PlayerSummonKnownObjectNpcSkillValidationMutationTrace(
						PlayerSummonKnownObjectNpcSkillValidationMutationTraceStatus.BlockedByTargetRelationProperty,
						actionResult,
						steps);
				}
			}

			if (hasTargetStatus)
			{
				steps.Add(PlayerSummonKnownObjectNpcSkillValidationMutationStep.TargetStatusFilter);
				if (!targetStatusPasses)
				{
					return new PlayerSummonKnownObjectNpcSkillValidationMutationTrace(
						PlayerSummonKnownObjectNpcSkillValidationMutationTraceStatus.BlockedByTargetStatusProperty,
						actionResult,
						steps);
				}
			}

			if (hasTargetSpecies)
			{
				steps.Add(PlayerSummonKnownObjectNpcSkillValidationMutationStep.TargetSpeciesFilter);
				if (!targetSpeciesPasses)
				{
					return new PlayerSummonKnownObjectNpcSkillValidationMutationTrace(
						PlayerSummonKnownObjectNpcSkillValidationMutationTraceStatus.BlockedByTargetSpeciesProperty,
						actionResult,
						steps);
				}
			}

			if (hasMaxCount)
			{
				steps.Add(PlayerSummonKnownObjectNpcSkillValidationMutationStep.MaxCountRetainNearest);
				if (!maxCountPasses)
				{
					return new PlayerSummonKnownObjectNpcSkillValidationMutationTrace(
						PlayerSummonKnownObjectNpcSkillValidationMutationTraceStatus.BlockedByMaxCountProperty,
						actionResult,
						steps);
				}
			}

			steps.Add(PlayerSummonKnownObjectNpcSkillValidationMutationStep.SkillSetFirstTargetFromValidationResult);
		}

		if (isPlayerEffector)
		{
			steps.Add(PlayerSummonKnownObjectNpcSkillValidationMutationStep.PlayerCanUseSkillGate);
			if (!playerCanUseSkill)
			{
				steps.Add(PlayerSummonKnownObjectNpcSkillValidationMutationStep.PlayerClearEffectedList);
				return new PlayerSummonKnownObjectNpcSkillValidationMutationTrace(
					PlayerSummonKnownObjectNpcSkillValidationMutationTraceStatus.PlayerUsabilityClearedTargets,
					actionResult,
					steps);
			}

			steps.Add(PlayerSummonKnownObjectNpcSkillValidationMutationStep.PlayerInvalidTargetFilter);
			if (!playerTargetFilterKeepsAny)
				steps.Add(PlayerSummonKnownObjectNpcSkillValidationMutationStep.PlayerFilterEmptiedTargets);
		}

		if (targetTypeZero)
		{
			steps.Add(PlayerSummonKnownObjectNpcSkillValidationMutationStep.TargetTypeZeroEmptyNonAreaCheck);
			if (!isAreaTarget && !playerTargetFilterKeepsAny)
			{
				steps.Add(PlayerSummonKnownObjectNpcSkillValidationMutationStep.SendInvalidTargetMessage);
				return new PlayerSummonKnownObjectNpcSkillValidationMutationTrace(
					PlayerSummonKnownObjectNpcSkillValidationMutationTraceStatus.BlockedByEmptyNonAreaTarget,
					actionResult,
					steps);
			}
		}

		return new PlayerSummonKnownObjectNpcSkillValidationMutationTrace(
			PlayerSummonKnownObjectNpcSkillValidationMutationTraceStatus.OrderedMutations,
			actionResult,
			steps);
	}
}

public enum PlayerSummonKnownObjectNpcSkillValidationMutationTraceStatus
{
	MissingResult,
	NoEndCastValidation,
	BlockedByFirstTargetRange,
	BlockedByTargetRangeProperty,
	BlockedByTargetRelationProperty,
	BlockedByTargetStatusProperty,
	BlockedByTargetSpeciesProperty,
	BlockedByMaxCountProperty,
	PlayerUsabilityClearedTargets,
	BlockedByEmptyNonAreaTarget,
	OrderedMutations,
}

public enum PlayerSummonKnownObjectNpcSkillValidationMutationStep
{
	CastStartFirstTargetProperty,
	CastStartFirstTargetMayAddEffected,
	EndCastClearEffectedList,
	EndCastSeedFirstTarget,
	FirstTargetRangeProperty,
	TargetRangeProperty,
	TargetRangeMayAddOrClearTargets,
	TargetRelationProperty,
	TargetRelationMayReplaceFirstTarget,
	TargetStatusFilter,
	TargetSpeciesFilter,
	MaxCountRetainNearest,
	SkillSetFirstTargetFromValidationResult,
	PlayerCanUseSkillGate,
	PlayerClearEffectedList,
	PlayerInvalidTargetFilter,
	PlayerFilterEmptiedTargets,
	TargetTypeZeroEmptyNonAreaCheck,
	SendInvalidTargetMessage,
}

public sealed record PlayerSummonKnownObjectNpcSkillTemplateActionTrace(
	PlayerSummonKnownObjectNpcSkillTemplateActionTraceStatus Status,
	PlayerSummonKnownObjectNpcSkillActionResult? ActionResult = null,
	IReadOnlyList<PlayerSummonKnownObjectNpcSkillTemplateActionStep>? Steps = null)
{
	private static readonly IReadOnlyList<PlayerSummonKnownObjectNpcSkillTemplateActionStep> EmptySteps = [];

	public IReadOnlyList<PlayerSummonKnownObjectNpcSkillTemplateActionStep> OrderedSteps => Steps ?? EmptySteps;

	public bool WouldExecuteActions => false;

	public bool ShortCircuitsBeforeEffects =>
		Status == PlayerSummonKnownObjectNpcSkillTemplateActionTraceStatus.ActionShortCircuited;

	public bool ConsumesResourceBeforeEffects =>
		OrderedSteps.Contains(PlayerSummonKnownObjectNpcSkillTemplateActionStep.ReduceMp)
		|| OrderedSteps.Contains(PlayerSummonKnownObjectNpcSkillTemplateActionStep.ReduceHp)
		|| OrderedSteps.Contains(PlayerSummonKnownObjectNpcSkillTemplateActionStep.SetDp)
		|| OrderedSteps.Contains(PlayerSummonKnownObjectNpcSkillTemplateActionStep.DecreaseInventoryByItemId);

	public static PlayerSummonKnownObjectNpcSkillTemplateActionTrace FromActionResult(
		PlayerSummonKnownObjectNpcSkillActionResult? actionResult,
		IReadOnlyList<PlayerSummonKnownObjectNpcSkillTemplateActionKind>? actionOrder,
		PlayerSummonKnownObjectNpcSkillTemplateActionKind? failingAction,
		bool effectorIsPlayer,
		bool mpRatio,
		bool hpRatio,
		bool boostSkillCostPresent)
	{
		if (actionResult == null || actionResult.Status == PlayerSummonKnownObjectNpcSkillActionResultStatus.MissingPreview)
		{
			return new PlayerSummonKnownObjectNpcSkillTemplateActionTrace(
				PlayerSummonKnownObjectNpcSkillTemplateActionTraceStatus.MissingResult,
				actionResult);
		}

		if (actionResult.Status != PlayerSummonKnownObjectNpcSkillActionResultStatus.UseSkill)
		{
			return new PlayerSummonKnownObjectNpcSkillTemplateActionTrace(
				PlayerSummonKnownObjectNpcSkillTemplateActionTraceStatus.NoEndCastActions,
				actionResult);
		}

		if (actionOrder == null || actionOrder.Count == 0)
		{
			return new PlayerSummonKnownObjectNpcSkillTemplateActionTrace(
				PlayerSummonKnownObjectNpcSkillTemplateActionTraceStatus.NoActions,
				actionResult,
				[]);
		}

		var steps = new List<PlayerSummonKnownObjectNpcSkillTemplateActionStep>
		{
			PlayerSummonKnownObjectNpcSkillTemplateActionStep.ActionsLiveListOrder,
		};

		foreach (var action in actionOrder)
		{
			AddActionSteps(action, steps, effectorIsPlayer, mpRatio, hpRatio, boostSkillCostPresent);
			if (failingAction == action)
			{
				AddFailureStep(action, steps);
				return new PlayerSummonKnownObjectNpcSkillTemplateActionTrace(
					PlayerSummonKnownObjectNpcSkillTemplateActionTraceStatus.ActionShortCircuited,
					actionResult,
					steps);
			}

			AddSuccessStep(action, steps, effectorIsPlayer);
		}

		return new PlayerSummonKnownObjectNpcSkillTemplateActionTrace(
			PlayerSummonKnownObjectNpcSkillTemplateActionTraceStatus.OrderedActions,
			actionResult,
			steps);
	}

	private static void AddActionSteps(
		PlayerSummonKnownObjectNpcSkillTemplateActionKind action,
		List<PlayerSummonKnownObjectNpcSkillTemplateActionStep> steps,
		bool effectorIsPlayer,
		bool mpRatio,
		bool hpRatio,
		bool boostSkillCostPresent)
	{
		switch (action)
		{
			case PlayerSummonKnownObjectNpcSkillTemplateActionKind.MpUse:
				steps.Add(PlayerSummonKnownObjectNpcSkillTemplateActionStep.ExecuteMpUseAction);
				steps.Add(PlayerSummonKnownObjectNpcSkillTemplateActionStep.CalculateMpCostWithDelta);
				if (mpRatio)
					steps.Add(PlayerSummonKnownObjectNpcSkillTemplateActionStep.ApplyMpRatioCost);
				if (boostSkillCostPresent)
					steps.Add(PlayerSummonKnownObjectNpcSkillTemplateActionStep.ApplyBoostSkillCost);
				break;
			case PlayerSummonKnownObjectNpcSkillTemplateActionKind.HpUse:
				steps.Add(PlayerSummonKnownObjectNpcSkillTemplateActionStep.ExecuteHpUseAction);
				steps.Add(PlayerSummonKnownObjectNpcSkillTemplateActionStep.CalculateHpCostWithDelta);
				if (hpRatio)
					steps.Add(PlayerSummonKnownObjectNpcSkillTemplateActionStep.ApplyHpRatioCost);
				break;
			case PlayerSummonKnownObjectNpcSkillTemplateActionKind.DpUse:
				steps.Add(PlayerSummonKnownObjectNpcSkillTemplateActionStep.ExecuteDpUseAction);
				if (!effectorIsPlayer)
					steps.Add(PlayerSummonKnownObjectNpcSkillTemplateActionStep.CastEffectorToPlayer);
				break;
			case PlayerSummonKnownObjectNpcSkillTemplateActionKind.ItemUse:
				steps.Add(PlayerSummonKnownObjectNpcSkillTemplateActionStep.ExecuteItemUseAction);
				if (effectorIsPlayer)
				{
					steps.Add(PlayerSummonKnownObjectNpcSkillTemplateActionStep.LookupItemTemplate);
					steps.Add(PlayerSummonKnownObjectNpcSkillTemplateActionStep.DecreaseInventoryByItemId);
				}
				else
				{
					steps.Add(PlayerSummonKnownObjectNpcSkillTemplateActionStep.SkipItemUseForNonPlayer);
				}
				break;
		}
	}

	private static void AddFailureStep(
		PlayerSummonKnownObjectNpcSkillTemplateActionKind action,
		List<PlayerSummonKnownObjectNpcSkillTemplateActionStep> steps)
	{
		if (action == PlayerSummonKnownObjectNpcSkillTemplateActionKind.ItemUse)
			steps.Add(PlayerSummonKnownObjectNpcSkillTemplateActionStep.InventoryMayPartiallyConsumeBeforeFalse);

		steps.Add(action switch
		{
			PlayerSummonKnownObjectNpcSkillTemplateActionKind.MpUse => PlayerSummonKnownObjectNpcSkillTemplateActionStep.SendNotEnoughMp,
			PlayerSummonKnownObjectNpcSkillTemplateActionKind.HpUse => PlayerSummonKnownObjectNpcSkillTemplateActionStep.SendNotEnoughHp,
			PlayerSummonKnownObjectNpcSkillTemplateActionKind.DpUse => PlayerSummonKnownObjectNpcSkillTemplateActionStep.SendNotEnoughDp,
			PlayerSummonKnownObjectNpcSkillTemplateActionKind.ItemUse => PlayerSummonKnownObjectNpcSkillTemplateActionStep.SendNotEnoughItem,
			_ => PlayerSummonKnownObjectNpcSkillTemplateActionStep.ActionReturnedFalse,
		});
		steps.Add(PlayerSummonKnownObjectNpcSkillTemplateActionStep.ActionReturnedFalse);
	}

	private static void AddSuccessStep(
		PlayerSummonKnownObjectNpcSkillTemplateActionKind action,
		List<PlayerSummonKnownObjectNpcSkillTemplateActionStep> steps,
		bool effectorIsPlayer)
	{
		switch (action)
		{
			case PlayerSummonKnownObjectNpcSkillTemplateActionKind.MpUse:
				steps.Add(PlayerSummonKnownObjectNpcSkillTemplateActionStep.ReduceMp);
				break;
			case PlayerSummonKnownObjectNpcSkillTemplateActionKind.HpUse:
				steps.Add(PlayerSummonKnownObjectNpcSkillTemplateActionStep.ReduceHp);
				break;
			case PlayerSummonKnownObjectNpcSkillTemplateActionKind.DpUse:
				if (effectorIsPlayer)
					steps.Add(PlayerSummonKnownObjectNpcSkillTemplateActionStep.SetDp);
				break;
			case PlayerSummonKnownObjectNpcSkillTemplateActionKind.ItemUse when effectorIsPlayer:
				steps.Add(PlayerSummonKnownObjectNpcSkillTemplateActionStep.ItemUseSucceeded);
				break;
		}
	}
}

public enum PlayerSummonKnownObjectNpcSkillTemplateActionTraceStatus
{
	MissingResult,
	NoEndCastActions,
	NoActions,
	ActionShortCircuited,
	OrderedActions,
}

public enum PlayerSummonKnownObjectNpcSkillTemplateActionKind
{
	MpUse,
	HpUse,
	DpUse,
	ItemUse,
}

public enum PlayerSummonKnownObjectNpcSkillTemplateActionStep
{
	ActionsLiveListOrder,
	ExecuteMpUseAction,
	CalculateMpCostWithDelta,
	ApplyMpRatioCost,
	ApplyBoostSkillCost,
	SendNotEnoughMp,
	ReduceMp,
	ExecuteHpUseAction,
	CalculateHpCostWithDelta,
	ApplyHpRatioCost,
	SendNotEnoughHp,
	ReduceHp,
	ExecuteDpUseAction,
	CastEffectorToPlayer,
	SendNotEnoughDp,
	SetDp,
	ExecuteItemUseAction,
	LookupItemTemplate,
	DecreaseInventoryByItemId,
	InventoryMayPartiallyConsumeBeforeFalse,
	SendNotEnoughItem,
	ItemUseSucceeded,
	SkipItemUseForNonPlayer,
	ActionReturnedFalse,
}

public sealed record PlayerSummonKnownObjectNpcSkillEffectInitializationTrace(
	PlayerSummonKnownObjectNpcSkillEffectInitializationTraceStatus Status,
	PlayerSummonKnownObjectNpcSkillActionResult? ActionResult = null,
	IReadOnlyList<PlayerSummonKnownObjectNpcSkillEffectInitializationStep>? Steps = null)
{
	private static readonly IReadOnlyList<PlayerSummonKnownObjectNpcSkillEffectInitializationStep> EmptySteps = [];

	public IReadOnlyList<PlayerSummonKnownObjectNpcSkillEffectInitializationStep> OrderedSteps => Steps ?? EmptySteps;

	public bool WouldExecuteEffects => false;

	public bool CapturesDashStatusBeforeCastResult =>
		OrderedSteps.Contains(PlayerSummonKnownObjectNpcSkillEffectInitializationStep.CaptureFirstTargetDashStatus);

	public bool BlocksPenaltyAfterFullResistOrDodge =>
		OrderedSteps.Contains(PlayerSummonKnownObjectNpcSkillEffectInitializationStep.MarkBlockedPenaltySkill);

	public bool NotifiesSupportAfterApply =>
		IndexOf(PlayerSummonKnownObjectNpcSkillEffectInitializationStep.ApplyEachEffect) is int applyIndex
		&& applyIndex >= 0
		&& IndexOf(PlayerSummonKnownObjectNpcSkillEffectInitializationStep.NotifyKnownNpcsCreatureNeedsSupport) is int notifyIndex
		&& notifyIndex > applyIndex;

	private int IndexOf(PlayerSummonKnownObjectNpcSkillEffectInitializationStep step)
	{
		for (var i = 0; i < OrderedSteps.Count; i++)
		{
			if (OrderedSteps[i] == step)
				return i;
		}

		return -1;
	}

	public static PlayerSummonKnownObjectNpcSkillEffectInitializationTrace FromActionResult(
		PlayerSummonKnownObjectNpcSkillActionResult? actionResult,
		bool hasSkillEffects,
		int effectedCount,
		bool isPointPointSkill,
		bool hasConflictOnPlayerTarget,
		bool allEffectsResistedOrDodged,
		bool firstTargetReceivesDashStatus,
		bool successEffectsEmpty,
		bool emptySuccessFromPhysicalEffect,
		bool launchesSubEffect,
		bool mayCreateCriticalSubEffect,
		bool isInstantSkill,
		bool hasResistedTauntHate)
	{
		if (actionResult == null || actionResult.Status == PlayerSummonKnownObjectNpcSkillActionResultStatus.MissingPreview)
		{
			return new PlayerSummonKnownObjectNpcSkillEffectInitializationTrace(
				PlayerSummonKnownObjectNpcSkillEffectInitializationTraceStatus.MissingResult,
				actionResult);
		}

		if (actionResult.Status != PlayerSummonKnownObjectNpcSkillActionResultStatus.UseSkill)
		{
			return new PlayerSummonKnownObjectNpcSkillEffectInitializationTrace(
				PlayerSummonKnownObjectNpcSkillEffectInitializationTraceStatus.NoEndCast,
				actionResult);
		}

		if (!hasSkillEffects)
		{
			return new PlayerSummonKnownObjectNpcSkillEffectInitializationTrace(
				PlayerSummonKnownObjectNpcSkillEffectInitializationTraceStatus.NoSkillEffects,
				actionResult,
				[]);
		}

		var steps = new List<PlayerSummonKnownObjectNpcSkillEffectInitializationStep>();
		var blocksChainAndPenalty = allEffectsResistedOrDodged || effectedCount == 0;
		if (effectedCount > 0)
		{
			for (var i = 0; i < effectedCount; i++)
				AddPerTargetEffectSetupSteps(steps, hasConflictOnPlayerTarget, successEffectsEmpty, emptySuccessFromPhysicalEffect, launchesSubEffect, mayCreateCriticalSubEffect);

			if (firstTargetReceivesDashStatus)
				steps.Add(PlayerSummonKnownObjectNpcSkillEffectInitializationStep.CaptureFirstTargetDashStatus);
			if (allEffectsResistedOrDodged)
				steps.Add(PlayerSummonKnownObjectNpcSkillEffectInitializationStep.CountExactResistOrDodge);
		}
		else if (isPointPointSkill)
		{
			steps.Add(PlayerSummonKnownObjectNpcSkillEffectInitializationStep.EmptyEffectedListCountsAsAllResistedOrDodged);
			steps.Add(PlayerSummonKnownObjectNpcSkillEffectInitializationStep.CreatePointPointNullEffect);
			steps.Add(PlayerSummonKnownObjectNpcSkillEffectInitializationStep.InitializePointPointNullEffect);
			steps.Add(PlayerSummonKnownObjectNpcSkillEffectInitializationStep.SetPointPointWorldPosition);
			steps.Add(PlayerSummonKnownObjectNpcSkillEffectInitializationStep.AddPointPointEffectToList);
		}
		else
		{
			steps.Add(PlayerSummonKnownObjectNpcSkillEffectInitializationStep.EmptyEffectedListCountsAsAllResistedOrDodged);
		}

		if (blocksChainAndPenalty)
		{
			steps.Add(PlayerSummonKnownObjectNpcSkillEffectInitializationStep.MarkBlockedChain);
			steps.Add(PlayerSummonKnownObjectNpcSkillEffectInitializationStep.MarkBlockedPenaltySkill);
		}

		steps.Add(isInstantSkill
			? PlayerSummonKnownObjectNpcSkillEffectInitializationStep.ApplyEffectImmediately
			: PlayerSummonKnownObjectNpcSkillEffectInitializationStep.ScheduleApplyEffect);
		steps.Add(PlayerSummonKnownObjectNpcSkillEffectInitializationStep.ApplyEachEffect);
		if (successEffectsEmpty)
			steps.Add(PlayerSummonKnownObjectNpcSkillEffectInitializationStep.BroadcastHateWhenNoSuccessEffects);
		else
		{
			steps.Add(PlayerSummonKnownObjectNpcSkillEffectInitializationStep.ApplySuccessEffectTemplates);
			if (mayCreateCriticalSubEffect)
				steps.Add(PlayerSummonKnownObjectNpcSkillEffectInitializationStep.ApplyCriticalSubEffect);
			steps.Add(PlayerSummonKnownObjectNpcSkillEffectInitializationStep.NotifyEffectedAiOnEffectApplied);
		}

		steps.Add(PlayerSummonKnownObjectNpcSkillEffectInitializationStep.AddResistedEffectHateAndNotifyFriends);
		if (hasResistedTauntHate)
		{
			steps.Add(PlayerSummonKnownObjectNpcSkillEffectInitializationStep.AddHateForResistedEffect);
			steps.Add(PlayerSummonKnownObjectNpcSkillEffectInitializationStep.NotifyKnownNpcsCreatureNeedsSupport);
		}

		return new PlayerSummonKnownObjectNpcSkillEffectInitializationTrace(
			PlayerSummonKnownObjectNpcSkillEffectInitializationTraceStatus.OrderedEffectSetup,
			actionResult,
			steps);
	}

	private static void AddPerTargetEffectSetupSteps(
		List<PlayerSummonKnownObjectNpcSkillEffectInitializationStep> steps,
		bool hasConflictOnPlayerTarget,
		bool successEffectsEmpty,
		bool emptySuccessFromPhysicalEffect,
		bool launchesSubEffect,
		bool mayCreateCriticalSubEffect)
	{
		steps.Add(PlayerSummonKnownObjectNpcSkillEffectInitializationStep.CreateEffectForEffected);
		steps.Add(PlayerSummonKnownObjectNpcSkillEffectInitializationStep.InitializeEffect);
		steps.Add(PlayerSummonKnownObjectNpcSkillEffectInitializationStep.CheckEffectControllerConflict);
		if (hasConflictOnPlayerTarget)
		{
			steps.Add(PlayerSummonKnownObjectNpcSkillEffectInitializationStep.SetEffectResultConflict);
			steps.Add(PlayerSummonKnownObjectNpcSkillEffectInitializationStep.MarkBlockedStance);
		}
		else
		{
			steps.Add(PlayerSummonKnownObjectNpcSkillEffectInitializationStep.CalculateEffectTemplates);
		}

		if (successEffectsEmpty)
		{
			steps.Add(PlayerSummonKnownObjectNpcSkillEffectInitializationStep.ClearSuccessEffects);
			steps.Add(emptySuccessFromPhysicalEffect
				? PlayerSummonKnownObjectNpcSkillEffectInitializationStep.ResolveEmptySuccessAsDodge
				: PlayerSummonKnownObjectNpcSkillEffectInitializationStep.ResolveEmptySuccessAsResist);
		}
		else
		{
			steps.Add(PlayerSummonKnownObjectNpcSkillEffectInitializationStep.CalculateHateForSuccessEffects);
			if (launchesSubEffect)
				steps.Add(PlayerSummonKnownObjectNpcSkillEffectInitializationStep.CalculateSubEffects);
			if (mayCreateCriticalSubEffect)
				steps.Add(PlayerSummonKnownObjectNpcSkillEffectInitializationStep.MaybeCreateCriticalSubEffect);
		}

		steps.Add(PlayerSummonKnownObjectNpcSkillEffectInitializationStep.SetSpellStatusFromBaseAttackStatus);
		steps.Add(PlayerSummonKnownObjectNpcSkillEffectInitializationStep.SetEffectWorldPosition);
		steps.Add(PlayerSummonKnownObjectNpcSkillEffectInitializationStep.AddEffectToList);
	}
}

public enum PlayerSummonKnownObjectNpcSkillEffectInitializationTraceStatus
{
	MissingResult,
	NoEndCast,
	NoSkillEffects,
	OrderedEffectSetup,
}

public enum PlayerSummonKnownObjectNpcSkillEffectInitializationStep
{
	CreateEffectForEffected,
	InitializeEffect,
	CheckEffectControllerConflict,
	SetEffectResultConflict,
	CalculateEffectTemplates,
	ClearSuccessEffects,
	CalculateHateForSuccessEffects,
	CalculateSubEffects,
	MaybeCreateCriticalSubEffect,
	ResolveEmptySuccessAsDodge,
	ResolveEmptySuccessAsResist,
	SetSpellStatusFromBaseAttackStatus,
	MarkBlockedStance,
	SetEffectWorldPosition,
	AddEffectToList,
	CaptureFirstTargetDashStatus,
	CountExactResistOrDodge,
	EmptyEffectedListCountsAsAllResistedOrDodged,
	MarkBlockedChain,
	MarkBlockedPenaltySkill,
	CreatePointPointNullEffect,
	InitializePointPointNullEffect,
	SetPointPointWorldPosition,
	AddPointPointEffectToList,
	ApplyEffectImmediately,
	ScheduleApplyEffect,
	ApplyEachEffect,
	BroadcastHateWhenNoSuccessEffects,
	ApplySuccessEffectTemplates,
	ApplyCriticalSubEffect,
	NotifyEffectedAiOnEffectApplied,
	AddResistedEffectHateAndNotifyFriends,
	AddHateForResistedEffect,
	NotifyKnownNpcsCreatureNeedsSupport,
}

public sealed record PlayerSummonKnownObjectNpcSkillCastResultPacketTrace(
	PlayerSummonKnownObjectNpcSkillCastResultPacketTraceStatus Status,
	PlayerSummonKnownObjectNpcSkillActionResult? ActionResult = null,
	IReadOnlyList<PlayerSummonKnownObjectNpcSkillCastResultPacketStep>? Steps = null)
{
	private static readonly IReadOnlyList<PlayerSummonKnownObjectNpcSkillCastResultPacketStep> EmptySteps = [];

	public IReadOnlyList<PlayerSummonKnownObjectNpcSkillCastResultPacketStep> OrderedSteps => Steps ?? EmptySteps;

	public bool WouldSerializePackets => false;

	public bool BroadcastsCastSpellResult =>
		OrderedSteps.Contains(PlayerSummonKnownObjectNpcSkillCastResultPacketStep.BroadcastSmCastspellResult);

	public bool SendsItemUseMessageAfterResult =>
		IndexOf(PlayerSummonKnownObjectNpcSkillCastResultPacketStep.BroadcastSmCastspellResult) is int packetIndex
		&& packetIndex >= 0
		&& IndexOf(PlayerSummonKnownObjectNpcSkillCastResultPacketStep.SendUseItemSystemMessage) is int itemMessageIndex
		&& itemMessageIndex > packetIndex;

	public bool WritesEffectResultBeforeAttackStatus =>
		IndexOf(PlayerSummonKnownObjectNpcSkillCastResultPacketStep.WriteEffectResultId) is int effectResultIndex
		&& effectResultIndex >= 0
		&& IndexOf(PlayerSummonKnownObjectNpcSkillCastResultPacketStep.WriteAttackStatusId) is int attackStatusIndex
		&& attackStatusIndex > effectResultIndex;

	private int IndexOf(PlayerSummonKnownObjectNpcSkillCastResultPacketStep step)
	{
		for (var i = 0; i < OrderedSteps.Count; i++)
		{
			if (OrderedSteps[i] == step)
				return i;
		}

		return -1;
	}

	public static PlayerSummonKnownObjectNpcSkillCastResultPacketTrace FromActionResult(
		PlayerSummonKnownObjectNpcSkillActionResult? actionResult,
		bool skillMethodSendsCastSpellEnd,
		bool hasItemTemplate,
		bool itemTemplateIsCombatActivated,
		bool skillSubTypeIsAttack,
		int targetType,
		int effectsCount,
		bool chainSuccess,
		bool dashStatusWritesLocation,
		bool isPenaltySkillMethod,
		bool isItemSkillMethod,
		bool effectorIsPlayer,
		bool effectHasOriginalTarget,
		bool spellStatusWritesLocation,
		bool subEffectWritesLocation,
		bool hasReservedEffects,
		PlayerSummonKnownObjectNpcSkillCastResultShieldBranch shieldBranch)
	{
		if (actionResult == null || actionResult.Status == PlayerSummonKnownObjectNpcSkillActionResultStatus.MissingPreview)
		{
			return new PlayerSummonKnownObjectNpcSkillCastResultPacketTrace(
				PlayerSummonKnownObjectNpcSkillCastResultPacketTraceStatus.MissingResult,
				actionResult);
		}

		if (actionResult.Status != PlayerSummonKnownObjectNpcSkillActionResultStatus.UseSkill || !skillMethodSendsCastSpellEnd)
		{
			return new PlayerSummonKnownObjectNpcSkillCastResultPacketTrace(
				PlayerSummonKnownObjectNpcSkillCastResultPacketTraceStatus.NoCastResult,
				actionResult);
		}

		var steps = new List<PlayerSummonKnownObjectNpcSkillCastResultPacketStep>();
		if (hasItemTemplate && !itemTemplateIsCombatActivated)
		{
			steps.Add(PlayerSummonKnownObjectNpcSkillCastResultPacketStep.CheckNonCombatItemTemplate);
			steps.Add(PlayerSummonKnownObjectNpcSkillCastResultPacketStep.ConstructSmItemUsageAnimation);
			steps.Add(PlayerSummonKnownObjectNpcSkillCastResultPacketStep.BroadcastSmItemUsageAnimationAndReceive);
			AddOptionalItemUseSystemMessage(steps, isItemSkillMethod, effectorIsPlayer);

			return new PlayerSummonKnownObjectNpcSkillCastResultPacketTrace(
				PlayerSummonKnownObjectNpcSkillCastResultPacketTraceStatus.ItemUsageAnimationOnly,
				actionResult,
				steps);
		}

		if (targetType is not 0 and not 1 and not 3)
		{
			AddOptionalItemUseSystemMessage(steps, isItemSkillMethod, effectorIsPlayer);
			return new PlayerSummonKnownObjectNpcSkillCastResultPacketTrace(
				PlayerSummonKnownObjectNpcSkillCastResultPacketTraceStatus.UnsupportedTargetType,
				actionResult,
				steps);
		}

		if (skillSubTypeIsAttack)
			steps.Add(PlayerSummonKnownObjectNpcSkillCastResultPacketStep.ResolveCreatureNeedsHelpAiEvent);

		steps.Add(targetType == 1
			? PlayerSummonKnownObjectNpcSkillCastResultPacketStep.SelectXyzTargetBranch
			: PlayerSummonKnownObjectNpcSkillCastResultPacketStep.SelectObjectTargetBranch);
		steps.Add(PlayerSummonKnownObjectNpcSkillCastResultPacketStep.ConstructSmCastspellResult);
		steps.Add(PlayerSummonKnownObjectNpcSkillCastResultPacketStep.BroadcastSmCastspellResult);
		steps.Add(PlayerSummonKnownObjectNpcSkillCastResultPacketStep.MarkCastSpellResultSent);
		AddSmCastspellResultWriteSteps(
			steps,
			targetType,
			effectsCount,
			chainSuccess,
			hasItemTemplate && itemTemplateIsCombatActivated,
			dashStatusWritesLocation,
			isPenaltySkillMethod,
			effectHasOriginalTarget,
			spellStatusWritesLocation,
			subEffectWritesLocation,
			hasReservedEffects,
			shieldBranch);
		AddOptionalItemUseSystemMessage(steps, isItemSkillMethod, effectorIsPlayer);

		return new PlayerSummonKnownObjectNpcSkillCastResultPacketTrace(
			PlayerSummonKnownObjectNpcSkillCastResultPacketTraceStatus.OrderedPacketFanout,
			actionResult,
			steps);
	}

	private static void AddSmCastspellResultWriteSteps(
		List<PlayerSummonKnownObjectNpcSkillCastResultPacketStep> steps,
		int targetType,
		int effectsCount,
		bool chainSuccess,
		bool combatActivatedItem,
		bool dashStatusWritesLocation,
		bool isPenaltySkillMethod,
		bool effectHasOriginalTarget,
		bool spellStatusWritesLocation,
		bool subEffectWritesLocation,
		bool hasReservedEffects,
		PlayerSummonKnownObjectNpcSkillCastResultShieldBranch shieldBranch)
	{
		steps.Add(PlayerSummonKnownObjectNpcSkillCastResultPacketStep.WriteEffectorObjectId);
		steps.Add(PlayerSummonKnownObjectNpcSkillCastResultPacketStep.WriteTargetType);
		steps.Add(targetType == 1
			? PlayerSummonKnownObjectNpcSkillCastResultPacketStep.WriteTargetCoordinates
			: PlayerSummonKnownObjectNpcSkillCastResultPacketStep.WriteTargetObjectId);
		steps.Add(PlayerSummonKnownObjectNpcSkillCastResultPacketStep.WriteSkillIdAndLevel);
		steps.Add(PlayerSummonKnownObjectNpcSkillCastResultPacketStep.WriteCooldownHitTimeAndUnknown);
		steps.Add(effectsCount == 0
			? PlayerSummonKnownObjectNpcSkillCastResultPacketStep.WriteNoDamageChainFlag
			: chainSuccess
				? PlayerSummonKnownObjectNpcSkillCastResultPacketStep.WriteChainSuccessFlag
				: PlayerSummonKnownObjectNpcSkillCastResultPacketStep.WriteNoChainFlag);

		if (combatActivatedItem)
		{
			steps.Add(PlayerSummonKnownObjectNpcSkillCastResultPacketStep.WriteCombatItemHeader);
		}
		else
		{
			if (isPenaltySkillMethod)
				steps.Add(PlayerSummonKnownObjectNpcSkillCastResultPacketStep.WritePenaltySkillHeader);
			else
				steps.Add(PlayerSummonKnownObjectNpcSkillCastResultPacketStep.WriteNormalSkillHeader);
			steps.Add(PlayerSummonKnownObjectNpcSkillCastResultPacketStep.WriteDashStatus);
			if (dashStatusWritesLocation)
				steps.Add(PlayerSummonKnownObjectNpcSkillCastResultPacketStep.WriteDashLocation);
		}

		steps.Add(PlayerSummonKnownObjectNpcSkillCastResultPacketStep.WriteEffectsCount);
		if (effectsCount <= 0)
			return;

		steps.Add(effectHasOriginalTarget
			? PlayerSummonKnownObjectNpcSkillCastResultPacketStep.WriteEffectedObjectId
			: PlayerSummonKnownObjectNpcSkillCastResultPacketStep.WriteEffectorAsPointPointEffectTarget);
		steps.Add(PlayerSummonKnownObjectNpcSkillCastResultPacketStep.WriteEffectResultId);
		steps.Add(PlayerSummonKnownObjectNpcSkillCastResultPacketStep.WriteEffectedHpPercent);
		steps.Add(PlayerSummonKnownObjectNpcSkillCastResultPacketStep.WriteEffectorHpPercent);
		steps.Add(PlayerSummonKnownObjectNpcSkillCastResultPacketStep.WriteSpellStatus);
		steps.Add(PlayerSummonKnownObjectNpcSkillCastResultPacketStep.WriteSuccessfulEffectsByte);
		steps.Add(PlayerSummonKnownObjectNpcSkillCastResultPacketStep.WriteReservedZeroAndCarvedSignet);
		if (spellStatusWritesLocation || subEffectWritesLocation)
			steps.Add(PlayerSummonKnownObjectNpcSkillCastResultPacketStep.WriteEffectMovementLocation);
		steps.Add(PlayerSummonKnownObjectNpcSkillCastResultPacketStep.WriteReservedEffectsCount);
		if (!hasReservedEffects)
			return;

		steps.Add(PlayerSummonKnownObjectNpcSkillCastResultPacketStep.WriteReservedEffectTypeAndValue);
		steps.Add(PlayerSummonKnownObjectNpcSkillCastResultPacketStep.WriteAttackStatusId);
		steps.Add(PlayerSummonKnownObjectNpcSkillCastResultPacketStep.MaybeSetLastCounterSkill);
		steps.Add(PlayerSummonKnownObjectNpcSkillCastResultPacketStep.WriteShieldDefense);
		if (shieldBranch == PlayerSummonKnownObjectNpcSkillCastResultShieldBranch.Protect)
			steps.Add(PlayerSummonKnownObjectNpcSkillCastResultPacketStep.WriteProtectShieldFields);
		else if (shieldBranch == PlayerSummonKnownObjectNpcSkillCastResultShieldBranch.ReflectOrMpShield)
			steps.Add(PlayerSummonKnownObjectNpcSkillCastResultPacketStep.WriteReflectOrMpShieldFields);
	}

	private static void AddOptionalItemUseSystemMessage(
		List<PlayerSummonKnownObjectNpcSkillCastResultPacketStep> steps,
		bool isItemSkillMethod,
		bool effectorIsPlayer)
	{
		if (isItemSkillMethod && effectorIsPlayer)
			steps.Add(PlayerSummonKnownObjectNpcSkillCastResultPacketStep.SendUseItemSystemMessage);
	}
}

public enum PlayerSummonKnownObjectNpcSkillCastResultPacketTraceStatus
{
	MissingResult,
	NoCastResult,
	ItemUsageAnimationOnly,
	UnsupportedTargetType,
	OrderedPacketFanout,
}

public enum PlayerSummonKnownObjectNpcSkillCastResultShieldBranch
{
	None,
	Protect,
	ReflectOrMpShield,
}

public sealed record PlayerSummonKnownObjectNpcSkillEffectReservedPacketProjection(
	PlayerSummonKnownObjectNpcSkillEffectReservedPacketProjectionStatus Status,
	IReadOnlyList<PlayerSummonKnownObjectNpcSkillEffectReservedPacketRow> Rows,
	int ShieldDefense,
	PlayerSummonKnownObjectNpcSkillEffectReservedShieldPayloadBranch ShieldPayloadBranch,
	bool UsedHpZeroFallback,
	bool HasSamePositionOrderingRisk,
	bool WouldSerializePackets = false)
{
	private static readonly IReadOnlyList<PlayerSummonKnownObjectNpcSkillEffectReservedInput> EmptyInputRows = [];

	public int ReservedEffectsCountByte => Rows.Count & 0xFF;

	public int ShieldDefenseByte => ShieldDefense & 0xFF;

	public bool HasExactProtectPayload =>
		ShieldPayloadBranch == PlayerSummonKnownObjectNpcSkillEffectReservedShieldPayloadBranch.ProtectFields;

	public bool HasLongReflectOrMpShieldPayload =>
		ShieldPayloadBranch == PlayerSummonKnownObjectNpcSkillEffectReservedShieldPayloadBranch.ReflectOrMpShieldFields;

	public static PlayerSummonKnownObjectNpcSkillEffectReservedPacketProjection FromJavaEffectReservedRows(
		IReadOnlyList<PlayerSummonKnownObjectNpcSkillEffectReservedInput>? reservedEffects,
		int shieldDefense)
	{
		var sourceRows = reservedEffects ?? EmptyInputRows;
		var samePositionOrderingRisk = sourceRows
			.GroupBy(row => row.Position)
			.Any(group => group.Count() > 1);
		var sendableRows = sourceRows
			.Where(row => row.Send && row.Value != 0)
			.Select((row, inputIndex) => new { row, inputIndex })
			.OrderBy(rowWithIndex => rowWithIndex.row.Position)
			.ThenBy(rowWithIndex => rowWithIndex.inputIndex)
			.Select(rowWithIndex => PlayerSummonKnownObjectNpcSkillEffectReservedPacketRow.FromInput(rowWithIndex.row, rowWithIndex.inputIndex, isJavaFallback: false))
			.ToArray();

		if (sendableRows.Length == 0)
		{
			sendableRows = [
				new PlayerSummonKnownObjectNpcSkillEffectReservedPacketRow(
					Position: 0,
					InputIndex: -1,
					Type: PlayerSummonKnownObjectNpcSkillEffectReservedResourceType.HP,
					ResourceTypeByte: (int)PlayerSummonKnownObjectNpcSkillEffectReservedResourceType.HP,
					SourceValue: 0,
					ValueToSend: 0,
					IsDamage: true,
					Send: true,
					IsJavaFallback: true),
			];
		}

		return new PlayerSummonKnownObjectNpcSkillEffectReservedPacketProjection(
			PlayerSummonKnownObjectNpcSkillEffectReservedPacketProjectionStatus.Projected,
			sendableRows,
			shieldDefense,
			PlayerSummonKnownObjectNpcSkillEffectReservedShieldPayloadBranchClassifier.FromShieldDefense(shieldDefense),
			UsedHpZeroFallback: sendableRows.Any(row => row.IsJavaFallback),
			HasSamePositionOrderingRisk: samePositionOrderingRisk);
	}
}

public sealed record PlayerSummonKnownObjectNpcSkillEffectReservedInput(
	int Position,
	int Value,
	PlayerSummonKnownObjectNpcSkillEffectReservedResourceType Type,
	bool IsDamage = true,
	bool Send = true);

public sealed record PlayerSummonKnownObjectNpcSkillEffectReservedPacketRow(
	int Position,
	int InputIndex,
	PlayerSummonKnownObjectNpcSkillEffectReservedResourceType Type,
	int ResourceTypeByte,
	int SourceValue,
	int ValueToSend,
	bool IsDamage,
	bool Send,
	bool IsJavaFallback)
{
	public static PlayerSummonKnownObjectNpcSkillEffectReservedPacketRow FromInput(
		PlayerSummonKnownObjectNpcSkillEffectReservedInput input,
		int inputIndex,
		bool isJavaFallback)
	{
		return new PlayerSummonKnownObjectNpcSkillEffectReservedPacketRow(
			input.Position,
			inputIndex,
			input.Type,
			(int)input.Type,
			input.Value,
			input.IsDamage ? input.Value : unchecked(-input.Value),
			input.IsDamage,
			input.Send,
			isJavaFallback);
	}
}

public enum PlayerSummonKnownObjectNpcSkillEffectReservedPacketProjectionStatus
{
	Projected,
}

// Java: EffectReserved.ResourceType uses explicit values HP=0, MP=1, FP=2, DP=3; HealType order is not the packet source of truth.
public enum PlayerSummonKnownObjectNpcSkillEffectReservedResourceType
{
	HP = 0,
	MP = 1,
	FP = 2,
	DP = 3,
}

public enum PlayerSummonKnownObjectNpcSkillEffectReservedShieldPayloadBranch
{
	NoExtraFields,
	ProtectFields,
	ReflectOrMpShieldFields,
}

public static class PlayerSummonKnownObjectNpcSkillEffectReservedShieldPayloadBranchClassifier
{
	public static PlayerSummonKnownObjectNpcSkillEffectReservedShieldPayloadBranch FromShieldDefense(int shieldDefense)
	{
		return shieldDefense switch
		{
			0 or 2 => PlayerSummonKnownObjectNpcSkillEffectReservedShieldPayloadBranch.NoExtraFields,
			8 or 10 => PlayerSummonKnownObjectNpcSkillEffectReservedShieldPayloadBranch.ProtectFields,
			_ => PlayerSummonKnownObjectNpcSkillEffectReservedShieldPayloadBranch.ReflectOrMpShieldFields,
		};
	}
}

public sealed record PlayerSummonKnownObjectNpcSkillEffectReservedProducerCatalog(
	IReadOnlyList<PlayerSummonKnownObjectNpcSkillEffectReservedProducerContract> ReservedProducers,
	IReadOnlyList<PlayerSummonKnownObjectNpcSkillShieldProducerContract> ShieldProducers,
	bool WouldExecuteEffects = false)
{
	public bool DocumentsNonSentOverTimeRows =>
		ReservedProducers.Any(producer => producer.Visibility == PlayerSummonKnownObjectNpcSkillEffectReservedProducerVisibility.FilteredFromCastResult);

	public bool DocumentsPlayerOnlyResourceRows =>
		ReservedProducers.Any(producer => producer.RequiresPlayerTarget || producer.RequiresPlayerEffector);

	public bool DocumentsShieldPayloadBranches =>
		ShieldProducers.Any(producer => producer.PayloadBranch == PlayerSummonKnownObjectNpcSkillEffectReservedShieldPayloadBranch.ProtectFields)
		&& ShieldProducers.Any(producer => producer.PayloadBranch == PlayerSummonKnownObjectNpcSkillEffectReservedShieldPayloadBranch.ReflectOrMpShieldFields)
		&& ShieldProducers.Any(producer => producer.PayloadBranch == PlayerSummonKnownObjectNpcSkillEffectReservedShieldPayloadBranch.NoExtraFields);

	public static PlayerSummonKnownObjectNpcSkillEffectReservedProducerCatalog JavaSourceReviewed()
	{
		return new PlayerSummonKnownObjectNpcSkillEffectReservedProducerCatalog(
			[
				new(
					"com.aionemu.gameserver.controllers.attack.AttackUtil",
					"calculateEffectResult",
					PlayerSummonKnownObjectNpcSkillEffectReservedProducerKind.AttackResultHpDamage,
					PlayerSummonKnownObjectNpcSkillEffectReservedResourceType.HP,
					IsDamage: true,
					Send: true,
					PlayerSummonKnownObjectNpcSkillEffectReservedProducerValueSource.AttackResultDamageIntTruncation,
					PlayerSummonKnownObjectNpcSkillEffectReservedProducerPositionSource.EffectTemplatePosition,
					PlayerSummonKnownObjectNpcSkillEffectReservedProducerVisibility.SentWhenNonZero,
					MayCopyShieldDefense: true,
					MaySuppressSendFlag: true,
					UsesFloatToIntTruncation: true),
				new(
					"com.aionemu.gameserver.skillengine.effect.AbstractHealEffect",
					"calculate",
					PlayerSummonKnownObjectNpcSkillEffectReservedProducerKind.InstantHeal,
					PlayerSummonKnownObjectNpcSkillEffectReservedResourceType.HP,
					IsDamage: false,
					Send: true,
					PlayerSummonKnownObjectNpcSkillEffectReservedProducerValueSource.CalculateHealValueCappedByMissingStat,
					PlayerSummonKnownObjectNpcSkillEffectReservedProducerPositionSource.EffectTemplatePosition,
					PlayerSummonKnownObjectNpcSkillEffectReservedProducerVisibility.SentWhenNonZero,
					ResourceTypeComesFromHealTypeName: true,
					UsesIntegerPercentCalculation: true,
					MayProduceZeroValue: true),
				new(
					"com.aionemu.gameserver.skillengine.effect.HealOverTimeEffect",
					"startEffect",
					PlayerSummonKnownObjectNpcSkillEffectReservedProducerKind.HealOverTime,
					PlayerSummonKnownObjectNpcSkillEffectReservedResourceType.HP,
					IsDamage: false,
					Send: false,
					PlayerSummonKnownObjectNpcSkillEffectReservedProducerValueSource.CalculateHealValueCappedByMissingStat,
					PlayerSummonKnownObjectNpcSkillEffectReservedProducerPositionSource.EffectTemplatePosition,
					PlayerSummonKnownObjectNpcSkillEffectReservedProducerVisibility.FilteredFromCastResult,
					ResourceTypeComesFromHealTypeName: true,
					IsOverTimeProducer: true,
					UsesScheduler: true,
					UsesIntegerPercentCalculation: true,
					MayProduceZeroValue: true),
				new(
					"com.aionemu.gameserver.skillengine.effect.BleedEffect",
					"startEffect",
					PlayerSummonKnownObjectNpcSkillEffectReservedProducerKind.OverTimeHpDamage,
					PlayerSummonKnownObjectNpcSkillEffectReservedResourceType.HP,
					IsDamage: true,
					Send: false,
					PlayerSummonKnownObjectNpcSkillEffectReservedProducerValueSource.MagicalOverTimeDamageInt,
					PlayerSummonKnownObjectNpcSkillEffectReservedProducerPositionSource.EffectTemplatePosition,
					PlayerSummonKnownObjectNpcSkillEffectReservedProducerVisibility.FilteredFromCastResult,
					IsOverTimeProducer: true,
					UsesScheduler: true,
					UsesFloatToIntTruncation: true,
					MinimumDamageOne: true),
				new(
					"com.aionemu.gameserver.skillengine.effect.PoisonEffect",
					"startEffect",
					PlayerSummonKnownObjectNpcSkillEffectReservedProducerKind.OverTimeHpDamage,
					PlayerSummonKnownObjectNpcSkillEffectReservedResourceType.HP,
					IsDamage: true,
					Send: false,
					PlayerSummonKnownObjectNpcSkillEffectReservedProducerValueSource.MagicalOverTimeDamageInt,
					PlayerSummonKnownObjectNpcSkillEffectReservedProducerPositionSource.EffectTemplatePosition,
					PlayerSummonKnownObjectNpcSkillEffectReservedProducerVisibility.FilteredFromCastResult,
					IsOverTimeProducer: true,
					UsesScheduler: true,
					UsesFloatToIntTruncation: true,
					MinimumDamageOne: true),
				new(
					"com.aionemu.gameserver.skillengine.effect.SpellAttackEffect",
					"startEffect",
					PlayerSummonKnownObjectNpcSkillEffectReservedProducerKind.OverTimeHpDamage,
					PlayerSummonKnownObjectNpcSkillEffectReservedResourceType.HP,
					IsDamage: true,
					Send: false,
					PlayerSummonKnownObjectNpcSkillEffectReservedProducerValueSource.MagicalOverTimeDamageInt,
					PlayerSummonKnownObjectNpcSkillEffectReservedProducerPositionSource.EffectTemplatePosition,
					PlayerSummonKnownObjectNpcSkillEffectReservedProducerVisibility.FilteredFromCastResult,
					IsOverTimeProducer: true,
					UsesScheduler: true,
					UsesFloatToIntTruncation: true,
					MinimumDamageOne: true),
				new(
					"com.aionemu.gameserver.skillengine.effect.MpAttackInstantEffect",
					"calculate",
					PlayerSummonKnownObjectNpcSkillEffectReservedProducerKind.InstantMpDamage,
					PlayerSummonKnownObjectNpcSkillEffectReservedResourceType.MP,
					IsDamage: true,
					Send: true,
					PlayerSummonKnownObjectNpcSkillEffectReservedProducerValueSource.TemplateValueOrPercentOfMaxResource,
					PlayerSummonKnownObjectNpcSkillEffectReservedProducerPositionSource.EffectTemplatePosition,
					PlayerSummonKnownObjectNpcSkillEffectReservedProducerVisibility.SentWhenNonZero,
					UsesIntegerPercentCalculation: true),
				new(
					"com.aionemu.gameserver.skillengine.effect.FpAttackInstantEffect",
					"calculate",
					PlayerSummonKnownObjectNpcSkillEffectReservedProducerKind.InstantFpDamage,
					PlayerSummonKnownObjectNpcSkillEffectReservedResourceType.FP,
					IsDamage: true,
					Send: true,
					PlayerSummonKnownObjectNpcSkillEffectReservedProducerValueSource.TemplateValueOrPercentOfMaxResource,
					PlayerSummonKnownObjectNpcSkillEffectReservedProducerPositionSource.EffectTemplatePosition,
					PlayerSummonKnownObjectNpcSkillEffectReservedProducerVisibility.SentWhenNonZero,
					RequiresPlayerTarget: true,
					UsesIntegerPercentCalculation: true),
				new(
					"com.aionemu.gameserver.skillengine.effect.DPTransferEffect",
					"calculate",
					PlayerSummonKnownObjectNpcSkillEffectReservedProducerKind.DpTransfer,
					PlayerSummonKnownObjectNpcSkillEffectReservedResourceType.DP,
					IsDamage: true,
					Send: true,
					PlayerSummonKnownObjectNpcSkillEffectReservedProducerValueSource.EffectorCurrentDp,
					PlayerSummonKnownObjectNpcSkillEffectReservedProducerPositionSource.EffectTemplatePosition,
					PlayerSummonKnownObjectNpcSkillEffectReservedProducerVisibility.SentWhenNonZero,
					RequiresPlayerTarget: true,
					RequiresPlayerEffector: true),
			],
			[
				new(
					"com.aionemu.gameserver.skillengine.effect.ShieldEffect",
					"startEffect",
					2,
					PlayerSummonKnownObjectNpcSkillEffectReservedShieldPayloadBranch.NoExtraFields,
					MayReduceIncomingDamage: true),
				new(
					"com.aionemu.gameserver.skillengine.effect.MPShieldEffect",
					"startEffect",
					16,
					PlayerSummonKnownObjectNpcSkillEffectReservedShieldPayloadBranch.ReflectOrMpShieldFields,
					MayReduceIncomingDamage: true,
					WritesMpShieldPayload: true),
				new(
					"com.aionemu.gameserver.skillengine.effect.ReflectorEffect",
					"startEffect",
					1,
					PlayerSummonKnownObjectNpcSkillEffectReservedShieldPayloadBranch.ReflectOrMpShieldFields,
					WritesReflectPayload: true),
				new(
					"com.aionemu.gameserver.skillengine.effect.ReflectorEffect",
					"startEffect",
					32,
					PlayerSummonKnownObjectNpcSkillEffectReservedShieldPayloadBranch.ReflectOrMpShieldFields,
					WritesReflectPayload: true,
					CanEndShieldAfterOneSkill: true,
					CanBeStrippedForUnsupportedSubtype: true),
				new(
					"com.aionemu.gameserver.skillengine.effect.ProtectEffect",
					"startEffect",
					8,
					PlayerSummonKnownObjectNpcSkillEffectReservedShieldPayloadBranch.ProtectFields,
					WritesProtectPayload: true,
					MayReduceIncomingDamage: true),
				new(
					"com.aionemu.gameserver.skillengine.effect.ConvertHealEffect",
					"startEffect",
					0,
					PlayerSummonKnownObjectNpcSkillEffectReservedShieldPayloadBranch.NoExtraFields,
					MayReduceIncomingDamage: true,
					MayHealInsteadOfWritingShieldType: true),
				new(
					"com.aionemu.gameserver.skillengine.effect.EffectTemplate",
					"calculateDamage",
					32,
					PlayerSummonKnownObjectNpcSkillEffectReservedShieldPayloadBranch.ReflectOrMpShieldFields,
					WritesReflectPayload: true,
					CanBeStrippedForUnsupportedSubtype: true),
			]);
	}
}

public sealed record PlayerSummonKnownObjectNpcSkillEffectReservedProducerContract(
	string JavaArtifact,
	string JavaMethod,
	PlayerSummonKnownObjectNpcSkillEffectReservedProducerKind Kind,
	PlayerSummonKnownObjectNpcSkillEffectReservedResourceType ResourceType,
	bool IsDamage,
	bool Send,
	PlayerSummonKnownObjectNpcSkillEffectReservedProducerValueSource ValueSource,
	PlayerSummonKnownObjectNpcSkillEffectReservedProducerPositionSource PositionSource,
	PlayerSummonKnownObjectNpcSkillEffectReservedProducerVisibility Visibility,
	bool MayCopyShieldDefense = false,
	bool MaySuppressSendFlag = false,
	bool ResourceTypeComesFromHealTypeName = false,
	bool RequiresPlayerTarget = false,
	bool RequiresPlayerEffector = false,
	bool IsOverTimeProducer = false,
	bool UsesScheduler = false,
	bool UsesIntegerPercentCalculation = false,
	bool UsesFloatToIntTruncation = false,
	bool MayProduceZeroValue = false,
	bool MinimumDamageOne = false);

public sealed record PlayerSummonKnownObjectNpcSkillShieldProducerContract(
	string JavaArtifact,
	string JavaMethod,
	int ShieldTypeValue,
	PlayerSummonKnownObjectNpcSkillEffectReservedShieldPayloadBranch PayloadBranch,
	bool WritesProtectPayload = false,
	bool WritesReflectPayload = false,
	bool WritesMpShieldPayload = false,
	bool MayReduceIncomingDamage = false,
	bool MayHealInsteadOfWritingShieldType = false,
	bool CanEndShieldAfterOneSkill = false,
	bool CanBeStrippedForUnsupportedSubtype = false)
{
	public int ShieldTypeByte => ShieldTypeValue & 0xFF;
}

public enum PlayerSummonKnownObjectNpcSkillEffectReservedProducerKind
{
	AttackResultHpDamage,
	InstantHeal,
	HealOverTime,
	OverTimeHpDamage,
	InstantMpDamage,
	InstantFpDamage,
	DpTransfer,
}

public enum PlayerSummonKnownObjectNpcSkillEffectReservedProducerValueSource
{
	AttackResultDamageIntTruncation,
	CalculateHealValueCappedByMissingStat,
	MagicalOverTimeDamageInt,
	TemplateValueOrPercentOfMaxResource,
	EffectorCurrentDp,
}

public enum PlayerSummonKnownObjectNpcSkillEffectReservedProducerPositionSource
{
	EffectTemplatePosition,
}

public enum PlayerSummonKnownObjectNpcSkillEffectReservedProducerVisibility
{
	SentWhenNonZero,
	FilteredFromCastResult,
}

public enum PlayerSummonKnownObjectNpcSkillCastResultPacketStep
{
	CheckNonCombatItemTemplate,
	ConstructSmItemUsageAnimation,
	BroadcastSmItemUsageAnimationAndReceive,
	ResolveCreatureNeedsHelpAiEvent,
	SelectObjectTargetBranch,
	SelectXyzTargetBranch,
	ConstructSmCastspellResult,
	BroadcastSmCastspellResult,
	MarkCastSpellResultSent,
	WriteEffectorObjectId,
	WriteTargetType,
	WriteTargetObjectId,
	WriteTargetCoordinates,
	WriteSkillIdAndLevel,
	WriteCooldownHitTimeAndUnknown,
	WriteNoDamageChainFlag,
	WriteChainSuccessFlag,
	WriteNoChainFlag,
	WriteCombatItemHeader,
	WritePenaltySkillHeader,
	WriteNormalSkillHeader,
	WriteDashStatus,
	WriteDashLocation,
	WriteEffectsCount,
	WriteEffectedObjectId,
	WriteEffectorAsPointPointEffectTarget,
	WriteEffectResultId,
	WriteEffectedHpPercent,
	WriteEffectorHpPercent,
	WriteSpellStatus,
	WriteSuccessfulEffectsByte,
	WriteReservedZeroAndCarvedSignet,
	WriteEffectMovementLocation,
	WriteReservedEffectsCount,
	WriteReservedEffectTypeAndValue,
	WriteAttackStatusId,
	MaybeSetLastCounterSkill,
	WriteShieldDefense,
	WriteProtectShieldFields,
	WriteReflectOrMpShieldFields,
	SendUseItemSystemMessage,
}

public sealed record PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaGolden(
	PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaGoldenStatus Status,
	PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaSample Sample,
	byte[] Payload,
	IReadOnlyList<PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaField> Fields)
{
	public bool WouldSendPacket => false;

	public bool UsesLittleEndianByteOrder => true;

	public bool DemonstratesJavaShortTruncation =>
		Fields.Any(schemaField => schemaField.WriteKind == PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaWriteKind.WriteHTruncated);

	public bool DemonstratesJavaByteTruncation =>
		Fields.Any(schemaField => schemaField.WriteKind == PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaWriteKind.WriteCTruncated);

	public string PayloadHex => Convert.ToHexString(Payload);

	public static PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaGolden FromSample(
		PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaSample sample)
	{
		var writer = new JavaPacketSchemaWriter();
		switch (sample)
		{
			case PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaSample.ObjectTargetWithReservedProtect:
				WriteObjectTargetWithReservedProtect(writer);
				break;
			case PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaSample.XyzTargetNoEffects:
				WriteXyzTargetNoEffects(writer);
				break;
			case PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaSample.CombatItemPointPointReflect:
				WriteCombatItemPointPointReflect(writer);
				break;
			default:
				return new PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaGolden(
					PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaGoldenStatus.UnsupportedSample,
					sample,
					[],
					[]);
		}

		return new PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaGolden(
			PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaGoldenStatus.Projected,
			sample,
			writer.ToArray(),
			writer.Fields);
	}

	private static void WriteObjectTargetWithReservedProtect(JavaPacketSchemaWriter writer)
	{
		writer.WriteD(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.EffectorObjectId), 0x01020304);
		writer.WriteC(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.TargetType), 0);
		writer.WriteD(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.TargetObjectId), 0x05060708);
		writer.WriteH(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.SkillId), 0x12345);
		writer.WriteC(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.SkillLevel), 0x1FF);
		writer.WriteD(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.Cooldown), 0x0A0B0C0D);
		writer.WriteH(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.HitTime), 0x12345);
		writer.WriteC(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.UnknownHeaderByte), 0);
		writer.WriteC(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.ChainResultFlag), 32);
		writer.WriteC(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.SkillHeader), 0);
		writer.WriteC(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.DashStatus), 2);
		writer.WriteC(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.DashHeading), 0x12);
		writer.WriteF(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.DashX), 1.5f);
		writer.WriteF(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.DashY), -2.25f);
		writer.WriteF(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.DashZ), 3.75f);
		writer.WriteH(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.EffectsCount), 1);
		writer.WriteD(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.EffectedObjectId), 0x0E0F1011);
		writer.WriteC(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.EffectResultId), 4);
		writer.WriteC(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.EffectedHpPercent), 0x12C);
		writer.WriteC(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.EffectorHpPercent), 0x101);
		writer.WriteC(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.SpellStatus), 256);
		writer.WriteC(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.SuccessfulEffectsByte), 0x1AB);
		writer.WriteH(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.ReservedZero), 0);
		writer.WriteC(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.CarvedSignet), 0x102);
		writer.WriteC(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.ReservedEffectsCount), 1);
		writer.WriteC(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.ReservedEffectType), 3);
		writer.WriteD(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.ReservedEffectValue), -123456);
		writer.WriteC(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.AttackStatusId), -54);
		writer.WriteC(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.ShieldDefense), 8);
		writer.WriteD(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.ProtectorId), 0x01000002);
		writer.WriteD(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.ProtectedDamage), 0x03000004);
		writer.WriteD(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.ProtectedSkillId), 0x05000006);
	}

	private static void WriteXyzTargetNoEffects(JavaPacketSchemaWriter writer)
	{
		writer.WriteD(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.EffectorObjectId), 0x11223344);
		writer.WriteC(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.TargetType), 1);
		writer.WriteF(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.TargetX), 10.5f);
		writer.WriteF(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.TargetY), -20.25f);
		writer.WriteF(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.TargetZ), 30.75f);
		writer.WriteH(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.SkillId), 0x23456);
		writer.WriteC(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.SkillLevel), 2);
		writer.WriteD(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.Cooldown), 0);
		writer.WriteH(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.HitTime), 0x1FFFF);
		writer.WriteC(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.UnknownHeaderByte), 0);
		writer.WriteC(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.ChainResultFlag), 16);
		writer.WriteC(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.SkillHeader), 4);
		writer.WriteC(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.DashStatus), 0);
		writer.WriteH(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.EffectsCount), 0);
	}

	private static void WriteCombatItemPointPointReflect(JavaPacketSchemaWriter writer)
	{
		writer.WriteD(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.EffectorObjectId), 0x01010101);
		writer.WriteC(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.TargetType), 0);
		writer.WriteD(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.TargetObjectId), 0x02020202);
		writer.WriteH(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.SkillId), 0x1010);
		writer.WriteC(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.SkillLevel), 3);
		writer.WriteD(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.Cooldown), 0x03030303);
		writer.WriteH(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.HitTime), 250);
		writer.WriteC(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.UnknownHeaderByte), 0);
		writer.WriteC(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.ChainResultFlag), 0);
		writer.WriteC(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.CombatItemHeader), 2);
		writer.WriteD(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.ItemObjectId), 0x04040404);
		writer.WriteD(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.ItemTemplateId), 0x05050505);
		writer.WriteC(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.CombatItemUnknown), 0);
		writer.WriteH(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.EffectsCount), 1);
		writer.WriteD(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.PointPointEffectorObjectId), 0x01010101);
		writer.WriteC(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.EffectResultId), 0);
		writer.WriteC(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.EffectedHpPercent), 100);
		writer.WriteC(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.EffectorHpPercent), 77);
		writer.WriteC(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.SpellStatus), 16);
		writer.WriteC(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.SuccessfulEffectsByte), 1);
		writer.WriteH(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.ReservedZero), 0);
		writer.WriteC(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.CarvedSignet), 0);
		writer.WriteC(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.SpinHeading), 0x7F);
		writer.WriteC(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.ReservedEffectsCount), 1);
		writer.WriteC(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.ReservedEffectType), 0);
		writer.WriteD(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.ReservedEffectValue), 42);
		writer.WriteC(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.AttackStatusId), 10);
		writer.WriteC(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.ShieldDefense), 16);
		writer.WriteD(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.ProtectorId), 1);
		writer.WriteD(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.ProtectedDamage), 2);
		writer.WriteD(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.ProtectedSkillId), 3);
		writer.WriteD(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.ReflectedDamage), 4);
		writer.WriteD(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.ReflectedSkillId), 5);
		writer.WriteD(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.MpAbsorbed), 6);
		writer.WriteD(nameof(PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName.MpShieldSkillId), 7);
	}

	private sealed class JavaPacketSchemaWriter
	{
		private readonly List<byte> _payload = [];
		private readonly List<PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaField> _fields = [];

		public IReadOnlyList<PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaField> Fields => _fields;

		public byte[] ToArray() => _payload.ToArray();

		public void WriteD(string name, int value)
		{
			Span<byte> bytes = stackalloc byte[4];
			BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
			Add(name, value, PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaWriteKind.WriteD, bytes);
		}

		public void WriteH(string name, int value)
		{
			Span<byte> bytes = stackalloc byte[2];
			BinaryPrimitives.WriteInt16LittleEndian(bytes, unchecked((short)value));
			Add(
				name,
				value,
				value is < short.MinValue or > ushort.MaxValue
					? PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaWriteKind.WriteHTruncated
					: PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaWriteKind.WriteH,
				bytes);
		}

		public void WriteC(string name, int value)
		{
			Span<byte> bytes = stackalloc byte[1];
			bytes[0] = unchecked((byte)value);
			Add(
				name,
				value,
				value is < sbyte.MinValue or > byte.MaxValue
					? PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaWriteKind.WriteCTruncated
					: PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaWriteKind.WriteC,
				bytes);
		}

		public void WriteF(string name, float value)
		{
			Span<byte> bytes = stackalloc byte[4];
			BinaryPrimitives.WriteInt32LittleEndian(bytes, BitConverter.SingleToInt32Bits(value));
			Add(name, value, PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaWriteKind.WriteF, bytes);
		}

		private void Add(
			string name,
			object value,
			PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaWriteKind writeKind,
			ReadOnlySpan<byte> bytes)
		{
			var fieldBytes = bytes.ToArray();
			_payload.AddRange(fieldBytes);
			_fields.Add(new PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaField(
				name,
				writeKind,
				Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
				fieldBytes));
		}
	}
}

public sealed record PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaField(
	string Name,
	PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaWriteKind WriteKind,
	string SourceValue,
	byte[] Bytes);

public enum PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaGoldenStatus
{
	Projected,
	UnsupportedSample,
}

public enum PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaSample
{
	ObjectTargetWithReservedProtect,
	XyzTargetNoEffects,
	CombatItemPointPointReflect,
}

public enum PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaWriteKind
{
	WriteD,
	WriteH,
	WriteHTruncated,
	WriteC,
	WriteCTruncated,
	WriteF,
}

public enum PlayerSummonKnownObjectNpcSkillCastResultPacketSchemaFieldName
{
	EffectorObjectId,
	TargetType,
	TargetObjectId,
	TargetX,
	TargetY,
	TargetZ,
	SkillId,
	SkillLevel,
	Cooldown,
	HitTime,
	UnknownHeaderByte,
	ChainResultFlag,
	SkillHeader,
	CombatItemHeader,
	ItemObjectId,
	ItemTemplateId,
	CombatItemUnknown,
	DashStatus,
	DashHeading,
	DashX,
	DashY,
	DashZ,
	EffectsCount,
	EffectedObjectId,
	PointPointEffectorObjectId,
	EffectResultId,
	EffectedHpPercent,
	EffectorHpPercent,
	SpellStatus,
	SuccessfulEffectsByte,
	ReservedZero,
	CarvedSignet,
	SpinHeading,
	ReservedEffectsCount,
	ReservedEffectType,
	ReservedEffectValue,
	AttackStatusId,
	ShieldDefense,
	ProtectorId,
	ProtectedDamage,
	ProtectedSkillId,
	ReflectedDamage,
	ReflectedSkillId,
	MpAbsorbed,
	MpShieldSkillId,
}

public sealed record PlayerSummonKnownObjectNpcSkillEndCastBranchTrace(
	PlayerSummonKnownObjectNpcSkillEndCastBranchTraceStatus Status,
	PlayerSummonKnownObjectNpcSkillActionResult? ActionResult = null,
	IReadOnlyList<PlayerSummonKnownObjectNpcSkillEndCastBranchStep>? Steps = null)
{
	private static readonly IReadOnlyList<PlayerSummonKnownObjectNpcSkillEndCastBranchStep> EmptySteps = [];

	public IReadOnlyList<PlayerSummonKnownObjectNpcSkillEndCastBranchStep> OrderedSteps => Steps ?? EmptySteps;

	public bool WouldExecuteSideEffects => false;

	public bool IgnoresEndConditionResult =>
		OrderedSteps.Contains(PlayerSummonKnownObjectNpcSkillEndCastBranchStep.EndCondCheckIgnored);

	public bool ActionsRunBeforeCooldowns =>
		IndexOf(PlayerSummonKnownObjectNpcSkillEndCastBranchStep.ExecuteActions) is int actionIndex
		&& actionIndex >= 0
		&& IndexOf(PlayerSummonKnownObjectNpcSkillEndCastBranchStep.SetCooldowns) is int cooldownIndex
		&& cooldownIndex > actionIndex;

	public bool BlocksPenaltyAfterFullResistOrDodge =>
		OrderedSteps.Contains(PlayerSummonKnownObjectNpcSkillEndCastBranchStep.MarkBlockedPenaltySkill)
		&& !OrderedSteps.Contains(PlayerSummonKnownObjectNpcSkillEndCastBranchStep.StartPenaltySkill);

	private int IndexOf(PlayerSummonKnownObjectNpcSkillEndCastBranchStep step)
	{
		for (var i = 0; i < OrderedSteps.Count; i++)
		{
			if (OrderedSteps[i] == step)
				return i;
		}

		return -1;
	}

	public static PlayerSummonKnownObjectNpcSkillEndCastBranchTrace FromActionResult(
		PlayerSummonKnownObjectNpcSkillActionResult? actionResult,
		bool effectorStillCasting,
		bool endCastValidationPasses,
		bool isItemSkill,
		bool itemAvailable,
		bool itemConsumed,
		bool actionsSucceed,
		bool hasEffects,
		bool allEffectsResistedOrDodged,
		bool isPlayerEffector,
		bool isMultiCast,
		bool hasChainCategory,
		bool chainSucceeds,
		bool isInstantSkill,
		bool sendsCastResult,
		bool isNpcEffector,
		bool isCastSkillMethod)
	{
		if (actionResult == null || actionResult.Status == PlayerSummonKnownObjectNpcSkillActionResultStatus.MissingPreview)
		{
			return new PlayerSummonKnownObjectNpcSkillEndCastBranchTrace(
				PlayerSummonKnownObjectNpcSkillEndCastBranchTraceStatus.MissingResult,
				actionResult);
		}

		if (actionResult.Status != PlayerSummonKnownObjectNpcSkillActionResultStatus.UseSkill)
		{
			return new PlayerSummonKnownObjectNpcSkillEndCastBranchTrace(
				PlayerSummonKnownObjectNpcSkillEndCastBranchTraceStatus.NoEndCast,
				actionResult);
		}

		var steps = new List<PlayerSummonKnownObjectNpcSkillEndCastBranchStep>
		{
			PlayerSummonKnownObjectNpcSkillEndCastBranchStep.RemoveObservers,
			PlayerSummonKnownObjectNpcSkillEndCastBranchStep.CheckEffectorStillCastingAndNotCancelled,
		};

		if (!effectorStillCasting)
		{
			return new PlayerSummonKnownObjectNpcSkillEndCastBranchTrace(
				PlayerSummonKnownObjectNpcSkillEndCastBranchTraceStatus.NotCastingOrCancelled,
				actionResult,
				steps);
		}

		steps.Add(PlayerSummonKnownObjectNpcSkillEndCastBranchStep.PropertiesEndCastValidate);
		steps.Add(PlayerSummonKnownObjectNpcSkillEndCastBranchStep.ValidateEffectedList);
		steps.Add(PlayerSummonKnownObjectNpcSkillEndCastBranchStep.PreUsageCheck);
		if (!endCastValidationPasses)
		{
			steps.Add(PlayerSummonKnownObjectNpcSkillEndCastBranchStep.ControllerCancelCurrentSkill);
			return new PlayerSummonKnownObjectNpcSkillEndCastBranchTrace(
				PlayerSummonKnownObjectNpcSkillEndCastBranchTraceStatus.CancelledByEndCastValidation,
				actionResult,
				steps);
		}

		steps.Add(PlayerSummonKnownObjectNpcSkillEndCastBranchStep.EffectorSetCastingNull);
		if (isItemSkill)
		{
			steps.Add(PlayerSummonKnownObjectNpcSkillEndCastBranchStep.ItemLookup);
			if (!itemAvailable)
			{
				return new PlayerSummonKnownObjectNpcSkillEndCastBranchTrace(
					PlayerSummonKnownObjectNpcSkillEndCastBranchTraceStatus.ItemMissing,
					actionResult,
					steps);
			}

			steps.Add(PlayerSummonKnownObjectNpcSkillEndCastBranchStep.ItemActivationOrInventoryConsume);
			if (!itemConsumed)
			{
				return new PlayerSummonKnownObjectNpcSkillEndCastBranchTrace(
					PlayerSummonKnownObjectNpcSkillEndCastBranchTraceStatus.ItemConsumeFailed,
					actionResult,
					steps);
			}
		}

		steps.Add(PlayerSummonKnownObjectNpcSkillEndCastBranchStep.EndCondCheckIgnored);
		steps.Add(PlayerSummonKnownObjectNpcSkillEndCastBranchStep.ExecuteActions);
		if (!actionsSucceed)
		{
			return new PlayerSummonKnownObjectNpcSkillEndCastBranchTrace(
				PlayerSummonKnownObjectNpcSkillEndCastBranchTraceStatus.ActionFailed,
				actionResult,
				steps);
		}

		if (hasEffects)
		{
			steps.Add(PlayerSummonKnownObjectNpcSkillEndCastBranchStep.BuildEffects);
			if (allEffectsResistedOrDodged)
			{
				steps.Add(PlayerSummonKnownObjectNpcSkillEndCastBranchStep.MarkBlockedChain);
				steps.Add(PlayerSummonKnownObjectNpcSkillEndCastBranchStep.MarkBlockedPenaltySkill);
			}
		}

		var shouldSetCooldowns = true;
		if (isPlayerEffector)
		{
			if (isMultiCast)
			{
				steps.Add(PlayerSummonKnownObjectNpcSkillEndCastBranchStep.SkipCooldownsForMultiCast);
				shouldSetCooldowns = false;
			}

			if (hasChainCategory)
				steps.Add(chainSucceeds && !allEffectsResistedOrDodged
					? PlayerSummonKnownObjectNpcSkillEndCastBranchStep.PlayerChainUpdate
					: PlayerSummonKnownObjectNpcSkillEndCastBranchStep.PlayerChainReset);

			steps.Add(PlayerSummonKnownObjectNpcSkillEndCastBranchStep.QuestEngineOnUseSkill);
		}

		if (shouldSetCooldowns)
			steps.Add(PlayerSummonKnownObjectNpcSkillEndCastBranchStep.SetCooldowns);
		if (!allEffectsResistedOrDodged)
			steps.Add(PlayerSummonKnownObjectNpcSkillEndCastBranchStep.StartPenaltySkill);
		steps.Add(isInstantSkill
			? PlayerSummonKnownObjectNpcSkillEndCastBranchStep.ApplyEffectImmediately
			: PlayerSummonKnownObjectNpcSkillEndCastBranchStep.ScheduleApplyEffect);
		if (sendsCastResult)
			steps.Add(PlayerSummonKnownObjectNpcSkillEndCastBranchStep.SendCastSpellEnd);
		steps.Add(PlayerSummonKnownObjectNpcSkillEndCastBranchStep.EffectorAiOnEndUseSkill);
		if (isNpcEffector)
		{
			steps.Add(PlayerSummonKnownObjectNpcSkillEndCastBranchStep.NpcSkillEntryFireOnEndCastEvents);
			steps.Add(PlayerSummonKnownObjectNpcSkillEndCastBranchStep.SkillAttackManagerAfterUseSkill);
		}
		if (isCastSkillMethod)
			steps.Add(PlayerSummonKnownObjectNpcSkillEndCastBranchStep.NotifyEndSkillCastObservers);
		steps.Add(PlayerSummonKnownObjectNpcSkillEndCastBranchStep.InstanceHandlerOnEndCastSkill);

		return new PlayerSummonKnownObjectNpcSkillEndCastBranchTrace(
			PlayerSummonKnownObjectNpcSkillEndCastBranchTraceStatus.OrderedBranches,
			actionResult,
			steps);
	}
}

public enum PlayerSummonKnownObjectNpcSkillEndCastBranchTraceStatus
{
	MissingResult,
	NoEndCast,
	NotCastingOrCancelled,
	CancelledByEndCastValidation,
	ItemMissing,
	ItemConsumeFailed,
	ActionFailed,
	OrderedBranches,
}

public enum PlayerSummonKnownObjectNpcSkillEndCastBranchStep
{
	RemoveObservers,
	CheckEffectorStillCastingAndNotCancelled,
	PropertiesEndCastValidate,
	ValidateEffectedList,
	PreUsageCheck,
	ControllerCancelCurrentSkill,
	EffectorSetCastingNull,
	ItemLookup,
	ItemActivationOrInventoryConsume,
	EndCondCheckIgnored,
	ExecuteActions,
	BuildEffects,
	MarkBlockedChain,
	MarkBlockedPenaltySkill,
	SkipCooldownsForMultiCast,
	PlayerChainUpdate,
	PlayerChainReset,
	QuestEngineOnUseSkill,
	SetCooldowns,
	StartPenaltySkill,
	ApplyEffectImmediately,
	ScheduleApplyEffect,
	SendCastSpellEnd,
	EffectorAiOnEndUseSkill,
	NpcSkillEntryFireOnEndCastEvents,
	SkillAttackManagerAfterUseSkill,
	NotifyEndSkillCastObservers,
	InstanceHandlerOnEndCastSkill,
}

public sealed record PlayerSummonKnownObjectNpcSkillEndCastSideEffectTrace(
	PlayerSummonKnownObjectNpcSkillEndCastSideEffectTraceStatus Status,
	PlayerSummonKnownObjectNpcSkillActionResult? ActionResult = null,
	PlayerSummonKnownObjectNpcSkillPostSpawnPreview? PostSpawnPreview = null,
	IReadOnlyList<PlayerSummonKnownObjectNpcSkillEndCastSideEffectStep>? Steps = null)
{
	private static readonly IReadOnlyList<PlayerSummonKnownObjectNpcSkillEndCastSideEffectStep> EmptySteps = [];

	public IReadOnlyList<PlayerSummonKnownObjectNpcSkillEndCastSideEffectStep> OrderedSteps => Steps ?? EmptySteps;

	public bool WouldExecuteSideEffects => false;

	public bool FiresPostSpawnBeforeAfterUseSkill =>
		IndexOf(PlayerSummonKnownObjectNpcSkillEndCastSideEffectStep.NpcSkillEntryFireOnEndCastEvents) is int postSpawnIndex
		&& postSpawnIndex >= 0
		&& IndexOf(PlayerSummonKnownObjectNpcSkillEndCastSideEffectStep.SkillAttackManagerAfterUseSkill) is int afterUseIndex
		&& afterUseIndex > postSpawnIndex;

	public bool SendsCastResultBeforeNpcAfterUseSkill =>
		IndexOf(PlayerSummonKnownObjectNpcSkillEndCastSideEffectStep.SendCastSpellEnd) is int packetIndex
		&& packetIndex >= 0
		&& IndexOf(PlayerSummonKnownObjectNpcSkillEndCastSideEffectStep.SkillAttackManagerAfterUseSkill) is int afterUseIndex
		&& afterUseIndex > packetIndex;

	private int IndexOf(PlayerSummonKnownObjectNpcSkillEndCastSideEffectStep step)
	{
		for (var i = 0; i < OrderedSteps.Count; i++)
		{
			if (OrderedSteps[i] == step)
				return i;
		}

		return -1;
	}

	public static PlayerSummonKnownObjectNpcSkillEndCastSideEffectTrace FromActionResult(
		PlayerSummonKnownObjectNpcSkillActionResult? actionResult,
		PlayerSummonKnownObjectNpcSkillPostSpawnPreview? postSpawnPreview,
		bool isInstantSkill,
		bool skillMethodSendsCastSpellEnd,
		bool isNpcEffector,
		bool isCastSkillMethod)
	{
		if (actionResult?.Status != PlayerSummonKnownObjectNpcSkillActionResultStatus.UseSkill)
		{
			return new PlayerSummonKnownObjectNpcSkillEndCastSideEffectTrace(
				PlayerSummonKnownObjectNpcSkillEndCastSideEffectTraceStatus.NoEndCast,
				actionResult,
				postSpawnPreview);
		}

		var steps = new List<PlayerSummonKnownObjectNpcSkillEndCastSideEffectStep>
		{
			isInstantSkill
				? PlayerSummonKnownObjectNpcSkillEndCastSideEffectStep.ApplyEffectImmediately
				: PlayerSummonKnownObjectNpcSkillEndCastSideEffectStep.ScheduleApplyEffect,
		};

		if (skillMethodSendsCastSpellEnd)
			steps.Add(PlayerSummonKnownObjectNpcSkillEndCastSideEffectStep.SendCastSpellEnd);

		steps.Add(PlayerSummonKnownObjectNpcSkillEndCastSideEffectStep.EffectorAiOnEndUseSkill);

		if (isNpcEffector)
		{
			steps.Add(PlayerSummonKnownObjectNpcSkillEndCastSideEffectStep.NpcSkillEntryFireOnEndCastEvents);
			if (postSpawnPreview?.ShouldSpawnImmediately == true)
				steps.Add(PlayerSummonKnownObjectNpcSkillEndCastSideEffectStep.PostSpawnImmediate);
			if (postSpawnPreview?.ShouldScheduleSpawn == true)
				steps.Add(PlayerSummonKnownObjectNpcSkillEndCastSideEffectStep.PostSpawnSchedule);
			steps.Add(PlayerSummonKnownObjectNpcSkillEndCastSideEffectStep.SkillAttackManagerAfterUseSkill);
		}

		if (isCastSkillMethod)
			steps.Add(PlayerSummonKnownObjectNpcSkillEndCastSideEffectStep.NotifyEndSkillCastObservers);

		steps.Add(PlayerSummonKnownObjectNpcSkillEndCastSideEffectStep.InstanceHandlerOnEndCastSkill);

		return new PlayerSummonKnownObjectNpcSkillEndCastSideEffectTrace(
			PlayerSummonKnownObjectNpcSkillEndCastSideEffectTraceStatus.OrderedSideEffects,
			actionResult,
			postSpawnPreview,
			steps);
	}
}

public enum PlayerSummonKnownObjectNpcSkillEndCastSideEffectTraceStatus
{
	NoEndCast,
	OrderedSideEffects,
}

public enum PlayerSummonKnownObjectNpcSkillEndCastSideEffectStep
{
	ApplyEffectImmediately,
	ScheduleApplyEffect,
	SendCastSpellEnd,
	EffectorAiOnEndUseSkill,
	NpcSkillEntryFireOnEndCastEvents,
	PostSpawnImmediate,
	PostSpawnSchedule,
	SkillAttackManagerAfterUseSkill,
	NotifyEndSkillCastObservers,
	InstanceHandlerOnEndCastSkill,
}

public enum PlayerSummonKnownObjectNpcSkillActionResultStatus
{
	MissingPreview,
	NoAction,
	ResumeFightAfterInterruptedCast,
	TargetGiveUp,
	TargetTooFar,
	AfterUseSkill,
	UseSkill,
	UseSkillFailed,
}

public enum PlayerSummonKnownObjectNpcSkillAiEvent
{
	None,
	TargetGiveUp,
	TargetTooFar,
	AttackComplete,
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
	bool IsInRange = false,
	int? NpcId = null,
	int? ObjectId = null,
	bool IsVisible = false,
	bool IsCreature = false,
	bool IsDead = false,
	bool IsAboutToDie = false,
	bool IsSupport = false,
	bool IsFriend = false,
	int? HpPercentage = null,
	bool? GeoCanSee = null,
	IReadOnlyList<PlayerSummonKnownObjectNpcSkillCarvedSignetState>? CarvedSignets = null)
{
	public bool IsInAnyAbnormalState(PlayerAbnormalState state)
	{
		// Java parity: NpcSkillTemplateEntry.conditionReady delegates target states to EffectController.isInAnyAbnormalState.
		return state == PlayerAbnormalState.None ? AbnormalState == PlayerAbnormalState.None : (AbnormalState & state) != 0;
	}

	public static PlayerSummonKnownObjectNpcSkillConditionTarget WorldNpcPresence(int npcId)
	{
		return new PlayerSummonKnownObjectNpcSkillConditionTarget(
			PlayerSummonKnownObjectNpcSkillConditionTargetKind.Npc,
			NpcId: npcId);
	}

	public static PlayerSummonKnownObjectNpcSkillConditionTarget HelpFriendSearch()
	{
		return new PlayerSummonKnownObjectNpcSkillConditionTarget(
			PlayerSummonKnownObjectNpcSkillConditionTargetKind.Unknown);
	}

	public static PlayerSummonKnownObjectNpcSkillConditionTarget HelpFriendTarget(
		PlayerSummonKnownObjectNpcSkillHelpFriendCandidate candidate,
		PlayerSummonKnownObjectNpcSkillConditionMetadata conditionMetadata)
	{
		return new PlayerSummonKnownObjectNpcSkillConditionTarget(
			candidate.Kind,
			IsInRange: candidate.DistanceMeters <= conditionMetadata.RangeMeters,
			ObjectId: candidate.ObjectId,
			IsVisible: candidate.IsVisible,
			IsCreature: candidate.IsCreature,
			IsDead: candidate.IsDead,
			IsAboutToDie: candidate.IsAboutToDie,
			IsSupport: candidate.IsSupport,
			IsFriend: candidate.IsFriend,
			HpPercentage: candidate.HpPercentage,
			GeoCanSee: candidate.GeoCanSee);
	}
}

public sealed record PlayerSummonKnownObjectNpcSkillHelpFriendCandidate(
	int ObjectId,
	PlayerSummonKnownObjectNpcSkillConditionTargetKind Kind = PlayerSummonKnownObjectNpcSkillConditionTargetKind.Npc,
	bool IsVisible = true,
	bool IsCreature = true,
	bool IsDead = false,
	bool IsAboutToDie = false,
	bool IsSupport = false,
	bool IsFriend = false,
	int HpPercentage = 100,
	double DistanceMeters = 0,
	bool GeoCanSee = true);

public sealed record PlayerSummonKnownObjectNpcSkillCarvedSignetState(
	string Signet,
	int SkillLevel);

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
	PlayerSummonKnownObjectNpcSkillConditionTarget? Target = null,
	PlayerSummonKnownObjectNpcSkillHelpFriendCandidate? HelpFriendTarget = null)
{
	public bool WouldSetOwnerTarget => Condition == PlayerSummonKnownObjectNpcSkillCondition.HelpFriend
		&& Status == PlayerSummonKnownObjectNpcSkillConditionReadinessStatus.Ready
		&& HelpFriendTarget != null;

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
		PlayerSummonKnownObjectNpcSkillConditionTarget? target,
		PlayerSummonKnownObjectNpcSkillHelpFriendCandidate? helpFriendTarget = null)
	{
		return new PlayerSummonKnownObjectNpcSkillConditionReadiness(
			PlayerSummonKnownObjectNpcSkillConditionReadinessStatus.Ready,
			condition,
			target,
			helpFriendTarget);
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
