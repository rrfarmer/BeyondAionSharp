using Aion.GameServer.Model.Templates;

namespace Aion.GameServer.Model;

/// <summary>
/// Race identifiers for players and NPCs.
/// Java parity: model/Race (@XmlEnum, implements L10n).
/// </summary>
/// <remarks>
/// SCREAMING_SNAKE_CASE member names are preserved so @XmlEnum data (and DB values) deserialize
/// identically to Java (data backward-compat rule). Enum value = Java raceId (not ordinal).
/// Java declares both LIVINGWATER(28) and DEFORM(28); in C# DEFORM is an alias of value 28
/// (LIVINGWATER declared first, matching Java order). C# enums cannot implement interfaces, so the
/// L10n contract is provided via <see cref="RaceExtensions"/> (closest-to-1:1 per doctrine).
/// </remarks>
public enum Race
{
    // Playable races
    ELYOS = 0,
    ASMODIANS = 1,
    // Npc races
    LYCAN = 2,
    CONSTRUCT = 3,
    CARRIER = 4,
    DRAKAN = 5,
    LIZARDMAN = 6,
    TELEPORTER = 7,
    NAGA = 8,
    BROWNIE = 9,
    KRALL = 10,
    SHULACK = 11,
    BARRIER = 12,
    PC_LIGHT_CASTLE_DOOR = 13,
    PC_DARK_CASTLE_DOOR = 14,
    DRAGON_CASTLE_DOOR = 15,
    GCHIEF_LIGHT = 16,
    GCHIEF_DARK = 17,
    DRAGON = 18,
    OUTSIDER = 19,
    RATMAN = 20,
    DEMIHUMANOID = 21,
    UNDEAD = 22,
    BEAST = 23,
    MAGICALMONSTER = 24,
    ELEMENTAL = 25,
    LIVINGWATER = 28,
    // Special races
    NONE = 26,
    PC_ALL = 27,
    DEFORM = 28, // Java: DEFORM(28) — shares value 28 with LIVINGWATER (alias)
    // 2.6
    NEUT = 29,
    // 2.7
    GHENCHMAN_LIGHT = 30,
    GHENCHMAN_DARK = 31,
    // 3.0
    EVENT_TOWER_DARK = 32,
    EVENT_TOWER_LIGHT = 33,
    GOBLIN = 34,
    TRICODARK = 35,
    NPC = 36,
    // 3.5
    LIGHT = 37,
    DARK = 38,
    WORLD_EVENT_DEFTOWER = 39,
    // 4.3
    ORC = 40,
    DRAGONET = 41,
    SIEGEDRAKAN = 42,
    GCHIEF_DRAGON = 43,
    WORLD_EVENT_BONFIRE = 44,
    // 4.7.5
    DOOR_KILLER = 45,
    // 4.8.0
    LF5_Q_ITEM = 46,
}

/// <summary>
/// Java parity: model/Race instance methods + L10n.
/// </summary>
public static class RaceExtensions
{
    // Java parity: getRaceId() — returns raceId (= enum int value)
    public static int GetRaceId(this Race race) => (int)race;

    // Java parity: getL10nId() — only ELYOS/ASMODIANS have a non-zero l10n id
    public static int GetL10nId(this Race race) => race switch
    {
        Race.ELYOS => 900240,
        Race.ASMODIANS => 900241,
        _ => 0,
    };

    // Java parity: L10n default getL10n() via ChatUtil.l10n(l10nId)
    public static string? GetL10n(this Race race) => race.GetL10nId() == 0 ? null : Utils.ChatUtil.L10n(race.GetL10nId());

    // Java parity: isAsmoOrEly()
    public static bool IsAsmoOrEly(this Race race) => race == Race.ELYOS || race == Race.ASMODIANS;

    // Java parity: isPlayerRace()
    public static bool IsPlayerRace(this Race race) => race.IsAsmoOrEly() || race == Race.PC_ALL;

    // Java parity: static getRaceByString(String fieldName) — iterates all constant names (incl. the
    // LIVINGWATER/DEFORM aliases); returns null if not found.
    public static Race? GetRaceByString(string fieldName)
    {
        foreach (string name in Enum.GetNames<Race>())
        {
            if (name.Equals(fieldName, StringComparison.Ordinal))
                return Enum.Parse<Race>(name);
        }
        return null;
    }
}
