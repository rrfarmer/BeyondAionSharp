using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.LegionDominion;
using Aion.GameServer.Model.Templates.Cp;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Services.ConquerorAndProtectorSystem;

/// <summary>Java parity: services/conquerorAndProtectorSystem/ConquerorAndProtectorService (Source, Dtem, ginho, Yeats, Neon). 4.8 conqueror/protector system: init (handled worlds by race + periodic kill-decay task), per-map CPInfo lookup/creation, enter/leave map+zone+legion hooks, onKill (victim accrual + rank up/down + buff), intruder scan w/ cooldown, rank calc. ConcurrentHashMap->ConcurrentDictionary, HashMap->Dictionary; computeIfAbsent->GetOrAdd; map.get->TryGetValue/GetValueOrDefault; values().removeIf->ToArray()+TryRemove; Stream.concat->Concat; scheduleAtFixedRate(lambda)->ScheduleAtFixedRateTask(ct-lambda); currentTimeMillis->UtcNow; getType()->GetType_(); IllegalArgument->Argument; enum.name()->ToString; equalsIgnoreCase->OrdinalIgnoreCase; handledWorlds Race lookup -> TryGetValue (value-type). CPType/CPRank/SM_/zone red-tolerated.</summary>
public class ConquerorAndProtectorService
{
    private readonly ConcurrentDictionary<int, CPInfo> conquerors = new ConcurrentDictionary<int, CPInfo>();
    private readonly ConcurrentDictionary<int, CPInfo> protectors = new ConcurrentDictionary<int, CPInfo>();
    private readonly ConcurrentDictionary<int, long> intruderScanCooldowns = new ConcurrentDictionary<int, long>();
    private readonly Dictionary<int, Race> handledWorlds = new Dictionary<int, Race>();

    private ConquerorAndProtectorService()
    {
    }

