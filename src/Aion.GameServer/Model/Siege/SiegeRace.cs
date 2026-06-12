using Aion.GameServer.Utils;

namespace Aion.GameServer.Model.Siege;

/// <summary>
/// Siege race identifiers — maps each to a raceId int and an L10n string.
/// Java parity: model/siege/SiegeRace (Sarynth). UPPER_CASE names match Java; GHENCHMAN_LIGHT/DARK mirror the
/// Race enum's henchman-balaur factions used by AgentSiege.
/// </summary>
public enum SiegeRace
{
    ELYOS,
    ASMODIANS,
    BALAUR,
    GHENCHMAN_LIGHT,
    GHENCHMAN_DARK,
}

/// <summary>
/// Extension/static helpers mirroring SiegeRace's Java instance/static methods.
/// Java parity: model/siege/SiegeRace::getRaceId, ::getL10n, ::getByRace.
/// </summary>
public static class SiegeRaceExtensions
{
    // Java parity: SiegeRace::getRaceId() — matches Java constructor raceId
    public static int GetRaceId(this SiegeRace race) => race switch
    {
        SiegeRace.ELYOS => (int)Model.Race.ELYOS,         // Race.ELYOS.getRaceId() = 0
        SiegeRace.ASMODIANS => (int)Model.Race.ASMODIANS, // Race.ASMODIANS.getRaceId() = 1
        SiegeRace.BALAUR => 2,
        SiegeRace.GHENCHMAN_LIGHT => (int)Model.Race.GHENCHMAN_LIGHT,
        SiegeRace.GHENCHMAN_DARK => (int)Model.Race.GHENCHMAN_DARK,
        _ => 2,
    };

    // Java parity: SiegeRace::getL10n() — matches Java constructor l10n
    public static string? GetL10n(this SiegeRace race) => race switch
    {
        SiegeRace.ELYOS => ChatUtil.L10n(900240),   // Race.ELYOS.getL10n()
        SiegeRace.ASMODIANS => ChatUtil.L10n(900241), // Race.ASMODIANS.getL10n()
        SiegeRace.BALAUR => ChatUtil.L10n(900242),
        _ => null,
    };

    // Java parity: SiegeRace::getByRace(Race race)
    public static SiegeRace GetByRace(Model.Race race) => race switch
    {
        Model.Race.ASMODIANS => SiegeRace.ASMODIANS,
        Model.Race.ELYOS => SiegeRace.ELYOS,
        Model.Race.GHENCHMAN_LIGHT => SiegeRace.GHENCHMAN_LIGHT,
        Model.Race.GHENCHMAN_DARK => SiegeRace.GHENCHMAN_DARK,
        _ => SiegeRace.BALAUR,
    };
}
