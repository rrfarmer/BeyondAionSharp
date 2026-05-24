using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ClientPackets;

namespace Aion.GameServer.Services;

public sealed class PlayerSummonSkillExecutionService
{
	public PlayerSummonSkillExecutionResult ValidateExecution(
		Player player,
		PlayerPetSkillOrder order,
		PetSkillTable petSkills)
	{
		// Java parity: controllers/SummonController.useSkill(SkillOrder) petHasSkill guard before SkillEngine invocation.
		if (!player.HasPetSummon || player.PetSummonNpcId == 0)
			return PlayerSummonSkillExecutionResult.MissingSummon(order);

		if (!petSkills.PetHasSkill(player.PetSummonNpcId, order.SkillId))
			return PlayerSummonSkillExecutionResult.InvalidPetSkill(player.PetSummonNpcId, order);

		return PlayerSummonSkillExecutionResult.WouldInvokeSkillEngine(
			player.PetSummonNpcId,
			order,
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
	IReadOnlyList<PlayerSummonSkillExecutionAction>? PlannedActions = null)
{
	public IReadOnlyList<PlayerSummonSkillExecutionAction> Actions => PlannedActions ?? Array.Empty<PlayerSummonSkillExecutionAction>();

	public static PlayerSummonSkillExecutionResult MissingSummon(PlayerPetSkillOrder order)
	{
		return new PlayerSummonSkillExecutionResult(PlayerSummonSkillExecutionStatus.MissingSummon, 0, order);
	}

	public static PlayerSummonSkillExecutionResult InvalidPetSkill(int petSummonNpcId, PlayerPetSkillOrder order)
	{
		return new PlayerSummonSkillExecutionResult(PlayerSummonSkillExecutionStatus.InvalidPetSkill, petSummonNpcId, order);
	}

	public static PlayerSummonSkillExecutionResult WouldInvokeSkillEngine(
		int petSummonNpcId,
		PlayerPetSkillOrder order,
		IReadOnlyList<PlayerSummonSkillExecutionAction> plannedActions)
	{
		return new PlayerSummonSkillExecutionResult(
			PlayerSummonSkillExecutionStatus.WouldInvokeSkillEngine,
			petSummonNpcId,
			order,
			plannedActions);
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

public sealed record PlayerMercenarySkillExecutionResult(
	PlayerMercenarySkillExecutionStatus Status,
	int MercenaryNpcId,
	int SkillId,
	int SkillLevel,
	int TargetObjectId,
	PlayerSummonCastSpellTarget? ResolvedTarget = null,
	IReadOnlyList<PlayerMercenarySkillExecutionAction>? PlannedActions = null,
	PlayerMercenarySkillExecutionAudit? Audit = null)
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
		int mercenaryNpcId,
		int skillId,
		int skillLevel,
		int targetObjectId,
		PlayerSummonCastSpellTarget? resolvedTarget)
	{
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
			]);
	}
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
