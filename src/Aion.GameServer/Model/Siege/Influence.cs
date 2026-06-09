using System.Collections.Generic;
using Aion.GameServer.Model;
using Aion.GameServer.Services;

namespace Aion.GameServer.Model.Siege;

/// <summary>Java parity: model/siege/Influence. Java stream groupingBy/summingInt → manual accumulation loops.</summary>
public class Influence
{
    private static readonly Influence instance = new Influence();
    private Dictionary<int, Dictionary<SiegeRace, int>> influencesByWorld;
    private Dictionary<SiegeRace, int> globalInfluences;
    private float elyosInfluenceRate;
    private float asmoInfluenceRate;
    private float balaurInfluenceRate;

    private Influence()
    {
        RecalculateInfluence();
    }

    public static Influence GetInstance()
    {
        return instance;
    }

    public void RecalculateInfluence()
    {
        influencesByWorld = CalculateFortressWorldInfluences();
        globalInfluences = CalculateGlobalInfluences(influencesByWorld);
        elyosInfluenceRate = GetInfluence(SiegeRace.ELYOS) / 100f;
        asmoInfluenceRate = GetInfluence(SiegeRace.ASMODIANS) / 100f;
        balaurInfluenceRate = GetInfluence(SiegeRace.BALAUR) / 100f;
    }

    private Dictionary<int, Dictionary<SiegeRace, int>> CalculateFortressWorldInfluences()
    {
        Dictionary<int, Dictionary<SiegeRace, int>> fortressWorldInfluences = new Dictionary<int, Dictionary<SiegeRace, int>>();
        foreach (SiegeLocation sLoc in SiegeService.GetInstance().GetSiegeLocations().Values)
        {
            int influence = sLoc.GetInfluenceValue();
            if (influence > 0)
            {
                if (!fortressWorldInfluences.TryGetValue(sLoc.GetWorldId(), out var influences))
                {
                    influences = new Dictionary<SiegeRace, int>();
                    fortressWorldInfluences[sLoc.GetWorldId()] = influences;
                }
                SiegeRace race = sLoc.GetRace();
                influences[race] = (influences.TryGetValue(race, out var value) ? value : 0) + influence;
            }
        }
        return fortressWorldInfluences;
    }

    private Dictionary<SiegeRace, int> CalculateGlobalInfluences(Dictionary<int, Dictionary<SiegeRace, int>> influencesByWorld)
    {
        Dictionary<SiegeRace, int> result = new Dictionary<SiegeRace, int>();
        if (influencesByWorld.Count == 0)
            return result;
        foreach (Dictionary<SiegeRace, int> inner in influencesByWorld.Values)
        {
            foreach (KeyValuePair<SiegeRace, int> entry in inner)
            {
                result[entry.Key] = (result.TryGetValue(entry.Key, out var v) ? v : 0) + entry.Value;
            }
        }
        return result;
    }

    public float GetElyosInfluenceRate()
    {
        return elyosInfluenceRate;
    }

    public float GetAsmodianInfluenceRate()
    {
        return asmoInfluenceRate;
    }

    public float GetBalaurInfluenceRate()
    {
        return balaurInfluenceRate;
    }

    public int GetInfluence(SiegeRace race)
    {
        return globalInfluences.TryGetValue(race, out var v) ? v : 0;
    }

    public int GetInfluence(int worldId, SiegeRace race)
    {
        if (influencesByWorld.TryGetValue(worldId, out var inner) && inner.TryGetValue(race, out var v))
            return v;
        return 0;
    }

    public ISet<int> GetInfluenceRelevantWorldIds()
    {
        return new HashSet<int>(influencesByWorld.Keys);
    }

    public int GetPvpRaceBonusRatio(Race attRace)
    {
        return attRace switch
        {
            Race.ASMODIANS => CalculatePvpRaceBonusRatio(GetAsmodianInfluenceRate(), GetElyosInfluenceRate()),
            Race.ELYOS => CalculatePvpRaceBonusRatio(GetElyosInfluenceRate(), GetAsmodianInfluenceRate()),
            _ => 0,
        };
    }

    private int CalculatePvpRaceBonusRatio(float ownInfluence, float enemyInfluence)
    {
        if (enemyInfluence >= 0.81f && ownInfluence <= 0.10f)
            return 200;
        else if (enemyInfluence >= 0.81f || (enemyInfluence >= 0.71f && ownInfluence <= 0.10f))
            return 150;
        else if (enemyInfluence >= 0.71f)
            return 100;
        return 0;
    }
}
