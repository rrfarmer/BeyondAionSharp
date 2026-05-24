using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ClientPackets;

namespace Aion.GameServer.Services;

public sealed class PlayerSummonCastSpellService
{
	public PlayerSummonCastSpellResult Handle(Player player, CmSummonCastSpell packet)
	{
		// Java parity: network/aion/clientpackets/CM_SUMMON_CASTSPELL.runImpl represented pet-summon order path.
		if (!player.HasPetSummon || player.PetSummonObjectId == 0 || packet.SummonObjectId != player.PetSummonObjectId)
			return PlayerSummonCastSpellResult.PetRequired(packet.SummonObjectId);

		var targetValidation = ValidateTarget(player, packet);
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

	private static PlayerSummonCastSpellResult? ValidateTarget(Player player, CmSummonCastSpell packet)
	{
		// Java parity: target can be the summon itself, otherwise it must be a Creature from summon.getKnownList().
		if (packet.TargetObjectId == player.PetSummonObjectId)
			return null;

		if (!player.TryGetSummonKnownObjectKind(packet.TargetObjectId, out var targetKind))
			return PlayerSummonCastSpellResult.UnknownTarget(packet.SummonObjectId, packet.TargetObjectId);

		return targetKind == PlayerSummonKnownObjectKind.Creature
			? null
			: PlayerSummonCastSpellResult.NonCreatureTarget(packet.SummonObjectId, packet.TargetObjectId);
	}
}

public sealed record PlayerSummonCastSpellResult(
	PlayerSummonCastSpellStatus Status,
	int SummonObjectId,
	int TargetObjectId,
	PlayerPetSkillOrder? ExecutedOrder = null,
	bool SkillMismatch = false)
{
	public static PlayerSummonCastSpellResult PetRequired(int summonObjectId)
	{
		return new PlayerSummonCastSpellResult(PlayerSummonCastSpellStatus.PetRequired, summonObjectId, 0);
	}

	public static PlayerSummonCastSpellResult NoQueuedOrder(int summonObjectId, int targetObjectId)
	{
		return new PlayerSummonCastSpellResult(PlayerSummonCastSpellStatus.NoQueuedOrder, summonObjectId, targetObjectId);
	}

	public static PlayerSummonCastSpellResult UnknownTarget(int summonObjectId, int targetObjectId)
	{
		return new PlayerSummonCastSpellResult(PlayerSummonCastSpellStatus.UnknownTarget, summonObjectId, targetObjectId);
	}

	public static PlayerSummonCastSpellResult NonCreatureTarget(int summonObjectId, int targetObjectId)
	{
		return new PlayerSummonCastSpellResult(PlayerSummonCastSpellStatus.NonCreatureTarget, summonObjectId, targetObjectId);
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
	NoQueuedOrder,
	UnknownTarget,
	NonCreatureTarget,
	TargetMismatch,
	Executed,
}

public sealed record PlayerSummonCastSpellConnectionResult(
	PlayerSummonCastSpellResult CastResult,
	PlayerSummonSkillExecutionResult? ExecutionResult);