    public void Init()
    {
        if (!CustomConfig.CONQUEROR_AND_PROTECTOR_SYSTEM_ENABLED || CustomConfig.CONQUEROR_AND_PROTECTOR_WORLDS.Count == 0)
            return;

        foreach (int worldId in CustomConfig.CONQUEROR_AND_PROTECTOR_WORLDS)
        {
            int worldType = worldId / 10000000 % 10; // the second digit in the map ID denotes the world type (0 = all, 1 = elyos, 2 = asmodians)
            if (worldType == 1)
                handledWorlds[worldId] = Race.ELYOS;
            else if (worldType == 2)
                handledWorlds[worldId] = Race.ASMODIANS;
            else
                throw new ArgumentException("Map " + worldId + " is not supported for conqueror and protector system (not race specific).");
        }

        ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(ct =>
        {
            long nowMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            foreach (KeyValuePair<int, long> cd in intruderScanCooldowns.ToArray())
                if (nowMillis >= cd.Value)
                    intruderScanCooldowns.TryRemove(cd.Key, out _);
            foreach (CPInfo info in conquerors.Values.Concat(protectors.Values))
            {
                if (info.GetVictims() > 0)
                    AddVictims(null, info, -CustomConfig.CONQUEROR_AND_PROTECTOR_KILLS_DECREASE_COUNT);
            }
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(CustomConfig.CONQUEROR_AND_PROTECTOR_KILLS_DECREASE_INTERVAL * 60000), TimeSpan.FromMilliseconds(CustomConfig.CONQUEROR_AND_PROTECTOR_KILLS_DECREASE_INTERVAL * 60000)); // kills remove timer
    }

    public CPInfo GetCPInfoForCurrentMap(Player player)
    {
        return GetCPInfoForCurrentMap(player, false);
    }

    public CPInfo GetCPInfoForCurrentMap(Player player, bool createIfNotExists)
    {
        CPInfo cpInfo = null;
        CPType? cpTypeForCurrentMap = GetCPTypeForCurrentMap(player);
        if (cpTypeForCurrentMap != null)
        {
            if (createIfNotExists)
                cpInfo = cpTypeForCurrentMap == CPType.PROTECTOR
                    ? protectors.GetOrAdd(player.GetObjectId(), key => new CPInfo(cpTypeForCurrentMap.Value, player))
                    : conquerors.GetOrAdd(player.GetObjectId(), key => new CPInfo(cpTypeForCurrentMap.Value, player));
            else
                cpInfo = cpTypeForCurrentMap == CPType.PROTECTOR ? protectors.GetValueOrDefault(player.GetObjectId()) : conquerors.GetValueOrDefault(player.GetObjectId());
        }
        return cpInfo;
    }

    public void OnEnterMap(Player player)
    {
        if (!CustomConfig.CONQUEROR_AND_PROTECTOR_SYSTEM_ENABLED)
            return;
        CPInfo cpInfo = GetCPInfoForCurrentMap(player);
        if (cpInfo != null && cpInfo.GetLDRank() == 0)
        {
            int type = cpInfo.GetType_() == CPType.CONQUEROR ? 0 : 7;
            int intruderScanCd = cpInfo.GetType_() == CPType.CONQUEROR ? 0 : GetOrRemoveCooldown(player.GetObjectId());
            PacketSendUtility.SendPacket(player, new SM_CONQUEROR_PROTECTOR(type, cpInfo.GetRank(), intruderScanCd));
            UpdateBuffAndNotifyNearbyPlayers(player, cpInfo);
        }
    }

    public void OnLeaveMap(Player player)
    {
        CPInfo info = GetCPInfoForCurrentMap(player);
        if (info != null)
            info.GetBuff().EndEffect(player);
    }

    // for Legion Dominion Zones
    public void OnEnterZone(Player player, ZoneInstance zone)
    {
        if (!CustomConfig.CONQUEROR_AND_PROTECTOR_SYSTEM_ENABLED)
            return;
        if (zone.IsDominionZone() && IsOccupiedLegionDominionZone(player, zone))
        {
            CPInfo cpInfo = protectors.GetOrAdd(player.GetObjectId(), key => new CPInfo(CPType.PROTECTOR, player));
            if (cpInfo.GetLDRank() == 0)
            {
                cpInfo.SetLDRank(3);
                cpInfo.GetBuff().ApplyEffect(player, cpInfo.GetType_(), 3);
                PacketSendUtility.SendPacket(player, new SM_CONQUEROR_PROTECTOR(7, cpInfo.GetLDRank(), GetOrRemoveCooldown(player.GetObjectId())));
            }
        }
    }

    public void OnLeaveZone(Player player, ZoneInstance zone)
    {
        if (!CustomConfig.CONQUEROR_AND_PROTECTOR_SYSTEM_ENABLED)
            return;
        if (zone.IsDominionZone() && IsOccupiedLegionDominionZone(player, zone))
        {
            ResetLegionDominionRank(player);
        }
    }

    public void OnLeaveLegion(Player player)
    {
        if (CustomConfig.CONQUEROR_AND_PROTECTOR_SYSTEM_ENABLED)
            ResetLegionDominionRank(player);
    }

    private void ResetLegionDominionRank(Player player)
    {
        CPInfo cpInfo = protectors.GetValueOrDefault(player.GetObjectId());
        if (cpInfo != null && cpInfo.GetLDRank() > 0)
        {
            cpInfo.SetLDRank(0);
            UpdateBuffAndNotifyNearbyPlayers(player, cpInfo);
            if (cpInfo.GetRank() == 0)
                protectors.TryRemove(player.GetObjectId(), out _);
        }
    }

    private bool IsOccupiedLegionDominionZone(Player player, ZoneInstance zone)
    {
        if (player.GetLegion() == null)
            return false;
        LegionDominionLocation loc = LegionDominionService.GetInstance().GetLegionDominionLoc(player.GetLegion().GetOccupiedLegionDominion());
        return loc != null && loc.GetZoneNameAsString().Equals(zone.GetAreaTemplate().GetZoneName().ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public void OnKill(Player killer, Player victim)
    {
        if (!CustomConfig.CONQUEROR_AND_PROTECTOR_SYSTEM_ENABLED)
            return;
        if (handledWorlds.ContainsKey(victim.GetWorldId()))
        {
            if (killer.GetLevel() - victim.GetLevel() <= CustomConfig.CONQUEROR_AND_PROTECTOR_LEVEL_DIFF)
            {
                CPInfo killerInfo = GetCPInfoForCurrentMap(killer, true);
                CPInfo victimInfo = GetCPInfoForCurrentMap(victim);

                if (victimInfo != null && victimInfo.GetType_() == CPType.CONQUEROR && victimInfo.GetRank() == 3)
                {
                    SM_SYSTEM_MESSAGE msg = killer.GetRace() == Race.ASMODIANS
                        ? SM_SYSTEM_MESSAGE.STR_MSG_SLAYER_LIGHT_DEATH_TO_B(killer.GetName(), victim.GetName())
                        : SM_SYSTEM_MESSAGE.STR_MSG_SLAYER_DARK_DEATH_TO_B(killer.GetName(), victim.GetName());
                    PacketSendUtility.BroadcastToMap(victim, msg);
                }
                AddVictims(killer, killerInfo, 1);
            }
        }
    }

    public void SendDetectCooldown(Player player)
    {
        CPInfo cpInfo = GetCPInfoForCurrentMap(player);
        if (cpInfo == null)
            return;
        PacketSendUtility.SendPacket(player, new SM_CONQUEROR_PROTECTOR(7, cpInfo.GetLDRank(), GetOrRemoveCooldown(player.GetObjectId())));
    }

    private void AddVictims(Player player, CPInfo info, int count)
    {
        int newVictims = Math.Min(Math.Max(info.GetVictims() + count, 0), CustomConfig.CONQUEROR_AND_PROTECTOR_KILLS_RANK3);
        info.SetVictims(newVictims);
        int newRank = GetRank(info.GetVictims());
        if (info.GetRank() != newRank)
        {
            info.SetRank(newRank);
            if (info.GetRank() == 0 && info.GetLDRank() == 0)
                (info.GetType_() == CPType.CONQUEROR ? conquerors : protectors).TryRemove(info.GetPlayerId(), out _);
            if (player == null)
                player = World.GetInstance().GetPlayer(info.GetPlayerId());
            if (player != null)
                UpdateBuffAndNotifyNearbyPlayers(player, info);
        }
    }

    private void UpdateBuffAndNotifyNearbyPlayers(Player player, CPInfo cpInfo)
    {
        if (cpInfo.GetLDRank() == 0)
        { // not inside a legion dominion zone
            if (GetCPTypeForCurrentMap(player) == cpInfo.GetType_())
                cpInfo.GetBuff().ApplyEffect(player, cpInfo.GetType_(), cpInfo.GetRank());
            else
                cpInfo.GetBuff().EndEffect(player);
            PacketSendUtility.SendPacket(player, new SM_CONQUEROR_PROTECTOR(cpInfo.GetType_() == CPType.CONQUEROR ? 1 : 8, cpInfo.GetRank()));
            PacketSendUtility.BroadcastPacket(player, new SM_CONQUEROR_PROTECTOR(cpInfo.GetType_() == CPType.CONQUEROR ? 6 : 9, player));
        }
    }

    public void IntruderScan(Player player)
    {
        if (GetCPTypeForCurrentMap(player) != CPType.PROTECTOR)
            return;
        if (GetOrRemoveCooldown(player.GetObjectId()) > 0)
            return;
        intruderScanCooldowns[player.GetObjectId()] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 180 * 1000;
        PacketSendUtility.SendPacket(player, new SM_CONQUEROR_PROTECTOR(FindIntruders(player), true));
    }

    private int GetOrRemoveCooldown(int objectId)
    {
        if (intruderScanCooldowns.TryGetValue(objectId, out long cd))
        {
            long remainingMillis = cd - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (remainingMillis > 0)
            {
                return (int)(remainingMillis / 1000);
            }
            else
            {
                intruderScanCooldowns.TryRemove(objectId, out _);
            }
        }
        return 0;
    }

    private List<Player> FindIntruders(Player player)
    {
        CPInfo protector = protectors.GetValueOrDefault(player.GetObjectId());
        List<Player> intruders = new List<Player>();
        if (protector != null)
        {
            foreach (CPInfo conqueror in conquerors.Values)
            {
                if (CanSee(protector, conqueror))
                {
                    Player intruder = World.GetInstance().GetPlayer(conqueror.GetPlayerId());
                    if (intruder != null && intruder.GetRace() != player.GetRace() && PositionUtil.IsInRange(intruder, player, 500))
                        intruders.Add(intruder);
                }
            }
        }
        return intruders;
    }

    /// <summary>
    /// Returns true if the protector has the required rank to detect the intruders position on the map
    /// </summary>
    private bool CanSee(CPInfo protector, CPInfo intruder)
    {
        int rank = Math.Max(protector.GetLDRank(), protector.GetRank());
        CPRank cpRank = DataManager.CONQUEROR_AND_PROTECTOR_DATA.GetRank(CPType.PROTECTOR, rank);
        return cpRank != null && cpRank.GetVisibleIntruderMinRank() != 0 && cpRank.GetVisibleIntruderMinRank() >= intruder.GetRank();
    }

    private int GetRank(int kills)
    {
        if (kills >= CustomConfig.CONQUEROR_AND_PROTECTOR_KILLS_RANK3)
            return 3;
        if (kills >= CustomConfig.CONQUEROR_AND_PROTECTOR_KILLS_RANK2)
            return 2;
        if (kills >= CustomConfig.CONQUEROR_AND_PROTECTOR_KILLS_RANK1)
            return 1;
        return 0;
    }

    private CPType? GetCPTypeForCurrentMap(Player player)
    {
        if (!handledWorlds.TryGetValue(player.GetWorldId(), out Race race))
            return null;
        return race == player.GetRace() ? CPType.PROTECTOR : CPType.CONQUEROR;
    }

    public static ConquerorAndProtectorService GetInstance()
    {
        return SingletonHolder.instance;
    }

    private static class SingletonHolder
    {
        internal static readonly ConquerorAndProtectorService instance = new ConquerorAndProtectorService();
    }
}
