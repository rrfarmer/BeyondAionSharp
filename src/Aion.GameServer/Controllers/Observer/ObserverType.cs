namespace Aion.GameServer.Controllers.Observer;

/// <summary>
/// Bit-mask of creature events an observer subscribes to (plus compound masks).
/// Java parity: controllers/observer/ObserverType.
/// </summary>
/// <remarks>The enum value IS the observer mask (matching Java's per-constant observerMask).</remarks>
public enum ObserverType
{
    MOVE = 1,
    ATTACK = 1 << 1,
    ATTACKED = 1 << 2,
    EQUIP = 1 << 3,
    UNEQUIP = 1 << 4,
    STARTSKILLCAST = 1 << 5,
    DEATH = 1 << 6,
    DOT_ATTACKED = 1 << 7,
    ITEMUSE = 1 << 8,
    ABNORMALSETTED = 1 << 9,
    SUMMONRELEASE = 1 << 10,
    SIT = 1 << 11,
    HP_CHANGED = 1 << 12,
    ENDSKILLCAST = 1 << 13,
    BOOSTSKILLCOST = 1 << 14,

    EQUIP_UNEQUIP = EQUIP | UNEQUIP,
    ATTACK_DEFEND = ATTACK | ATTACKED,
    DOT_ATTACK_DEFEND = DOT_ATTACKED | ATTACK | ATTACKED,
    MOVE_OR_DIE = MOVE | DEATH,
    ALL = MOVE | ATTACK | ATTACKED | EQUIP | UNEQUIP | STARTSKILLCAST
        | DEATH | DOT_ATTACKED | ITEMUSE | ABNORMALSETTED | SUMMONRELEASE
        | SIT | HP_CHANGED | ENDSKILLCAST | BOOSTSKILLCOST,
}

public static class ObserverTypeExtensions
{
    // Java parity: matchesObserver(ObserverType) — true if this mask contains all of other's bits.
    public static bool MatchesObserver(this ObserverType self, ObserverType observerType) =>
        ((int)observerType & (int)self) == (int)observerType;
}
