using Aion.GameServer.Model.Templates;

namespace Aion.GameServer.Model;

/// <summary>
/// Race identifiers for players and NPCs.
/// Java parity: model/Race — implements L10n (via extension methods; C# enums cannot implement interfaces directly).
/// Note: enum value = raceId from Java constructor (not ordinal).
/// Java @XmlEnum omitted — C# XmlSerializer handles enums natively by name.
/// </summary>
public enum Race
{
    // Playable races
    Elyos = 0,
    Asmodians = 1,
    // NPC races
    Lycan = 2,
    Construct = 3,
    Carrier = 4,
    Drakan = 5,
    Lizardman = 6,
    Teleporter = 7,
    Naga = 8,
    Brownie = 9,
    Krall = 10,
    Shulack = 11,
    Barrier = 12,
    PcLightCastleDoor = 13,
    PcDarkCastleDoor = 14,
    DragonCastleDoor = 15,
    GchiefLight = 16,
    GchiefDark = 17,
    Dragon = 18,
    Outsider = 19,
    Ratman = 20,
    Demihumanoid = 21,
    Undead = 22,
    Beast = 23,
    Magicalmonster = 24,
    Elemental = 25,
    // Special races
    None = 26,
    PcAll = 27,
    // Note: LIVINGWATER(28) and DEFORM(28) share value 28 in Java; DEFORM listed second (C# keeps last)
    Deform = 28,
    Neut = 29,
    GhenchmanLight = 30,
    GhenchmanDark = 31,
    EventTowerDark = 32,
    EventTowerLight = 33,
    Goblin = 34,
    Tricodark = 35,
    Npc = 36,
    Light = 37,
    Dark = 38,
    WorldEventDeftower = 39,
    Orc = 40,
    Dragonet = 41,
    Siegedrakan = 42,
    GchiefDragon = 43,
    WorldEventBonfire = 44,
    DoorKiller = 45,
    Lf5QItem = 46,
}

/// <summary>
/// Extension methods mirroring Race's Java instance methods and L10n interface.
/// Java parity: model/Race — getL10nId(), getRaceId(), isAsmoOrEly(), isPlayerRace(), getRaceByString().
/// </summary>
public static class RaceExtensions
{
    // Java parity: Race::getRaceId() — returns raceId (= enum int value)
    public static int GetRaceId(this Race race) => (int)race;

    // Java parity: Race::getL10nId() — L10n interface implementation; only Elyos/Asmodians have non-zero l10n
    public static int GetL10nId(this Race race) => race switch
    {
        Race.Elyos => 900240,
        Race.Asmodians => 900241,
        _ => 0,
    };

    // Java parity: Race::getL10n() — default L10n method via ChatUtil.l10n(l10nId)
    public static string? GetL10n(this Race race) => race.GetL10nId() == 0 ? null : Utils.ChatUtil.L10n(race.GetL10nId());

    // Java parity: Race::isAsmoOrEly()
    public static bool IsAsmoOrEly(this Race race) => race == Race.Elyos || race == Race.Asmodians;

    // Java parity: Race::isPlayerRace()
    public static bool IsPlayerRace(this Race race) => race.IsAsmoOrEly() || race == Race.PcAll;

    // Java parity: Race::getRaceByString(String fieldName) — returns null if not found
    public static Race? GetRaceByString(string fieldName)
    {
        foreach (Race r in Enum.GetValues<Race>())
        {
            if (r.ToString().Equals(fieldName, StringComparison.Ordinal))
                return r;
        }
        return null;
    }
}
