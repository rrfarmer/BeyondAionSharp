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
			var mercenaryTargetValidation = ValidateTarget(player, packet, actorKind);
			return mercenaryTargetValidation ?? PlayerSummonCastSpellResult.MercenaryReady(packet.SummonObjectId, packet.TargetObjectId);
		}

		var targetValidation = ValidateTarget(player, packet, actorKind);
		if (targetValidation != null)
			return targetValidation;

		var order = player.RetrieveNextPetSkillOrder();
		if (order is null)
			return PlayerSummonCastSpellResult.NoQueuedOrder(packet.SummonObjectId, packet.TargetObjectId);

		if (order.TargetObjectId != packet.TargetObjectId)
			return PlayerSummonCastSpellResult.TargetMismatch(packet.SummonObjectId, packet.TargetObjectId, order);

		var skillMismatch = order.SkillId != packet.SkillId || order.SkillLevel != packet.SkillLevel;
		return PlayerSummonCastSpellResult.Executed(packet.SummonObjectId, packet.TargetObjectId, order, skillMismatch);
	}

	private static PlayerSummonCastSpellResult? ValidateTarget(
		Player player,
		CmSummonCastSpell packet,
		PlayerSummonOrMercenaryKind actorKind)
	{
		// Java parity: target can be the summon itself, otherwise it must be a Creature from summon.getKnownList().
		if (packet.TargetObjectId == packet.SummonObjectId)
			return null;

		if (!player.TryGetSummonKnownObjectKind(packet.TargetObjectId, out var targetKind))
			return PlayerSummonCastSpellResult.UnknownTarget(packet.SummonObjectId, packet.TargetObjectId, actorKind);

		return targetKind == PlayerSummonKnownObjectKind.Creature
			? null
			: PlayerSummonCastSpellResult.NonCreatureTarget(packet.SummonObjectId, packet.TargetObjectId, targetKind, actorKind);
	}
}

public sealed record PlayerSummonCastSpellResult(
	PlayerSummonCastSpellStatus Status,
	int SummonObjectId,
	int TargetObjectId,
	PlayerPetSkillOrder? ExecutedOrder = null,
	bool SkillMismatch = false,
	PlayerSummonCastSpellAudit? Audit = null,
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

	public static PlayerSummonCastSpellResult MercenaryReady(int summonObjectId, int targetObjectId)
	{
		return new PlayerSummonCastSpellResult(
			PlayerSummonCastSpellStatus.MercenaryReady,
			summonObjectId,
			targetObjectId,
			ActorKind: PlayerSummonOrMercenaryKind.Mercenary);
	}

	public static PlayerSummonCastSpellResult NoQueuedOrder(int summonObjectId, int targetObjectId)
	{
		return new PlayerSummonCastSpellResult(PlayerSummonCastSpellStatus.NoQueuedOrder, summonObjectId, targetObjectId);
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
		return new PlayerSummonCastSpellResult(
			PlayerSummonCastSpellStatus.TargetMismatch,
			summonObjectId,
			targetObjectId,
			consumedOrder);
	}

	public static PlayerSummonCastSpellResult Executed(
		int summonObjectId,
		int targetObjectId,
		PlayerPetSkillOrder order,
		bool skillMismatch)
	{
		return new PlayerSummonCastSpellResult(
			PlayerSummonCastSpellStatus.Executed,
			summonObjectId,
			targetObjectId,
			order,
			skillMismatch);
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

public sealed record PlayerSummonCastSpellConnectionResult(
	PlayerSummonCastSpellResult CastResult,
	PlayerSummonSkillExecutionResult? ExecutionResult,
	PlayerMercenarySkillExecutionResult? MercenaryExecutionResult = null);
