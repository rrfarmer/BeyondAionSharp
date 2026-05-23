using System.Collections.Concurrent;

namespace Aion.GameServer.Services;

public sealed class WorldNpcCastingInterruptService
{
	private readonly ConcurrentDictionary<int, WorldNpcCastingSkill> _castingSkills = new();

	public WorldNpcCastingSkill SetCastingSkill(int npcObjectId, WorldNpcCastingSkill skill)
	{
		// Java parity: model/gameobjects/Creature.getCastingSkill exposes the current skill checked by CreatureController.onAttack.
		_castingSkills[npcObjectId] = skill;
		return skill;
	}

	public bool TryGetCastingSkill(int npcObjectId, out WorldNpcCastingSkill? skill)
	{
		return _castingSkills.TryGetValue(npcObjectId, out skill);
	}

	public WorldNpcCastingInterruptResult EvaluateIncomingDamage(
		int npcObjectId,
		int attackerObjectId,
		int damage,
		bool notifyAttack,
		int maxHp,
		WorldNpcCastingInterruptOptions? options = null)
	{
		// Java parity: controllers/CreatureController.onAttack checks casting interruption before attacked observers.
		if (!_castingSkills.TryGetValue(npcObjectId, out var skill))
			return WorldNpcCastingInterruptResult.NoCastingSkill(npcObjectId, attackerObjectId);

		var normalizedDamage = Math.Max(0, damage);
		if (normalizedDamage == 0 || !notifyAttack)
			return WorldNpcCastingInterruptResult.NotChecked(npcObjectId, attackerObjectId, skill);

		if (skill.Method == WorldNpcSkillMethod.Item)
			return Cancel(npcObjectId, attackerObjectId, skill, WorldNpcCastingInterruptStatus.ItemSkillCanceled, cancelChance: null, chanceRoll: null);

		if (skill.CancelRate >= 99999)
			return Cancel(npcObjectId, attackerObjectId, skill, WorldNpcCastingInterruptStatus.GuaranteedCanceled, cancelChance: null, chanceRoll: null);

		if (skill.CancelRate <= 0)
			return WorldNpcCastingInterruptResult.NotCancelable(npcObjectId, attackerObjectId, skill);

		if (skill.OwnerIsBoss)
			return WorldNpcCastingInterruptResult.BossProtected(npcObjectId, attackerObjectId, skill);

		var cancelChance = CalculateJavaCancelChance(normalizedDamage, maxHp, skill.OwnerConcentration, skill.CancelRate);
		var chanceRoll = options?.ChanceRoll;
		if (chanceRoll == null)
			return WorldNpcCastingInterruptResult.ChanceNotResolved(npcObjectId, attackerObjectId, skill, cancelChance);

		return chanceRoll.Value < cancelChance
			? Cancel(npcObjectId, attackerObjectId, skill, WorldNpcCastingInterruptStatus.ChanceCanceled, cancelChance, chanceRoll.Value)
			: WorldNpcCastingInterruptResult.ChanceResisted(npcObjectId, attackerObjectId, skill, cancelChance, chanceRoll.Value);
	}

	public void Clear(int npcObjectId)
	{
		_castingSkills.TryRemove(npcObjectId, out _);
	}

	private WorldNpcCastingInterruptResult Cancel(
		int npcObjectId,
		int attackerObjectId,
		WorldNpcCastingSkill skill,
		WorldNpcCastingInterruptStatus status,
		int? cancelChance,
		int? chanceRoll)
	{
		_castingSkills.TryRemove(npcObjectId, out _);
		return new WorldNpcCastingInterruptResult(
			status,
			npcObjectId,
			attackerObjectId,
			skill,
			Canceled: true,
			cancelChance,
			chanceRoll);
	}

	private static int CalculateJavaCancelChance(int damage, int maxHp, int concentration, int cancelRate)
	{
		if (maxHp <= 0)
			return 0;

		var raw = ((7d * (damage / (double)maxHp) * 100d) - concentration / 2d) * (cancelRate / 100d);
		return JavaRound(raw);
	}

