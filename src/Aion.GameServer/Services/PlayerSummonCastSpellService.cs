using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ClientPackets;

namespace Aion.GameServer.Services;

public sealed class PlayerSummonCastSpellService
{
	public PlayerSummonCastSpellResult Handle(Player player, CmSummonCastSpell packet)
	{
		// Java parity: network/aion/clientpackets/CM_SUMMON_CASTSPELL.runImpl -> Player.getSummonOrMercenary.
		var actorKind = player.GetSummonOrMercenaryKind(packet.SummonObjectId);
		if (actorKind is PlayerSummonOrMercenaryKind.None or PlayerSummonOrMercenaryKind.NonPetSummon)
			return PlayerSummonCastSpellResult.PetRequired(packet.SummonObjectId, actorKind);

		if (actorKind == PlayerSummonOrMercenaryKind.Mercenary)
		{
			var mercenaryTargetResolution = ResolveTarget(player, packet, actorKind);
			return mercenaryTargetResolution.Result
				?? PlayerSummonCastSpellResult.MercenaryReady(
					packet.SummonObjectId,
					packet.TargetObjectId,
					mercenaryTargetResolution.Target);
		}

		var targetResolution = ResolveTarget(player, packet, actorKind);
		if (targetResolution.Result != null)
			return targetResolution.Result;

		var order = player.RetrieveNextPetSkillOrder();
		if (order is null)
			return PlayerSummonCastSpellResult.NoQueuedOrder(packet.SummonObjectId, packet.TargetObjectId, targetResolution.Target);

		if (order.TargetObjectId != packet.TargetObjectId)
			return PlayerSummonCastSpellResult.TargetMismatch(packet.SummonObjectId, packet.TargetObjectId, order, targetResolution.Target);

		var skillMismatch = order.SkillId != packet.SkillId || order.SkillLevel != packet.SkillLevel;
		return PlayerSummonCastSpellResult.Executed(
			packet.SummonObjectId,
			packet.TargetObjectId,
			order,
			skillMismatch,
			packet.SkillId,
			packet.SkillLevel,
			targetResolution.Target);
	}

	private static PlayerSummonCastSpellTargetResolution ResolveTarget(
		Player player,
		CmSummonCastSpell packet,
		PlayerSummonOrMercenaryKind actorKind)
	{
		// Java parity: target can be the summon itself, otherwise it must be a Creature from summon.getKnownList().
		if (packet.TargetObjectId == packet.SummonObjectId)
			return new PlayerSummonCastSpellTargetResolution(
				new PlayerSummonCastSpellTarget(packet.TargetObjectId, PlayerSummonKnownObjectKind.Creature, IsActorSelfTarget: true),
				Result: null);

		if (!player.TryGetSummonKnownObjectKind(packet.TargetObjectId, out var targetKind))
			return new PlayerSummonCastSpellTargetResolution(
				Target: null,
				PlayerSummonCastSpellResult.UnknownTarget(packet.SummonObjectId, packet.TargetObjectId, actorKind));

		return targetKind == PlayerSummonKnownObjectKind.Creature
			? new PlayerSummonCastSpellTargetResolution(
				new PlayerSummonCastSpellTarget(packet.TargetObjectId, targetKind, IsActorSelfTarget: false),
				Result: null)
			: new PlayerSummonCastSpellTargetResolution(
				Target: null,
				PlayerSummonCastSpellResult.NonCreatureTarget(packet.SummonObjectId, packet.TargetObjectId, targetKind, actorKind));
	}
}

internal sealed record PlayerSummonCastSpellTargetResolution(
	PlayerSummonCastSpellTarget? Target,
	PlayerSummonCastSpellResult? Result);

