using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using Aion.Commons.Utils;
using Aion.GameServer.Dao;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team;
using Aion.GameServer.Model.Templates;
using Aion.GameServer.Model.Templates.Event;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Skillengine;
using Aion.GameServer.Skillengine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Time;
using Aion.GameServer.World;
using StoredBuffData = Aion.GameServer.Dao.EventDAO.StoredBuffData;
using ForceType = Aion.GameServer.Skillengine.Model.Effect.ForceType;

namespace Aion.GameServer.Services.Event;

/// <summary>Java parity: services/event/EventBuffHandler (Neon). Per-event buff pools/day-restrictions w/ DB persistence; trigger-driven buff application (enter-map/team, pve/pvp-kill), restriction checks (day/map/team-size/instance-level). ConcurrentHashMap->ConcurrentDictionary; Collections.shuffle->generic Fisher-Yates; IntStream.rangeClosed.boxed->Enumerable.Range; getOrDefault; containsAll->IsSupersetOf; Consumer<Player>->Action<Player>; TemporaryPlayerTeam<? extends TeamMember<Player>>/<?>-><TeamMember<Player>>; team.forEach method-group; ServerTime.now toLocalDate().lengthOfMonth->DateTime.DaysInMonth; Rnd.chance->Rnd.Chance; buff.isPermanent?0:null->int?; ForceType alias. Buff/EventDAO/SkillEngine red-tolerated.</summary>
public class EventBuffHandler
{
    private readonly string eventName;
    private readonly List<Buff> buffs;
    private readonly ConcurrentDictionary<Buff, HashSet<int>> activeBuffPoolSkillIds = new ConcurrentDictionary<Buff, HashSet<int>>();
    private readonly ConcurrentDictionary<Buff, HashSet<int>> allowedBuffDays = new ConcurrentDictionary<Buff, HashSet<int>>();
    private volatile int dayOfMonth = ServerTime.Now().Day;
    private readonly ForceType effectForceType;

    public EventBuffHandler(string eventName, List<Buff> buffs)
    {
        this.eventName = eventName;
        this.buffs = buffs;
        this.effectForceType = Event.GetOrCreateEffectForceType(eventName);
        InitBuffData();
    }

    private void InitBuffData()
    {
        UpdateActiveBuffSkillIds();
        UpdateAllowedBuffDays(DateTime.DaysInMonth(ServerTime.Now().Year, ServerTime.Now().Month));
        List<StoredBuffData> buffData = EventDAO.LoadStoredBuffData(eventName);
        if (buffData != null)
        {
            foreach (StoredBuffData storedBuffData in buffData)
            {
                if (storedBuffData.GetBuffIndex() < buffs.Count)
                {
                    Buff buff = buffs[storedBuffData.GetBuffIndex()];
                    if (storedBuffData.GetActivePoolSkillIds() != null && buff.GetSkillIds().IsSupersetOf(storedBuffData.GetActivePoolSkillIds()))
                        activeBuffPoolSkillIds[buff] = storedBuffData.GetActivePoolSkillIds();
                    if (storedBuffData.GetAllowedBuffDays() != null && buff.GetRestriction() != null
                        && buff.GetRestriction().GetRandomDaysPerMonth() == storedBuffData.GetAllowedBuffDays().Count)
                        allowedBuffDays[buff] = storedBuffData.GetAllowedBuffDays();
                }
            }
        }
        StoreBuffDataInDb();
    }

    private void UpdateActiveBuffSkillIds()
    {
        foreach (Buff buff in buffs)
        {
            if (buff.GetPool() > 0)
                activeBuffPoolSkillIds[buff] = CollectNRandomElements(buff.GetSkillIds(), buff.GetPool());
        }
    }

    private void UpdateAllowedBuffDays(int endOfMonthDay)
    {
        foreach (Buff buff in buffs)
        {
            int limit = buff.GetRestriction() == null ? 0 : buff.GetRestriction().GetRandomDaysPerMonth();
            if (limit > 0 && limit < endOfMonthDay)
            {
                List<int> daysThisMonth = Enumerable.Range(1, endOfMonthDay).ToList();
                HashSet<int> allowedRandomDaysThisMonth = CollectNRandomElements(daysThisMonth, limit);
                allowedBuffDays[buff] = allowedRandomDaysThisMonth;
            }
            else
            {
                allowedBuffDays.TryRemove(buff, out _);
            }
        }
    }

