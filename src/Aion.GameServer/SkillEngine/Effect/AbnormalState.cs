namespace Aion.GameServer.SkillEngine.Effect;

/// <summary>
/// Bit-flag altered/abnormal states plus compound masks used by effect/skill checks.
/// Java parity: skillengine/effect/AbnormalState (@XmlEnum).
/// </summary>
/// <remarks>
/// Underlying type is <c>int</c> to match Java exactly, including SANCTUARY = 1 &lt;&lt; 31
/// (int.MinValue) and the OR-combined compound states. SCREAMING_SNAKE_CASE preserved for XML.
/// </remarks>
public enum AbnormalState
{
    NONE = 0,
    POISON = 1 << 0,
    BLEED = 1 << 1,
    PARALYZE = 1 << 2,
    SLEEP = 1 << 3,
    ROOT = 1 << 4,
    BLIND = 1 << 5,
    CHARM = 1 << 6,
    DISEASE = 1 << 7,
    SILENCE = 1 << 8,
    FEAR = 1 << 9,
    CURSE = 1 << 10,
    CONFUSE = 1 << 11,
    STUN = 1 << 12,
    PETRIFICATION = 1 << 13,
    STUMBLE = 1 << 14,
    STAGGER = 1 << 15, // knockback
    OPENAERIAL = 1 << 16,
    SNARE = 1 << 17,
    SLOW = 1 << 18,
    SPIN = 1 << 19,
    BIND = 1 << 20,
    DEFORM = 1 << 21,
    PULLED = 1 << 22,
    NOFLY = 1 << 23,
    SIMPLE_MOVE_BACK = 1 << 24,
    STUNLIKE = 1 << 25,
    CANT_MOVE_OR_ATTACK = 1 << 26,
    UNK = 1 << 27,
    UNK_2 = 1 << 28,
    HIDE = 1 << 29,
    INVULNERABLE_WING = 1 << 30,
    SANCTUARY = 1 << 31,

    // Compound abnormal states
    CANT_ATTACK_STATE = SPIN | SLEEP | STUN | STUMBLE | STAGGER | OPENAERIAL | PARALYZE | FEAR | PULLED | SANCTUARY | CONFUSE,
    STANCE_OFF = SPIN | STUN | STUMBLE | STAGGER | OPENAERIAL | PARALYZE | FEAR | PULLED | SANCTUARY | CONFUSE,
    CANT_MOVE_STATE = SPIN | ROOT | SLEEP | STUMBLE | STUN | STAGGER | OPENAERIAL | PARALYZE | PULLED | SANCTUARY,
    DISMOUNT_RIDE = SPIN | ROOT | SLEEP | STUMBLE | STUN | STAGGER | OPENAERIAL | PARALYZE | PULLED | FEAR | SNARE | DEFORM | CONFUSE,
    AUTOMATICALLY_STANDUP = PARALYZE | SLEEP | FEAR | STUN | STAGGER | OPENAERIAL | SPIN | DEFORM | PULLED | CONFUSE,
    ANY_STUN = SPIN | STUN | STUMBLE | STAGGER,
}

public static class AbnormalStateExtensions
{
    // Java parity: getId()
    public static int GetId(this AbnormalState state) => (int)state;
}
