using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ClientPackets;

namespace Aion.GameServer.Services;

public sealed class PlayerSummonSkillExecutionService
{
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