    private void StoreBuffDataInDb()
    {
        List<StoredBuffData> storedBuffData = new List<StoredBuffData>(buffs.Count);
        for (int i = 0; i < buffs.Count; i++)
        {
            Buff buff = buffs[i];
            HashSet<int> poolSkillIds = activeBuffPoolSkillIds.GetValueOrDefault(buff);
            HashSet<int> allowedDays = allowedBuffDays.GetValueOrDefault(buff);
            if (poolSkillIds != null && allowedDays != null)
                storedBuffData.Add(new StoredBuffData(i, poolSkillIds, allowedDays));
        }
        EventDAO.StoreBuffData(eventName, storedBuffData);
    }

    public ForceType GetEffectForceType()
    {
        return effectForceType;
    }

    public void OnTimeChanged(DateTimeOffset now)
    {
        int nowDayOfMonth = now.Day;
        if (nowDayOfMonth != dayOfMonth)
        {
            dayOfMonth = nowDayOfMonth;
            UpdateActiveBuffSkillIds();
            if (dayOfMonth == 1)
                UpdateAllowedBuffDays(DateTime.DaysInMonth(now.Year, now.Month));
            StoreBuffDataInDb();
            ResetTodaysBuffs();
        }
    }

    private void ResetTodaysBuffs()
    {
        World.World.GetInstance().ForEachPlayer(OnEnterMap);
    }

    public void OnEventStop()
    {
        World.World.GetInstance().ForEachPlayer(EndEventBuffs);
    }

    public void OnEnterMap(Player player)
    {
        EndRestrictedEventBuffs(player);
        TryBuff(player, Buff.TriggerCondition.ENTER_MAP);
    }

    public void OnEnteredTeam(Player player, TemporaryPlayerTeam<TeamMember<Player>> team)
    {
        team.ForEach(EndRestrictedEventBuffs);
        TryBuff(player, Buff.TriggerCondition.ENTER_TEAM);
    }

    public void OnLeftTeam(Player player, TemporaryPlayerTeam<TeamMember<Player>> team)
    {
        EndRestrictedEventBuffs(player); // player isn't in team anymore
        team.ForEach(member =>
        {
            EndRestrictedEventBuffs(member);
            // try to apply buffs since some restrictions are now maybe met (team_size_max_percent)
            foreach (Buff buff in buffs)
                TryBuff(buff, member, Buff.TriggerCondition.ENTER_TEAM);
        });
    }

    public void OnPveKill(Player killer, Npc victim)
    {
        if (killer.GetLevel() - victim.GetLevel() < 10) // victim can be 9 levels below killer level
            TryBuff(killer, Buff.TriggerCondition.PVE_KILL);
    }

    public void OnPvpKill(Player killer, Player victim)
    {
        TryBuff(killer, Buff.TriggerCondition.PVP_KILL);
    }

    private void EndEventBuffs(Player player)
    {
        foreach (Effect effect in player.GetEffectController().GetAllEffects())
        {
            if (effect.GetForceType() == effectForceType)
                effect.EndEffect();
        }
    }

    private void EndRestrictedEventBuffs(Player player)
    {
        foreach (Effect effect in player.GetEffectController().GetAllEffects().ToArray())
        {
            if (effect.GetForceType() == effectForceType)
            {
                bool stillValid = false;
                foreach (Buff buff in buffs)
                {
                    if (GetActiveBuffSkillIds(buff).Contains(effect.GetSkillId()) && CheckRestrictions(buff, player))
                    {
                        stillValid = true; // event effect is still valid, check next one
                        break;
                    }
                }
                if (!stillValid)
                    effect.EndEffect();
            }
        }
    }

    private bool ApplyOnTeam(Player player, Action<Player> memberAction)
    {
        TemporaryPlayerTeam<TeamMember<Player>> team = player.GetCurrentTeam();
        if (team != null)
        {
            team.ForEach(memberAction);
            return true;
        }
        return false;
    }

    private void TryBuff(Player player, Buff.TriggerCondition triggerCondition)
    {
        foreach (Buff buff in buffs)
        {
            if (!buff.IsTeam() || !ApplyOnTeam(player, member => TryBuff(buff, member, triggerCondition)))
                TryBuff(buff, player, triggerCondition);
        }
    }

