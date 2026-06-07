using Aion.GameServer.Utils;

namespace Aion.GameServer.Model.Siege;

/// <summary>
/// Siege race identifiers — maps each to a raceId int and an L10n string.
/// Java parity: model/siege/SiegeRace.
/// </summary>
public enum SiegeRace
{
    // Java parity: ELYOS(Race.ELYOS)
    Elyos,
    // Java parity: ASMODIANS(Race.ASMODIANS)
    Asmodians,
    // Java parity: BALAUR(2, ChatUtil.l10n(900242))
    Balaur,
}

/// <summary>
/// Extension methods mirroring SiegeRace's Java instance/static methods.
/// Java parity: model/siege/SiegeRace::getRaceId, ::getL10n, ::getByRace.
/// </summary>
public static class SiegeRaceExtensions
{
    // Java parity: SiegeRace::getRaceId() — matches Java constructor raceId
    public static int GetRaceId(this SiegeRace race) => race switch
    {
        SiegeRace.Elyos => (int)Model.Race.Elyos,       // Race.ELYOS.getRaceId() = 0
        SiegeRace.Asmodians => (int)Model.Race.Asmodians, // Race.ASMODIANS.getRaceId() = 1
        SiegeRace.Balaur => 2,
        _ => 2,
    };

    // Java parity: SiegeRace::getL10n() — matches Java constructor l10n
    public static string? GetL10n(this SiegeRace race) => race switch
    {
        SiegeRace.Elyos => ChatUtil.L10n(900240),   // Race.ELYOS.getL10n()
        SiegeRace.Asmodians => ChatUtil.L10n(900241), // Race.ASMODIANS.getL10n()
        SiegeRace.Balaur => ChatUtil.L10n(900242),
        _ => null,
    };

    // Java parity: SiegeRace::getByRace(Race race)
    public static SiegeRace GetByRace(Model.Race race) => race switch
    {
        Model.Race.Asmodians => SiegeRace.Asmodians,
        Model.Race.Elyos => SiegeRace.Elyos,
        _ => SiegeRace.Balaur,
    };
}
