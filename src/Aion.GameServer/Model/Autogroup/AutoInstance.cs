using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Instance;
using Aion.GameServer.Model.Instance.Instancescore;
using Aion.GameServer.World;

namespace Aion.GameServer.Model.Autogroup;

/// <summary>Java parity: model/autogroup/AutoInstance (xTz, Estrayl). Abstract : AutoInstanceHandler. Map→ConcurrentDictionary; InstanceScore&lt;?&gt;→non-generic InstanceScore base; stream filter/collect→Where/ToList; List.sort(comparingInt)→Sort((a,b)=>...CompareTo); AGPlayer record p.race()→p.Race; currentTimeMillis→UtcNow.ToUnixTimeMilliseconds. Handler methods virtual for subclass override. AutoGroupType/LookingForParty/AutoInstanceHandler-deps red-tolerated.</summary>
public abstract class AutoInstance : AutoInstanceHandler
{
    protected readonly ConcurrentDictionary<int, AGPlayer> registeredAGPlayers = new();
    protected readonly AutoGroupType agt;
    protected WorldMapInstance instance;
    protected long startInstanceTime;

    public AutoInstance(AutoGroupType agt)
    {
        this.agt = agt;
    }

    protected bool RemoveItem(Player player, int itemId, long requiredCount)
    {
        long itemCount = 0;
        List<Item> items = player.GetInventory().GetItemsByItemId(itemId);
        foreach (Item item in items)
            itemCount += item.GetItemCount();
        if (itemCount < requiredCount)
            return false;
        items.Sort((a, b) => a.GetExpireTime().CompareTo(b.GetExpireTime()));
        foreach (Item item in items)
        {
            requiredCount = player.GetInventory().DecreaseItemCount(item, requiredCount);
            if (requiredCount == 0)
                break;
        }
        return true;
    }

    public virtual void OnInstanceCreate(WorldMapInstance instance)
    {
        this.instance = instance;
        startInstanceTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    public virtual AGQuestion AddLookingForParty(LookingForParty lookingForParty)
    {
        return AGQuestion.FAILED;
    }

    public virtual void OnEnterInstance(Player player)
    {
    }

    public virtual void OnLeaveInstance(Player player)
    {
    }

    public virtual void OnPressEnter(Player player)
    {
        long instanceCoolTime = DataManager.INSTANCE_COOLTIME_DATA.CalculateInstanceEntranceCooltime(player, instance.GetMapId());
        if (instanceCoolTime > 0)
            player.GetPortalCooldownList().AddPortalCooldown(instance.GetMapId(), instanceCoolTime);
    }

    public virtual void Unregister(Player player)
    {
        registeredAGPlayers.TryRemove(player.GetObjectId(), out _);
    }

    protected bool IsRegistrationDisabled(LookingForParty lfp)
    {
        if (instance != null)
        {
            InstanceScore instanceScore = instance.GetInstanceHandler().GetInstanceScore();
            if (instanceScore != null && instanceScore.GetInstanceProgressionType() == InstanceProgressionType.END_PROGRESS)
                return true;
        }
        if (startInstanceTime == 0)
            return false;
        else if (lfp.GetEntryRequestType() == EntryRequestType.QUICK_GROUP_ENTRY)
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startInstanceTime > agt.GetMaximumJoinTime();
        return true;
    }

    public AutoGroupType GetAutoGroupType()
    {
        return agt;
    }

    public IDictionary<int, AGPlayer> GetRegisteredAGPlayers()
    {
        return registeredAGPlayers;
    }

    public WorldMapInstance GetInstance()
    {
        return instance;
    }

    public long GetStartInstanceTime()
    {
        return startInstanceTime;
    }

    protected List<AGPlayer> GetAGPlayersByRace(Race race)
    {
        return registeredAGPlayers.Values.Where(p => p.Race == race).ToList();
    }

    protected List<AGPlayer> GetAGPlayersByClass(PlayerClass playerClass)
    {
        return registeredAGPlayers.Values.Where(p => p.PlayerClass == playerClass).ToList();
    }

    protected List<Player> GetPlayersByRace(Race race)
    {
        return instance.GetPlayersInside().Where(p => p.GetRace() == race).ToList();
    }

    public virtual int GetMaxPlayers()
    {
        if (instance != null)
            return instance.GetMaxPlayers();
        Race race = registeredAGPlayers.Count == 0 ? Race.ELYOS : registeredAGPlayers.Values.First().Race;
        return DataManager.INSTANCE_COOLTIME_DATA.GetMaxMemberCount(agt.GetTemplate().GetInstanceMapId(), race);
    }
}
