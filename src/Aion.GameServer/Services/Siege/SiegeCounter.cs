using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Siege;

namespace Aion.GameServer.Services.Siege;

/// <summary>Java parity: services/siege/SiegeCounter. EnumMap→Dictionary; stream filter/sorted/findFirst.orElseGet→Where/Sort/first-or-fallback.</summary>
public class SiegeCounter
{
    private readonly Dictionary<SiegeRace, SiegeRaceCounter> siegeRaceCounters = new Dictionary<SiegeRace, SiegeRaceCounter>();

    public SiegeCounter()
    {
        siegeRaceCounters[SiegeRace.ELYOS] = new SiegeRaceCounter(SiegeRace.ELYOS);
        siegeRaceCounters[SiegeRace.ASMODIANS] = new SiegeRaceCounter(SiegeRace.ASMODIANS);
        siegeRaceCounters[SiegeRace.BALAUR] = new SiegeRaceCounter(SiegeRace.BALAUR);
    }

    public void AddDamage(Creature creature, int damage)
    {
        SiegeRace siegeRace;
        if (creature is Player)
            siegeRace = SiegeRaceExtensions.GetByRace(creature.GetRace());
        else if (creature is SiegeNpc siegeNpc)
            siegeRace = siegeNpc.GetSiegeRace();
        else
            return;
        siegeRaceCounters[siegeRace].AddPoints(creature, damage);
    }

    public void AddAbyssPoints(Player player, int ap)
    {
        SiegeRace sr = SiegeRaceExtensions.GetByRace(player.GetRace());
        siegeRaceCounters[sr].AddAbyssPoints(player, ap);
    }

    public SiegeRaceCounter GetRaceCounter(SiegeRace race)
    {
        return siegeRaceCounters[race];
    }

    public void AddRaceDamage(SiegeRace race, int damage)
    {
        GetRaceCounter(race).AddTotalDamage(damage);
    }

    public SiegeRaceCounter GetWinnerRaceCounter(SiegeRace fallbackRace)
    {
        List<SiegeRaceCounter> sorted = siegeRaceCounters.Values.Where(c => c.GetTotalDamage() > 0).ToList();
        sorted.Sort();
        return sorted.Count > 0 ? sorted[0] : GetRaceCounter(fallbackRace);
    }
}