public sealed record PlayerSummonCastSpellResult(
	PlayerSummonCastSpellStatus Status,
	int SummonObjectId,
	int TargetObjectId,
	PlayerSummonCastSpellTarget? ResolvedTarget = null,
	PlayerPetSkillOrder? ExecutedOrder = null,
	bool SkillMismatch = false,
	PlayerSummonCastSpellAudit? Audit = null,
	PlayerSummonCastSpellWarning? Warning = null,
	PlayerSummonCastSpellSkippedExecution? SkippedExecution = null,
	PlayerSummonOrMercenaryKind ActorKind = PlayerSummonOrMercenaryKind.PetSummon)
{
	public static PlayerSummonCastSpellResult PetRequired(int summonObjectId, PlayerSummonOrMercenaryKind actorKind)
	{
		return new PlayerSummonCastSpellResult(
			PlayerSummonCastSpellStatus.PetRequired,
			summonObjectId,
			0,
			ActorKind: actorKind);
	}

	public static PlayerSummonCastSpellResult MercenaryReady(
		int summonObjectId,
		int targetObjectId,
		PlayerSummonCastSpellTarget? resolvedTarget)
	{
		return new PlayerSummonCastSpellResult(
			PlayerSummonCastSpellStatus.MercenaryReady,
			summonObjectId,
			targetObjectId,
			resolvedTarget,
			ActorKind: PlayerSummonOrMercenaryKind.Mercenary);
	}

	public static PlayerSummonCastSpellResult NoQueuedOrder(
		int summonObjectId,
		int targetObjectId,
		PlayerSummonCastSpellTarget? resolvedTarget)
	{
		return new PlayerSummonCastSpellResult(
			PlayerSummonCastSpellStatus.NoQueuedOrder,
			summonObjectId,
			targetObjectId,
			resolvedTarget);
	}

	public static PlayerSummonCastSpellResult UnknownTarget(
		int summonObjectId,
		int targetObjectId,
		PlayerSummonOrMercenaryKind actorKind = PlayerSummonOrMercenaryKind.PetSummon)
	{
		return new PlayerSummonCastSpellResult(
			PlayerSummonCastSpellStatus.UnknownTarget,
			summonObjectId,
			targetObjectId,
			ActorKind: actorKind);
	}

	public static PlayerSummonCastSpellResult NonCreatureTarget(
		int summonObjectId,
		int targetObjectId,
		PlayerSummonKnownObjectKind targetKind,
		PlayerSummonOrMercenaryKind actorKind = PlayerSummonOrMercenaryKind.PetSummon)
	{
		// Java parity: CM_SUMMON_CASTSPELL audits non-null known-list objects that are not Creature targets.
		var audit = new PlayerSummonCastSpellAudit(PlayerSummonCastSpellAuditKind.WrongTarget, targetObjectId, targetKind);
		return new PlayerSummonCastSpellResult(
			PlayerSummonCastSpellStatus.NonCreatureTarget,
			summonObjectId,
			targetObjectId,
			Audit: audit,
			ActorKind: actorKind);
	}

	public static PlayerSummonCastSpellResult TargetMismatch(int summonObjectId, int targetObjectId, PlayerPetSkillOrder consumedOrder)
	{
		return TargetMismatch(summonObjectId, targetObjectId, consumedOrder, resolvedTarget: null);
	}

	public static PlayerSummonCastSpellResult TargetMismatch(
		int summonObjectId,
		int targetObjectId,
		PlayerPetSkillOrder consumedOrder,
		PlayerSummonCastSpellTarget? resolvedTarget)
	{
		var skippedExecution = PlayerSummonCastSpellSkippedExecution.TargetMismatch(
			consumedOrder.TargetObjectId,
			targetObjectId);
		return new PlayerSummonCastSpellResult(
			PlayerSummonCastSpellStatus.TargetMismatch,
			summonObjectId,
			targetObjectId,
			resolvedTarget,
			consumedOrder,
			SkippedExecution: skippedExecution);
	}

	public static PlayerSummonCastSpellResult Executed(
		int summonObjectId,
		int targetObjectId,
		PlayerPetSkillOrder order,
		bool skillMismatch,
		int packetSkillId,
		int packetSkillLevel,
		PlayerSummonCastSpellTarget? resolvedTarget)
	{
		var warning = skillMismatch
			? PlayerSummonCastSpellWarning.SkillMismatch(packetSkillId, packetSkillLevel, order.SkillId, order.SkillLevel)
			: null;
		return new PlayerSummonCastSpellResult(
			PlayerSummonCastSpellStatus.Executed,
			summonObjectId,
			targetObjectId,
			resolvedTarget,
			order,
			skillMismatch,
			Warning: warning);
	}
}

public enum PlayerSummonCastSpellStatus
{
	PetRequired,
	MercenaryReady,
	NoQueuedOrder,
	UnknownTarget,
	NonCreatureTarget,
	TargetMismatch,
	Executed,
}

public sealed record PlayerSummonCastSpellAudit(
	PlayerSummonCastSpellAuditKind Kind,
	int TargetObjectId,
	PlayerSummonKnownObjectKind TargetKind);

public enum PlayerSummonCastSpellAuditKind
{
	WrongTarget,
}

public sealed record PlayerSummonCastSpellTarget(
	int ObjectId,
	PlayerSummonKnownObjectKind Kind,
	bool IsActorSelfTarget);

public sealed record PlayerSummonCastSpellWarning(
	PlayerSummonCastSpellWarningKind Kind,
	int PacketSkillId,
	int PacketSkillLevel,
	int QueuedSkillId,
	int QueuedSkillLevel)
{
	public static PlayerSummonCastSpellWarning SkillMismatch(
		int packetSkillId,
		int packetSkillLevel,
		int queuedSkillId,
		int queuedSkillLevel)
	{
		// Java parity: CM_SUMMON_CASTSPELL logs when packet skill id/level differ from the queued SkillOrder.
		return new PlayerSummonCastSpellWarning(
			PlayerSummonCastSpellWarningKind.SkillMismatch,
			packetSkillId,
			packetSkillLevel,
			queuedSkillId,
			queuedSkillLevel);
	}
}

public enum PlayerSummonCastSpellWarningKind
{
	SkillMismatch,
}

public sealed record PlayerSummonCastSpellSkippedExecution(
	PlayerSummonCastSpellSkippedExecutionKind Kind,
	int QueuedTargetObjectId,
	int PacketTargetObjectId)
{
	public static PlayerSummonCastSpellSkippedExecution TargetMismatch(
		int queuedTargetObjectId,
		int packetTargetObjectId)
	{
		// Java parity: CM_SUMMON_CASTSPELL consumes the order but skips useSkill when order.target != resolved target.
		return new PlayerSummonCastSpellSkippedExecution(
			PlayerSummonCastSpellSkippedExecutionKind.TargetMismatch,
			queuedTargetObjectId,
			packetTargetObjectId);
	}
}

public enum PlayerSummonCastSpellSkippedExecutionKind
{
	TargetMismatch,
}

public sealed record PlayerSummonCastSpellConnectionResult(
	PlayerSummonCastSpellResult CastResult,
	PlayerSummonSkillExecutionResult? ExecutionResult,
	PlayerMercenarySkillExecutionResult? MercenaryExecutionResult = null);
