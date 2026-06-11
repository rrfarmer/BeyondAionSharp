using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Siegelocation;
using Aion.GameServer.Utils.Collections;
using Aion.GameServer.World.Zone;
using Aion.GameServer.World.Zone.Handler;

namespace Aion.GameServer.Model.Siege;

/// <summary>Java parity: model/siege/SiegeLocation.</summary>
public class SiegeLocation : IZoneHandler
{
    public const int STATE_INVULNERABLE = 0;
    public const int STATE_VULNERABLE = 1;

    private readonly SiegeLocationTemplate template;
    private readonly List<SiegeZoneInstance> zones = new List<SiegeZoneInstance>();
    private readonly ConcurrentDictionary<int, Creature> creatures = new ConcurrentDictionary<int, Creature>();
    private readonly ConcurrentDictionary<int, Player> players = new ConcurrentDictionary<int, Player>();
    private SiegeRace siegeRace = SiegeRace.BALAUR;
    private int legionId;
    private bool vulnerable;
    private int nextState;
    private bool isUnderShield;
    private bool canTeleport = true;
    private int occupiedCount;
    private int factionBalance;

    public SiegeLocation(SiegeLocationTemplate template)
    {
        this.template = template;
    }

    public SiegeLocationTemplate GetTemplate()
    {
        return template;
    }

    public int GetLocationId()
    {
        return template.GetId();
    }

    public int GetWorldId()
    {
        return template.GetWorldId();
    }

    public SiegeType GetType_()
    {
        return template.GetType_();
    }

    public int GetSiegeDuration()
    {
        return template.GetSiegeDuration();
    }

    public List<SiegeReward> GetRewards()
    {
        return template.GetSiegeRewards();
    }

    public virtual SiegeRace GetRace()
    {
        return siegeRace;
    }

    public void SetRace(SiegeRace siegeRace)
    {
        this.siegeRace = siegeRace;
    }

    public int GetLegionId()
    {
        return legionId;
    }

    public void SetLegionId(int legionId)
    {
        this.legionId = legionId;
    }

    public virtual int GetNextState()
    {
        return nextState;
    }

    public void SetNextState(int nextState)
    {
        this.nextState = nextState;
    }

    public bool IsVulnerable()
    {
        return vulnerable;
    }

    public bool IsUnderShield()
    {
        return isUnderShield;
    }

    public int GetOccupiedCount()
    {
        return occupiedCount;
    }

    public void IncreaseOccupiedCount()
    {
        occupiedCount += 1;
    }

    public void SetOccupiedCount(int occupiedCount)
    {
        this.occupiedCount = occupiedCount;
    }

    public int GetFactionBalance()
    {
        return factionBalance;
    }

    public void AdjustFactionBalance(int adjustment)
    {
        factionBalance += Math.Sign(adjustment);
        if (factionBalance > 9)
            factionBalance = 9;
        else if (factionBalance < -9)
            factionBalance = -9;
    }

    public void SetFactionBalance(int factionBalance)
    {
        this.factionBalance = factionBalance;
    }

    public void SetUnderShield(bool value)
    {
        this.isUnderShield = value;
    }

    public bool IsCanTeleport(Player player)
    {
        if (player == null)
            return canTeleport;
        return canTeleport && player.GetRace().GetRaceId() == GetRace().GetRaceId();
    }

    public int GetLegionGp()
    {
        return template.GetLegionGp();
    }

    public void SetCanTeleport(bool canTeleport)
    {
        this.canTeleport = canTeleport;
    }

    public void SetVulnerable(bool value)
    {
        this.vulnerable = value;
    }

    public int GetInfluenceValue()
    {
        return template.GetInfluenceValue();
    }

    public List<SiegeZoneInstance> GetZone()
    {
        return zones;
    }

    public void AddZone(SiegeZoneInstance zone)
    {
        zones.Add(zone);
        zone.AddHandler(this);
    }

    public bool IsInsideLocation(Creature creature)
    {
        if (zones.Count == 0)
            return false;
        foreach (SiegeZoneInstance zone in zones)
            if (zone.IsInsideCreature(creature))
                return true;
        return false;
    }

    public bool IsInsideLocation(float x, float y, float z)
    {
        if (zones.Count == 0)
            return false;
        foreach (SiegeZoneInstance zone in zones)
            if (zone.IsInsideCordinate(x, y, z))
                return true;
        return false;
    }

    public virtual void ClearLocation()
    {
    }

    public virtual void OnEnterZone(Creature creature, ZoneInstance zone)
    {
        if (!creatures.ContainsKey(creature.GetObjectId()))
        {
            creatures[creature.GetObjectId()] = creature;
            if (creature is Player player)
            {
                players[creature.GetObjectId()] = player;
            }
        }
    }

    public virtual void OnLeaveZone(Creature creature, ZoneInstance zone)
    {
        if (!IsInsideLocation(creature))
        {
            creatures.TryRemove(creature.GetObjectId(), out _);
            players.TryRemove(creature.GetObjectId(), out _);
        }
    }

    public void ForEachCreature(Action<Creature> consumer)
    {
        CollectionUtil.ForEach(creatures.Values, consumer);
    }

    public void ForEachPlayer(Action<Player> consumer)
    {
        CollectionUtil.ForEach(players.Values, consumer);
    }
}
