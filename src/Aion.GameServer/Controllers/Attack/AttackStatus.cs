namespace Aion.GameServer.Controllers.Attack;

/// <summary>
/// Attack outcome status (dodge/parry/block/resist/crit + offhand variants); enum value = client id.
/// Java parity: controllers/attack/AttackStatus.
/// </summary>
public enum AttackStatus
{
    DODGE = 0,
    OFFHAND_DODGE = 1,
    PARRY = 2,
    OFFHAND_PARRY = 3,
    BLOCK = 4,
    OFFHAND_BLOCK = 5,
    RESIST = 6,
    OFFHAND_RESIST = 7,
    BUF = 8, // ??
    OFFHAND_BUF = 9,
    NORMALHIT = 10,
    OFFHAND_NORMALHIT = 11,
    CRITICAL_DODGE = -64,
    CRITICAL_PARRY = -62,
    CRITICAL_BLOCK = -60,
    CRITICAL_RESIST = -58,
    CRITICAL = -54,
    OFFHAND_CRITICAL_DODGE = -47,
    OFFHAND_CRITICAL_PARRY = -45,
    OFFHAND_CRITICAL_BLOCK = -43,
    OFFHAND_CRITICAL_RESIST = -41,
    OFFHAND_CRITICAL = -37,
}

public static class AttackStatusExtensions
{
    private static readonly HashSet<AttackStatus> CounterSkill = new()
    {
        AttackStatus.DODGE, AttackStatus.OFFHAND_DODGE, AttackStatus.PARRY, AttackStatus.OFFHAND_PARRY,
        AttackStatus.BLOCK, AttackStatus.OFFHAND_BLOCK, AttackStatus.RESIST, AttackStatus.OFFHAND_RESIST,
        AttackStatus.CRITICAL_DODGE, AttackStatus.CRITICAL_PARRY, AttackStatus.CRITICAL_BLOCK, AttackStatus.CRITICAL_RESIST,
        AttackStatus.OFFHAND_CRITICAL_DODGE, AttackStatus.OFFHAND_CRITICAL_PARRY, AttackStatus.OFFHAND_CRITICAL_BLOCK, AttackStatus.OFFHAND_CRITICAL_RESIST,
    };

    private static readonly HashSet<AttackStatus> Critical = new()
    {
        AttackStatus.CRITICAL_DODGE, AttackStatus.CRITICAL_PARRY, AttackStatus.CRITICAL_BLOCK, AttackStatus.CRITICAL_RESIST,
        AttackStatus.CRITICAL, AttackStatus.OFFHAND_CRITICAL_DODGE, AttackStatus.OFFHAND_CRITICAL_PARRY,
        AttackStatus.OFFHAND_CRITICAL_BLOCK, AttackStatus.OFFHAND_CRITICAL_RESIST, AttackStatus.OFFHAND_CRITICAL,
    };

    // Java parity: final getId()
    public static int GetId(this AttackStatus status) => (int)status;

    // Java parity: final isCounterSkill()
    public static bool IsCounterSkill(this AttackStatus status) => CounterSkill.Contains(status);

    // Java parity: final isCritical()
    public static bool IsCritical(this AttackStatus status) => Critical.Contains(status);

    // Java parity: static getOffHandStats(AttackStatus)
    public static AttackStatus GetOffHandStats(AttackStatus mainHandStatus) => mainHandStatus switch
    {
        AttackStatus.DODGE => AttackStatus.OFFHAND_DODGE,
        AttackStatus.PARRY => AttackStatus.OFFHAND_PARRY,
        AttackStatus.BLOCK => AttackStatus.OFFHAND_BLOCK,
        AttackStatus.RESIST => AttackStatus.OFFHAND_RESIST,
        AttackStatus.BUF => AttackStatus.OFFHAND_BUF,
        AttackStatus.NORMALHIT => AttackStatus.OFFHAND_NORMALHIT,
        AttackStatus.CRITICAL => AttackStatus.OFFHAND_CRITICAL,
        AttackStatus.CRITICAL_DODGE => AttackStatus.OFFHAND_CRITICAL_DODGE,
        AttackStatus.CRITICAL_PARRY => AttackStatus.OFFHAND_CRITICAL_PARRY,
        AttackStatus.CRITICAL_BLOCK => AttackStatus.OFFHAND_CRITICAL_BLOCK,
        AttackStatus.CRITICAL_RESIST => AttackStatus.OFFHAND_CRITICAL_RESIST,
        _ => throw new ArgumentException("Invalid mainHandStatus " + mainHandStatus),
    };

    // Java parity: static getBaseStatus(AttackStatus)
    public static AttackStatus GetBaseStatus(AttackStatus status) => status switch
    {
        AttackStatus.DODGE or AttackStatus.CRITICAL_DODGE or AttackStatus.OFFHAND_DODGE or AttackStatus.OFFHAND_CRITICAL_DODGE => AttackStatus.DODGE,
        AttackStatus.RESIST or AttackStatus.CRITICAL_RESIST or AttackStatus.OFFHAND_RESIST or AttackStatus.OFFHAND_CRITICAL_RESIST => AttackStatus.RESIST,
        AttackStatus.PARRY or AttackStatus.CRITICAL_PARRY or AttackStatus.OFFHAND_PARRY or AttackStatus.OFFHAND_CRITICAL_PARRY => AttackStatus.PARRY,
        AttackStatus.BLOCK or AttackStatus.CRITICAL_BLOCK or AttackStatus.OFFHAND_BLOCK or AttackStatus.OFFHAND_CRITICAL_BLOCK => AttackStatus.BLOCK,
        _ => status,
    };

    // Java parity: static getCriticalStatusFor(AttackStatus)
    public static AttackStatus GetCriticalStatusFor(AttackStatus status) => status switch
    {
        AttackStatus.DODGE => AttackStatus.CRITICAL_DODGE,
        AttackStatus.OFFHAND_DODGE => AttackStatus.OFFHAND_CRITICAL_DODGE,
        AttackStatus.PARRY => AttackStatus.CRITICAL_PARRY,
        AttackStatus.OFFHAND_PARRY => AttackStatus.OFFHAND_CRITICAL_PARRY,
        AttackStatus.BLOCK => AttackStatus.CRITICAL_BLOCK,
        AttackStatus.OFFHAND_BLOCK => AttackStatus.OFFHAND_CRITICAL_BLOCK,
        AttackStatus.NORMALHIT => AttackStatus.CRITICAL,
        AttackStatus.OFFHAND_NORMALHIT => AttackStatus.OFFHAND_CRITICAL,
        _ => status,
    };
}
