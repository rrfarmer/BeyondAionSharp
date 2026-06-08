namespace Aion.GameServer.SkillEngine.Model;

/// <summary>
/// Buff/debuff slot a skill occupies on the target.
/// Java parity: skillengine/model/SkillTargetSlot (@XmlType("TargetSlot") @XmlEnum).
/// </summary>
public enum SkillTargetSlot
{
    BUFF = 1,
    DEBUFF = 2,
    CHANT = 4,
    SPEC = 8,
    SPEC2 = 16, // soul sickness
    BOOST = 32,
    NOSHOW = 64,
    NONE = 128,
}

public static class SkillTargetSlotExtensions
{
    // Java parity: public static final int FULLSLOTS = 127;
    public const int FULLSLOTS = 127;

    // Java parity: getId()
    public static int GetId(this SkillTargetSlot slot) => (int)slot;

    // Java parity: static of(DispelSlotType) — returns null for unmapped values.
    public static SkillTargetSlot? Of(DispelSlotType dispelSlotType) => dispelSlotType switch
    {
        DispelSlotType.BUFF => SkillTargetSlot.BUFF,
        DispelSlotType.DEBUFF => SkillTargetSlot.DEBUFF,
        DispelSlotType.SPECIAL2 => SkillTargetSlot.SPEC2,
        _ => null,
    };
}
