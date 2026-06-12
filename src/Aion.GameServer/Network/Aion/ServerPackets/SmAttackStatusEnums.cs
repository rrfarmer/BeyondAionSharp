namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>
/// Java parity: the nested enums of network/aion/serverpackets/SM_ATTACK_STATUS (TYPE, LOG).
/// Ported as standalone enums (the C# damage/resource-stat senders reference them by the bare
/// names SmAttackStatusType/SmAttackStatusLog rather than as nested types) with PascalCase member
/// names per C# convention; the integer values are verbatim from Java. Several Java aliases share
/// a value (Damage/Hp=7, DamageMp/AbsorbedMp=20, Fp/FpDamage=26) — C# permits duplicate-valued
/// enum members, so the aliases are preserved.
/// </summary>
public enum SmAttackStatusType
{
    Regular = 5,
    AbsorbedHp = 6,
    Damage = 7,
    Hp = 7,
    DelayDamage = 10,
    DamageMp = 20,
    AbsorbedMp = 20,
    Mp = 21,
    Fp = 26,
    FpDamage = 26,
}

/// <summary>Java parity: SmAttackStatus.LOG. Standalone enum, PascalCase names, Java values verbatim.</summary>
public enum SmAttackStatusLog
{
    SpellAttack = 1,
    Heal = 3,
    MpHeal = 4,
    SkillAttackDrainInstant = 23,
    SpellAttackDrainInstant = 24,
    Poison = 25,
    Bleed = 26,
    ProcAttackInstant = 93,
    DelayedSpellAttackInstant = 97,
    SpellAttackDrain = 132,
    FpHeal = 134,
    FpAttack = 137,
    MpAttack = 141,
    Regular = 191,
}