	private static int JavaRound(double value)
	{
		// Java parity: Math.round(float) is equivalent to floor(value + 0.5), not banker's rounding.
		return (int)Math.Floor(value + 0.5d);
	}
}

public sealed record WorldNpcCastingSkill(
	int SkillId,
	WorldNpcSkillMethod Method,
	int CancelRate,
	int OwnerConcentration = 0,
	bool OwnerIsBoss = false);

public sealed record WorldNpcCastingInterruptOptions(int? ChanceRoll = null);

public sealed record WorldNpcCastingInterruptResult(
	WorldNpcCastingInterruptStatus Status,
	int NpcObjectId,
	int AttackerObjectId,
	WorldNpcCastingSkill? Skill,
	bool Canceled,
	int? CancelChance,
	int? ChanceRoll)
{
	public static WorldNpcCastingInterruptResult NoCastingSkill(int npcObjectId, int attackerObjectId)
	{
		return new WorldNpcCastingInterruptResult(
			WorldNpcCastingInterruptStatus.NoCastingSkill,
			npcObjectId,
			attackerObjectId,
			Skill: null,
			Canceled: false,
			CancelChance: null,
			ChanceRoll: null);
	}

	public static WorldNpcCastingInterruptResult NotChecked(int npcObjectId, int attackerObjectId, WorldNpcCastingSkill skill)
	{
		return new WorldNpcCastingInterruptResult(
			WorldNpcCastingInterruptStatus.NotChecked,
			npcObjectId,
			attackerObjectId,
			skill,
			Canceled: false,
			CancelChance: null,
			ChanceRoll: null);
	}

	public static WorldNpcCastingInterruptResult NotCancelable(int npcObjectId, int attackerObjectId, WorldNpcCastingSkill skill)
	{
		return new WorldNpcCastingInterruptResult(
			WorldNpcCastingInterruptStatus.NotCancelable,
			npcObjectId,
			attackerObjectId,
			skill,
			Canceled: false,
			CancelChance: null,
			ChanceRoll: null);
	}

	public static WorldNpcCastingInterruptResult BossProtected(int npcObjectId, int attackerObjectId, WorldNpcCastingSkill skill)
	{
		return new WorldNpcCastingInterruptResult(
			WorldNpcCastingInterruptStatus.BossProtected,
			npcObjectId,
			attackerObjectId,
			skill,
			Canceled: false,
			CancelChance: null,
			ChanceRoll: null);
	}

	public static WorldNpcCastingInterruptResult ChanceNotResolved(
		int npcObjectId,
		int attackerObjectId,
		WorldNpcCastingSkill skill,
		int cancelChance)
	{
		return new WorldNpcCastingInterruptResult(
			WorldNpcCastingInterruptStatus.ChanceNotResolved,
			npcObjectId,
			attackerObjectId,
			skill,
			Canceled: false,
			cancelChance,
			ChanceRoll: null);
	}

	public static WorldNpcCastingInterruptResult ChanceResisted(
		int npcObjectId,
		int attackerObjectId,
		WorldNpcCastingSkill skill,
		int cancelChance,
		int chanceRoll)
	{
		return new WorldNpcCastingInterruptResult(
			WorldNpcCastingInterruptStatus.ChanceResisted,
			npcObjectId,
			attackerObjectId,
			skill,
			Canceled: false,
			cancelChance,
			chanceRoll);
	}
}

public enum WorldNpcSkillMethod
{
	Cast,
	Item,
}

public enum WorldNpcCastingInterruptStatus
{
	NoCastingSkill,
	NotChecked,
	NotCancelable,
	ItemSkillCanceled,
	GuaranteedCanceled,
	BossProtected,
	ChanceNotResolved,
	ChanceResisted,
	ChanceCanceled,
}