    private void TryBuff(Buff buff, Player player, Buff.TriggerCondition triggerCondition)
    {
        if (CanReceiveBuff(buff, player, triggerCondition))
        {
            foreach (int skillId in GetActiveBuffSkillIds(buff))
            {
                if (player.GetEffectController().HasAbnormalEffect(skillId))
                    continue;
                Effect effect = SkillEngine.GetInstance().ApplyEffectDirectly(skillId, player, player, buff.IsPermanent() ? 0 : (int?)null, effectForceType);
                if (effect != null)
                {
                    int msgId = 1400697; // You received %0: %1.
                    SM_SYSTEM_MESSAGE message = new SM_SYSTEM_MESSAGE(ChatType.BRIGHT_YELLOW_CENTER, player, msgId, "[Server Buff]",
                        effect.GetSkillTemplate().GetL10n());
                    PacketSendUtility.SendPacket(player, message);
                }
            }
        }
    }

    private HashSet<int> GetActiveBuffSkillIds(Buff buff)
    {
        return activeBuffPoolSkillIds.GetValueOrDefault(buff, buff.GetSkillIds());
    }

    private HashSet<T> CollectNRandomElements<T>(ICollection<T> input, int n)
    {
        List<T> shuffledInput = new List<T>(input);
        Shuffle(shuffledInput);
        HashSet<T> nRandomElements = new HashSet<T>();
        for (int i = 0; i < n; i++)
            nRandomElements.Add(shuffledInput[i]);
        return nRandomElements;
    }

    /// <summary>Java parity: Collections.shuffle — in-place Fisher-Yates via Rnd.</summary>
    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Rnd.Get(0, i);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private bool CanReceiveBuff(Buff buff, Player player, Buff.TriggerCondition triggerCondition)
    {
        Buff.Trigger trigger = FindBuffTrigger(buff, triggerCondition);
        if (trigger == null)
            return false;
        if (!CheckRestrictions(buff, player))
            return false;
        return trigger.GetChance() == 100 || Rnd.Chance() < trigger.GetChance();
    }

    private Buff.Trigger FindBuffTrigger(Buff buff, Buff.TriggerCondition triggerCondition)
    {
        foreach (Buff.Trigger trigger in buff.GetTriggers())
        {
            if (trigger.GetCondition() == triggerCondition)
                return trigger;
        }
        return null;
    }

    private bool CheckRestrictions(Buff buff, Player player)
    {
        if (!IsAllowedToday(buff, player))
            return false;
        if (!IsAllowedOnCurrentMap(buff, player))
            return false;
        if (!IsAllowedTeamSize(buff, player))
            return false;
        return true;
    }

    private bool IsAllowedTeamSize(Buff buff, Player player)
    {
        if (buff.GetRestriction() == null || buff.GetRestriction().GetTeamSizeMaxPercent() == 0)
            return true;
        TemporaryPlayerTeam<TeamMember<Player>> team = player.GetCurrentTeam();
        if (team != null)
        {
            int maxAllowedTeamSize = DataManager.INSTANCE_COOLTIME_DATA.GetMaxMemberCount(player.GetWorldId(), player.GetRace());
            if (maxAllowedTeamSize == 0)
                maxAllowedTeamSize = team.GetMaxMemberCount();

            if (buff.GetRestriction().GetTeamSizeMaxPercent() >= team.Size() * 100f / maxAllowedTeamSize)
                return true;
        }
        return false;
    }

    private bool IsAllowedToday(Buff buff, Player player)
    {
        HashSet<int> allowedBuffDays = this.allowedBuffDays.GetValueOrDefault(buff);
        return allowedBuffDays == null || allowedBuffDays.Contains(dayOfMonth);
    }

    private bool IsAllowedOnCurrentMap(Buff buff, Player player)
    {
        if (buff.GetRestriction() == null || buff.GetRestriction().GetMaps() == null)
            return true;
        WorldMapInstance worldMapInstance = player.GetPosition().GetWorldMapInstance();
        foreach (Buff.BuffMapType buffMapType in buff.GetRestriction().GetMaps())
        {
            if (buffMapType.Matches(worldMapInstance))
            {
                if (buffMapType == Buff.BuffMapType.WORLD_MAP)
                    return true;
                if (CheckInstanceLevel(player))
                    return true;
            }
        }
        return false;
    }

    private bool CheckInstanceLevel(Player player)
    {
        // only allow if the player level is not too high (max 9 levels above the instance entry level)
        InstanceCooltime template = DataManager.INSTANCE_COOLTIME_DATA.GetInstanceCooltimeByWorldId(player.GetWorldId());
        if (template != null)
        {
            int instanceLevel = player.GetRace() == Race.ELYOS ? template.GetEnterMinLevelLight() : template.GetEnterMinLevelDark();
            return instanceLevel == 0 || player.GetLevel() - instanceLevel < 10;
        }
        return true;
    }
}
