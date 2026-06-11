using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Siege;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Services.Siege;

/// <summary>Java parity: services/siege/SiegeRaceCounter. GenericValidator.isBlankOrNull(map)→null/empty check; LinkedHashMap→Dictionary (insertion order).</summary>
public class SiegeRaceCounter : IComparable<SiegeRaceCounter>
{
    private readonly AtomicLong totalDamage = new AtomicLong();
    private readonly ConcurrentDictionary<int, AtomicLong> playerDamageCounter = new ConcurrentDictionary<int, AtomicLong>();
    private readonly ConcurrentDictionary<int, AtomicLong> playerAPCounter = new ConcurrentDictionary<int, AtomicLong>();
    private readonly SiegeRace siegeRace;

    public SiegeRaceCounter(SiegeRace siegeRace)
    {
        this.siegeRace = siegeRace;
    }

    public void AddPoints(Creature creature, int damage)
    {
        AddTotalDamage(damage);
        if (creature is Player player)
            AddPlayerDamage(player, damage);
    }

    public void AddTotalDamage(int damage)
    {
        totalDamage.AddAndGet(damage);
    }

    public void AddPlayerDamage(Player player, int damage)
    {
        AddToCounter(player.GetObjectId(), damage, playerDamageCounter);
    }

    public void AddAbyssPoints(Player player, int abyssPoints)
    {
        AddToCounter(player.GetObjectId(), abyssPoints, playerAPCounter);
    }

    protected void AddToCounter<K>(K key, int value, IDictionary<K, AtomicLong> counterMap)
    {
        // Get the counter for specific key
        if (!counterMap.TryGetValue(key, out AtomicLong counter))
        {
            // synchronize here, it may happen that there will be attempt to increment same counter from different threads
            lock (this)
            {
                if (counterMap.ContainsKey(key))
                {
                    counter = counterMap[key];
                }
                else
                {
                    counter = new AtomicLong();
                    counterMap[key] = counter;
                }
            }
        }
        counter.AddAndGet(value);
    }

    public long GetTotalDamage()
    {
        return totalDamage.Get();
    }

    public IDictionary<int, long> GetPlayerDamageCounter()
    {
        return GetOrderedCounterMap(playerDamageCounter);
    }

    public IDictionary<int, long> GetPlayerAbyssPoints()
    {
        return GetOrderedCounterMap(playerAPCounter);
    }

    protected IDictionary<K, long> GetOrderedCounterMap<K>(IDictionary<K, AtomicLong> unorderedMap)
    {
        if (unorderedMap == null || unorderedMap.Count == 0)
        {
            return new Dictionary<K, long>();
        }
        List<KeyValuePair<K, AtomicLong>> tempList = new List<KeyValuePair<K, AtomicLong>>(unorderedMap);
        tempList.Sort((o1, o2) => o2.Value.Get().CompareTo(o1.Value.Get()));
        Dictionary<K, long> result = new Dictionary<K, long>();
        foreach (KeyValuePair<K, AtomicLong> entry in tempList)
        {
            if (entry.Value.Get() > 0)
            {
                result[entry.Key] = entry.Value.Get();
            }
        }
        return result;
    }

    public int CompareTo(SiegeRaceCounter o)
    {
        return o.GetTotalDamage().CompareTo(GetTotalDamage());
    }

    public SiegeRace GetSiegeRace()
    {
        return siegeRace;
    }

    public int? GetWinnerLegionId()
    {
        Dictionary<Player, AtomicLong> teamDamageMap = new Dictionary<Player, AtomicLong>();
        foreach (int id in playerDamageCounter.Keys)
        {
            Player player = Aion.GameServer.World.World.GetInstance().GetPlayer(id);
            if (player != null)
            {
                if (player.GetCurrentTeam() != null)
                {
                    if (!player.IsInLeague())
                    {
                        Player teamLeader = player.GetCurrentTeam().GetLeaderObject();
                        long damage = playerDamageCounter[id].Get();
                        if (teamLeader != null)
                        {
                            if (!teamDamageMap.ContainsKey(teamLeader))
                            {
                                teamDamageMap[teamLeader] = new AtomicLong();
                            }
                            teamDamageMap[teamLeader].AddAndGet(damage);
                        }
                    }
                    else
                    {
                        Player teamLeader = player.GetPlayerAlliance().GetLeague().GetLeaderObject().GetLeaderObject();
                        long damage = playerDamageCounter[id].Get();
                        if (teamLeader != null)
                        {
                            if (!teamDamageMap.ContainsKey(teamLeader))
                            {
                                teamDamageMap[teamLeader] = new AtomicLong();
                            }
                            teamDamageMap[teamLeader].AddAndGet(damage);
                        }
                    }
                }
                else
                { // solo
                    long damage = playerDamageCounter[id].Get();
                    if (!teamDamageMap.ContainsKey(player))
                    {
                        teamDamageMap[player] = new AtomicLong();
                    }
                    teamDamageMap[player].AddAndGet(damage);
                }
            }
        }
        if (teamDamageMap.Count == 0)
        {
            return null;
        }
        Player topTeamLeader = GetOrderedCounterMap(teamDamageMap).Keys.First();
        Legion legion = topTeamLeader.GetLegion();
        return legion != null ? legion.GetLegionId() : (int?) null;
    }
}
