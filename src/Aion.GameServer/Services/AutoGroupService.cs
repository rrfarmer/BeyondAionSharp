using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Autogroup;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team.Alliance;
using Aion.GameServer.Model.Team.Group;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Services.Autogroup;
using Aion.GameServer.Services.Instance;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Services;

/// <summary>Java parity: services/AutoGroupService (xTz, Estrayl). Instance matchmaking queue. ConcurrentHashMap→ConcurrentDictionary; ConcurrentHashMap.newKeySet()→ConcurrentDictionary&lt;int,byte&gt; as set (add→TryAdd, remove→TryRemove); computeIfAbsent→GetOrAdd; synchronized(list)→lock(list); List.sort(null)→Sort(); stream filter/map/count/findFirst→LINQ; switch-expr(EntryRequestType)→C# switch expr; schedule(...,ms)→Schedule(TimeSpan.FromMilliseconds); AutoGroupType enum→extension methods. AutoInstance/LookingForParty converged; AutoGroupUtility/AGQuestion/services red-tolerated.</summary>
public class AutoGroupService
{
    private readonly ConcurrentDictionary<WorldMapInstance, AutoInstance> autoInstances = new ConcurrentDictionary<WorldMapInstance, AutoInstance>();
    private readonly ConcurrentDictionary<int, List<LookingForParty>> lookingParties = new ConcurrentDictionary<int, List<LookingForParty>>();
    // Java parity: ConcurrentHashMap.newKeySet() — concurrent set of ints.
    private readonly ConcurrentDictionary<int, byte> penalties = new ConcurrentDictionary<int, byte>();

    private AutoGroupService()
    {
    }

    public void StartLooking(Player player, int maskId, EntryRequestType ert)
    {
        AutoGroupType agt = AutoGroupTypeExtensions.GetAGTByMaskId(maskId);
        if (agt == null || !CanRegister(player, ert, agt))
            return;
        List<LookingForParty> lfps = lookingParties.GetOrAdd(maskId, k => new List<LookingForParty>());
        LookingForParty lfp;
        lock (lfps)
        {
            lfp = GetSearchEntry(player.GetObjectId(), lfps);
            if (lfp != null)
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_CANT_INSTANCE_ALREADY_REGISTERED(agt.GetTemplate().GetInstanceMapId()));
                return;
            }
            lfp = new LookingForParty(player, ert, maskId);
            lfps.Add(lfp);

            AutoGroupUtility.SendSuccessfulRegistration(lfp, player.GetName(), agt, maskId);
            if (AutoGroupConfig.ANNOUNCE_BATTLEGROUND_REGISTRATIONS && agt.IsPeriodicInstance() && ert == EntryRequestType.GROUP_ENTRY
                && lfps.Count(s => s.GetRace() == player.GetRace()) == 1)
            {
                PacketSendUtility.BroadcastToWorld(
                    new SM_MESSAGE(0, null, player.GetRace().GetL10n() + " have registered for " + agt.GetL10n() + ".", ChatType.BRIGHT_YELLOW_CENTER),
                    p => p.GetRace() != player.GetRace() && agt.IsInLvlRange(p.GetLevel()));
            }

