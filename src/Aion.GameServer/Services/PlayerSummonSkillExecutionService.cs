using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ClientPackets;

namespace Aion.GameServer.Services;

public sealed class PlayerSummonSkillExecutionService
{
	public PlayerSummonSkillInvocationExecutionResult PlanInvocationExecution(
		PlayerSummonSkillInvocationPlan? invocationPlan,
		SkillTemplateTable skillTemplates)
	{
		if (invocationPlan == null)
			return PlayerSummonSkillInvocationExecutionResult.MissingPlan();

		// Java parity: SkillEngine.getSkill returns null when DataManager.SKILL_DATA has no template.
		var skillTemplate = skillTemplates.GetSkillTemplate(invocationPlan.SkillId);
		if (skillTemplate == null)
			return PlayerSummonSkillInvocationExecutionResult.MissingSkillTemplate(invocationPlan);

		return PlayerSummonSkillInvocationExecutionResult.WouldUseSkill(invocationPlan, skillTemplate.SkillId);
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
		int skillTemplateId)
	{
		return new PlayerSummonSkillInvocationExecutionResult(
			PlayerSummonSkillInvocationExecutionStatus.WouldUseSkill,
			invocationPlan,
			skillTemplateId,
			CreateActions(invocationPlan));
	}

	private static IReadOnlyList<PlayerSummonSkillInvocationExecutionAction> CreateActions(PlayerSummonSkillInvocationPlan invocationPlan)
	{
		if (invocationPlan.ActorKind == PlayerSummonSkillInvocationActorKind.Mercenary)
		{
			return
			[
				PlayerSummonSkillInvocationExecutionAction.SetTarget,
				PlayerSummonSkillInvocationExecutionAction.ResolveSkillTemplate,
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
	WouldUseSkill,
}

public enum PlayerSummonSkillInvocationExecutionAction
{
	ResolveSkillTemplate,
	SetTarget,
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
