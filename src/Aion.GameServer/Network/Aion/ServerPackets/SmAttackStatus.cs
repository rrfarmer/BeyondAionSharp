using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmAttackStatus : GameServerPacket
{
	public const int PacketOpCode = 5;

	private readonly int _creatureObjectId;
	private readonly SmAttackStatusType _type;
	private readonly int _skillId;
	private readonly int _value;
	private readonly int _hpOrMpPercentage;
	private readonly SmAttackStatusLog _log;

	public SmAttackStatus(
		int creatureObjectId,
		SmAttackStatusType type,
		int skillId,
		int value,
		int hpOrMpPercentage,
		SmAttackStatusLog log = SmAttackStatusLog.Regular)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_ATTACK_STATUS.writeImpl.
		_creatureObjectId = creatureObjectId;
		_type = type;
		_skillId = skillId;
		_value = value;
		_hpOrMpPercentage = Math.Clamp(hpOrMpPercentage, 0, 100);
		_log = log;
	}

	public int CreatureObjectId => _creatureObjectId;

	public SmAttackStatusType Type => _type;

	public int SkillId => _skillId;

	public int Value => _value;

	public int HpOrMpPercentage => _hpOrMpPercentage;

	public SmAttackStatusLog Log => _log;

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		buffer.WriteD(_creatureObjectId);
		buffer.WriteD(UsesNegativeValue(_type) ? -_value : _value);
		buffer.WriteC((int)_type);
		buffer.WriteC(_hpOrMpPercentage);
		buffer.WriteH(_skillId);
		buffer.WriteH((int)_log);
	}

	private static bool UsesNegativeValue(SmAttackStatusType type)
	{
		return type
			is SmAttackStatusType.Damage
			or SmAttackStatusType.DelayDamage
			or SmAttackStatusType.FallDamage
			or SmAttackStatusType.FpDamage
			or SmAttackStatusType.MagicCounterAttack
			or SmAttackStatusType.DispelBuffCounterAttack
			or SmAttackStatusType.UsedHp
			or SmAttackStatusType.Drowning
			or SmAttackStatusType.UsedMp
			or SmAttackStatusType.DamageMp;
	}
}

public enum SmAttackStatusType
{
	Type1 = 1,
	Type2 = 2,
	NaturalHp = 3,
	UsedHp = 4,
	Regular = 5,
	AbsorbedHp = 6,
	Damage = 7,
	Hp = 7,
	ProtectDamage = 8,
	Type9 = 9,
	DelayDamage = 10,
	Type11 = 11,
	Type12 = 12,
	Drowning = 12,
	HpAfterResurrection = 13,
	Type14 = 14,
	MagicCounterAttack = 15,
	DispelBuffCounterAttack = 16,
	FallDamage = 17,
	DoorRepair = 18,
	HealMp = 19,
	DamageMp = 20,
	AbsorbedMp = 20,
	Mp = 21,
	NaturalMp = 22,
	UsedMp = 23,
	FpRings = 24,
	Type25 = 25,
	Fp = 26,
	FpDamage = 26,
	NaturalFp = 27,
}

public enum SmAttackStatusLog
{
	SpellAttack = 1,
	Heal = 3,
	MpHeal = 4,
	CaseHeal = 21,
	SkillAttackDrainInstant = 23,
	SpellAttackDrainInstant = 24,
	Poison = 25,
	Bleed = 26,
	ProcAttackInstant = 93,
	DelayedSpellAttackInstant = 97,
	MagicCounterAttack = 112,
	SpellAttackDrain = 132,
	FpHeal = 134,
	FpAttack = 137,
	MpAttack = 141,
	Regular = 191,
}