            if (!CheckInstancesForOpenQuickEntries(lfp, maskId))
                CheckQueueForNewMatches(maskId);
        }
    }

    private void CheckQueueForNewMatches(int maskId)
    {
        List<LookingForParty> queuedParties = lookingParties.GetValueOrDefault(maskId);
        if (queuedParties == null || queuedParties.Count == 0)
            return;
        AutoGroupType agt = AutoGroupTypeExtensions.GetAGTByMaskId(maskId);
        if (agt == null)
            return;
        lock (queuedParties)
        {
            queuedParties.Sort();

            for (int i = 0; i < queuedParties.Count; i++)
            {
                AutoInstance autoInstance = agt.CreateAutoInstance();
                LookingForParty lfp = queuedParties[i];
                AGQuestion question = autoInstance.AddLookingForParty(lfp);
                if (question == AGQuestion.FAILED)
                    continue;
                List<LookingForParty> filteredParties = new List<LookingForParty>();
                filteredParties.Add(lfp);
                if (question != AGQuestion.READY)
                {
                    for (int j = i + 1; j < queuedParties.Count; j++)
                    {
                        lfp = queuedParties[j];
                        question = autoInstance.AddLookingForParty(lfp);
                        if (question != AGQuestion.FAILED)
                        {
                            filteredParties.Add(lfp);
                            if (question == AGQuestion.READY)
                                break;
                        }
                    }
                }
                if (question == AGQuestion.READY)
                {
                    CreateNewInstance(autoInstance, agt, filteredParties, maskId);
                    break;
                }
            }
        }
    }

    private void CreateNewInstance(AutoInstance autoInstance, AutoGroupType agt, List<LookingForParty> filteredParties, int maskId)
    {
        WorldMapInstance instance = InstanceService.GetNextAvailableInstance(agt.GetTemplate().GetInstanceMapId(), 0, agt.GetDifficultId(), null,
            autoInstance.GetMaxPlayers(), false);
        autoInstance.OnInstanceCreate(instance);
        autoInstances[instance] = autoInstance;
        foreach (LookingForParty lfp in filteredParties)
        {
            RemoveSearchEntry(lfp);
            lfp.SetStartEnterTime();
            foreach (int id in lfp.GetMembers().Keys)
            {
                SearchAndRemoveAdditionalRegistrations(id);
                AutoGroupUtility.SendWindowToPlayerIfOnline(id, maskId, 4);
            }
        }
    }

    private bool CheckInstancesForOpenQuickEntries(LookingForParty lfp, int maskId)
    {
        if (lfp.GetEntryRequestType() != EntryRequestType.QUICK_GROUP_ENTRY || lfp.IsOnStartEnterTask())
            return false;
        foreach (AutoInstance autoInstance in autoInstances.Values)
        {
            if (autoInstance.GetAutoGroupType().GetTemplate().GetMaskId() == maskId && autoInstance.AddLookingForParty(lfp) == AGQuestion.ADDED)
            {
                RemoveSearchEntry(lfp);
                lfp.SetStartEnterTime();
                AutoGroupUtility.SendWindowToPlayerIfOnline(lfp.GetLeaderObjId(), maskId, 4);
                SearchAndRemoveAdditionalRegistrations(lfp.GetLeaderObjId());
                return true;
            }
        }
        return false;
    }

    private void CheckQueueForQuickEntries(AutoInstance autoInstance)
    {
        int maskId = autoInstance.GetAutoGroupType().GetTemplate().GetMaskId();
        List<LookingForParty> parties = lookingParties.GetValueOrDefault(maskId);
        if (parties == null || parties.Count == 0)
            return;
        lock (parties)
        {
            foreach (LookingForParty lfp in parties)
            {
                if (lfp.GetEntryRequestType() == EntryRequestType.QUICK_GROUP_ENTRY && !lfp.IsOnStartEnterTask()
                    && autoInstance.AddLookingForParty(lfp) == AGQuestion.ADDED)
                {
                    RemoveSearchEntry(lfp);
                    lfp.SetStartEnterTime();
                    AutoGroupUtility.SendWindowToPlayerIfOnline(lfp.GetLeaderObjId(), maskId, 4);
                    SearchAndRemoveAdditionalRegistrations(lfp.GetLeaderObjId());
                    return;
                }
            }
        }
    }

    private void SearchAndRemoveAdditionalRegistrations(int objectId)
    {
        List<LookingForParty> partiesToRemove = GetSearchEntries(objectId);
        foreach (LookingForParty lfp in partiesToRemove)
        {
            int maskId = lfp.GetMaskId();
            if (lfp.IsLeader(objectId))
            {
                RemoveSearchEntry(lfp);
                PenaliseParty(lfp);
                foreach (int id in lfp.GetMembers().Keys)
                    AutoGroupUtility.SendWindowToPlayerIfOnline(id, maskId, 2);
            }
            else
            {
                lfp.UnregisterMember(objectId);
                AutoGroupUtility.SendWindowToPlayerIfOnline(objectId, maskId, 2);
                PenalisePlayerAndScheduleRemoval(objectId);
                CheckQueueForNewMatches(maskId);
            }
        }
    }

    public void PressEnter(Player player, int instanceMaskId)
    {
        AutoInstance instance = GetAutoInstance(player, instanceMaskId);
        if (instance == null)
            return;

        if (player.IsInGroup())
            PlayerGroupService.RemovePlayer(player);
        if (player.IsInAlliance())
            PlayerAllianceService.RemovePlayer(player);

        instance.OnPressEnter(player);
        PacketSendUtility.SendPacket(player, new SM_AUTO_GROUP(instanceMaskId, 5));
    }

    public void OnEnterInstance(Player player)
    {
        if (player.IsInInstance())
        {
            int obj = player.GetObjectId();
            AutoInstance autoInstance = autoInstances.GetValueOrDefault(player.GetWorldMapInstance());
            if (autoInstance != null && autoInstance.GetRegisteredAGPlayers().ContainsKey(obj))
                autoInstance.OnEnterInstance(player);
        }
    }

    public void CancelEnter(Player player, int instanceMaskId)
    {
        AutoInstance autoInstance = GetAutoInstance(player, instanceMaskId);
        if (autoInstance != null)
        {
            int objectId = player.GetObjectId();
            autoInstance.Unregister(player);
            PenalisePlayerAndScheduleRemoval(objectId);
            DestroyOrAddPlayersFromQuickEntries(autoInstance);
            PacketSendUtility.SendPacket(player, new SM_AUTO_GROUP(instanceMaskId, 2));
        }
    }

    public void OnPlayerLogin(Player player)
    {
        PeriodicInstanceManager.GetInstance().CheckAndSendOpenRegistrations(player);
    }

    public bool IsSearching(Player player, int maskId)
    {
        return GetSearchEntry(player.GetObjectId(), lookingParties.GetValueOrDefault(maskId)) != null;
    }

    private LookingForParty GetSearchEntry(Player player, int maskId)
    {
        return GetSearchEntry(player.GetObjectId(), lookingParties.GetValueOrDefault(maskId));
    }

    private LookingForParty GetSearchEntry(int playerObjectId, List<LookingForParty> parties)
    {
        if (parties != null)
        {
            lock (parties)
            {
                foreach (LookingForParty lfp in parties)
                    if (lfp.IsMember(playerObjectId))
                        return lfp;
            }
        }
        return null;
    }

    private List<LookingForParty> GetSearchEntries(int playerObjectId)
    {
        return lookingParties.Values.Select(parties => GetSearchEntry(playerObjectId, parties)).Where(x => x != null).ToList();
    }

    public void OnLogout(Player player)
    {
        int objectId = player.GetObjectId();
        foreach (LookingForParty lfp in GetSearchEntries(objectId))
        {
            if (lfp.IsOnStartEnterTask())
            {
                foreach (AutoInstance autoInstance in autoInstances.Values)
                {
                    CancelEnter(player, autoInstance.GetAutoGroupType().GetTemplate().GetMaskId());
                }
            }
            else if (lfp.IsLeader(objectId))
            {
                lfp.SetLeaderObjId(lfp.GetMembers().Keys.Where(id => id != objectId).FirstOrDefault());
                if (lfp.GetLeaderObjId() == 0)
                {
                    RemoveSearchEntry(lfp);
                }
            }
            else
            {
                lfp.UnregisterMember(objectId);
                CheckQueueForNewMatches(lfp.GetMaskId());
            }
        }

        AutoInstance autoInstance2 = autoInstances.GetValueOrDefault(player.GetWorldMapInstance());
        if (autoInstance2 != null && autoInstance2.GetRegisteredAGPlayers().ContainsKey(objectId))
        {
            DestroyIfPossible(autoInstance2);
        }
    }

    private void RemoveSearchEntry(LookingForParty lfp)
    {
        List<LookingForParty> lfps = lookingParties.GetValueOrDefault(lfp.GetMaskId());
        lock (lfps)
        {
            lfps.Remove(lfp);
        }
    }

    public void OnLeaveInstance(Player player)
    {
        AutoInstance autoInstance = autoInstances.GetValueOrDefault(player.GetWorldMapInstance());
        if (autoInstance != null && autoInstance.GetRegisteredAGPlayers().ContainsKey(player.GetObjectId()))
        {
            autoInstance.OnLeaveInstance(player);
            DestroyOrAddPlayersFromQuickEntries(autoInstance);
        }
        PeriodicInstanceManager.GetInstance().CheckAndSendOpenRegistrations(player);
    }

    private bool CanRegister(Player player, EntryRequestType ert, AutoGroupType agt)
    {
        int mapId = agt.GetTemplate().GetInstanceMapId();
        int instanceMaskId = agt.GetTemplate().GetMaskId();
        if (!agt.IsInLvlRange(player.GetLevel()))
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_CANT_INSTANCE_ENTER_LEVEL());
            return false;
        }
        else if ((agt.IsPvPFFAArena() || agt.IsPvPSoloArena() || agt.IsHarmonyArena() || agt.IsGloryArena())
            && !PvPArenaService.IsPvPArenaAvailable(player, agt))
        {
            return false;
        }
        else if (AutoGroupUtility.HasCoolDown(player, mapId))
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_CANNOT_MAKE_INSTANCE_COOL_TIME());
            return false;
        }
        return ert switch
        {
            EntryRequestType.NEW_GROUP_ENTRY => AutoGroupUtility.CanRegisterNewEntry(player, agt),
            EntryRequestType.QUICK_GROUP_ENTRY => AutoGroupUtility.CanRegisterQuickEntry(player, agt),
            EntryRequestType.GROUP_ENTRY => AutoGroupUtility.CanRegisterGroupEntry(player, agt, mapId, instanceMaskId),
            _ => false,
        };
    }

    private void PenaliseParty(LookingForParty lfp)
    {
        foreach (int id in lfp.GetMembers().Keys)
            PenalisePlayerAndScheduleRemoval(id);
    }

    private void PenalisePlayerAndScheduleRemoval(int objectId)
    {
        if (penalties.TryAdd(objectId, 0))
        {
            ThreadPoolManager.GetInstance().Schedule(ct =>
            {
                penalties.TryRemove(objectId, out _);
                PeriodicInstanceManager.GetInstance().CheckAndSendOpenRegistrations(objectId);
                return ValueTask.CompletedTask;
            }, System.TimeSpan.FromMilliseconds(10000));
        }
    }

    public void StopRegistrationsByMaskId(int maskId)
    {
        lookingParties.TryRemove(maskId, out List<LookingForParty> parties);
        if (parties != null && parties.Count != 0)
            foreach (LookingForParty lfp in parties)
                foreach (int id in lfp.GetMembers().Keys)
                    AutoGroupUtility.SendWindowToPlayerIfOnline(id, maskId, 2);
    }

    public void CancelRegistration(Player player, int maskId)
    {
        CancelRegistration(GetSearchEntry(player, maskId), player, maskId);
    }

    public void CancelRegistration(LookingForParty lfp, Player player, int maskId)
    {
        int objectId = player.GetObjectId();
        if (lfp != null)
        {
            if (lfp.IsLeader(objectId))
            {
                lookingParties.GetValueOrDefault(maskId).Remove(lfp);
                PenaliseParty(lfp);
                foreach (int id in lfp.GetMembers().Keys)
                    AutoGroupUtility.SendWindowToPlayerIfOnline(id, maskId, 2);
            }
            else
            {
                lfp.UnregisterMember(objectId);
                AutoGroupUtility.SendWindowToPlayer(player, maskId, 2);
                PenalisePlayerAndScheduleRemoval(objectId);
                CheckQueueForNewMatches(maskId);
            }
        }
    }

    private void DestroyOrAddPlayersFromQuickEntries(AutoInstance autoInstance)
    {
        if (!DestroyIfPossible(autoInstance) && autoInstance.GetAutoGroupType().GetTemplate().CanRegisterQuickEntry())
            CheckQueueForQuickEntries(autoInstance);
    }

    public bool DestroyIfPossible(AutoInstance autoInstance)
    {
        WorldMapInstance instance = autoInstance.GetInstance();
        if (autoInstance.GetRegisteredAGPlayers().Count == 0 && !instance.GetPlayersInside().Any(p => p.IsOnline()))
        {
            autoInstances.TryRemove(instance, out _);
            InstanceService.DestroyInstance(instance);
            return true;
        }
        return false;
    }

    private AutoInstance GetAutoInstance(Player player, int instanceMaskId)
    {
        foreach (AutoInstance autoInstance in autoInstances.Values)
            if (autoInstance.GetAutoGroupType().GetTemplate().GetMaskId() == instanceMaskId
                && autoInstance.GetRegisteredAGPlayers().ContainsKey(player.GetObjectId()))
                return autoInstance;
        return null;
    }

    public bool IsInAutoInstance(Player player)
    {
        return autoInstances.ContainsKey(player.GetWorldMapInstance());
    }

    public static AutoGroupService GetInstance()
    {
        return NewSingletonHolder.INSTANCE;
    }

    private static class NewSingletonHolder
    {
        internal static readonly AutoGroupService INSTANCE = new AutoGroupService();
    }
}
