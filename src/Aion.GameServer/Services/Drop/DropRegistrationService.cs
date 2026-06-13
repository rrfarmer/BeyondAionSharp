using System.Collections.Generic;
using System.Collections.Concurrent;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Drop;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Model.Team.Common.Legacy;
using Aion.GameServer.Model.Templates.GlobalDrops;
using Aion.GameServer.Model.Templates.Housing;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Model.Templates.Spawns.Basespawns;
using Aion.GameServer.Model.Templates.Spawns.Siegespawns;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Event;
using Aion.GameServer.SpawnEngine;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Stats;
using Aion.GameServer.World;
using Aion.GameServer.World.Zone;
using Status = Aion.GameServer.Network.Aion.ServerPackets.SM_LOOT_STATUS.Status;
using Aion.GameServer.Model.Templates.GlobalDrops;

namespace Aion.GameServer.Services.Drop;

/// <summary>Java parity: services/drop/DropRegistrationService (xTz, Aioncool, Bobobear, Neon). Singleton; currentDropMap/dropRegistrationMap ConcurrentDictionary; registerDrop (custom + quest + global + event drops, loot-team init, FFA schedule), dropModifiers, global-rule restriction checks (race/maps/worlds/ratings/races/tribes/zones/npcs/groups/excluded), addGlobalDrops/addDropItems (member-limit shuffle distribution), collectDrops (Chance.selectElement cap), rank/rating switch-expr modifiers. Collections.shuffle→Fisher-Yates via Rnd; Rnd.chance/get→Rnd.Chance/Get; enum.name().toLowerCase()→ToString().ToLower(); long*=double lossy→explicit cast; Status alias. DAO/GlobalRule/DropNpc/DropService red-tolerated.</summary>
public class DropRegistrationService
{
    private ConcurrentDictionary<int, HashSet<DropItem>> currentDropMap = new ConcurrentDictionary<int, HashSet<DropItem>>();
    private ConcurrentDictionary<int, DropNpc> dropRegistrationMap = new ConcurrentDictionary<int, DropNpc>();

    private DropRegistrationService()
    {
    }

    public void RegisterDrop(Npc npc, Player player, ICollection<Player> groupMembers)
    {
        RegisterDrop(npc, player, player.GetLevel(), groupMembers);
    }

    /// <summary>After NPC dies, it can register arbitrary drop</summary>
    public void RegisterDrop(Npc npc, Player player, int highestLevel, ICollection<Player> groupMembers)
    {
        int npcObjId = npc.GetObjectId();

        // Getting all possible drops for this Npc
        NpcDrop npcDrop = DataManager.CUSTOM_NPC_DROP.GetNpcDrop(npc.GetNpcId());

        List<Player> allowedLooters = new List<Player>();
        Player looter = player;
        int winnerObj = 0;
        Player teamLooter = InitDropNpc(player, npcObjId, allowedLooters, groupMembers);
        if (teamLooter != null)
        {
            looter = teamLooter;
            winnerObj = teamLooter.GetObjectId();
        }

        int index = 1;
        HashSet<DropItem> droppedItems = new HashSet<DropItem>();
        DropModifiers dropModifiers = CreateDropModifiers(npc, looter, highestLevel);

        if (npcDrop != null) // add custom drops
            index = npcDrop.DropCalculator(droppedItems, index, dropModifiers, groupMembers);

        // Updating current dropMap
        currentDropMap[npcObjId] = droppedItems;

        index = QuestService.GetQuestDrop(droppedItems, index, npc, groupMembers, looter);

        // if npc ai == quest_use_item it will be always excluded from global drops
        bool isNpcQuest = npc.GetAi().GetName().Equals("quest_use_item");
        if (!isNpcQuest)
        {
            bool hasGlobalNpcExclusions = HasGlobalNpcExclusions(npc);
            bool isAllowedDefaultGlobalDropNpc = IsAllowedDefaultGlobalDropNpc(npc, dropModifiers.IsDropNpcChest());
            // instances with WorldDropType.NONE must not have global drops (example Arenas)
            if (!hasGlobalNpcExclusions && npc.GetWorldDropType() != WorldDropType.NONE)
            {
                index = AddGlobalDrops(index, dropModifiers, looter, npc, isAllowedDefaultGlobalDropNpc, DataManager.GLOBAL_DROP_DATA.GetAllRules(),
                    droppedItems, groupMembers, winnerObj);
            }
            if (!hasGlobalNpcExclusions || dropModifiers.IsDropNpcChest())
                AddGlobalDrops(index, dropModifiers, looter, npc, isAllowedDefaultGlobalDropNpc, EventService.GetInstance().GetActiveEventDropRules(),
                    droppedItems, groupMembers, winnerObj);
        }

        npc.GetPosition().GetWorldMapInstance().GetInstanceHandler().OnDropRegistered(npc, winnerObj);
        npc.GetAi().OnGeneralEvent(AiEventType.DROP_REGISTERED);

        foreach (Player p in allowedLooters)
        {
            PacketSendUtility.SendPacket(p, new SM_LOOT_STATUS(npcObjId, Status.LOOT_ENABLE));
        }

        DropService.GetInstance().ScheduleFreeForAll(npcObjId);
    }

    public DropModifiers CreateDropModifiers(Npc npc, Player player, int highestLevel)
    {
        DropModifiers dropModifiers = new DropModifiers();
        string dropType = npc.GetGroupDrop().ToString().ToLower();
        bool isChest = npc.GetAi().GetName().Equals("chest") || dropType.StartsWith("treasure") || dropType.EndsWith("box");
        dropModifiers.SetIsDropNpcChest(isChest);
        dropModifiers.SetDropRace(player.GetRace());
        dropModifiers.SetBoostDropRate(CalculateBoostDropRate(player, npc));
        dropModifiers.SetReductionDropRate(GetReductionDropRate(npc, highestLevel));
        return dropModifiers;
    }

    private Player InitDropNpc(Player player, int npcObjId, List<Player> allowedLooters, ICollection<Player> groupMembers)
    {
        Player looter = null;
        DropNpc dropNpc = new DropNpc(npcObjId);
        // Distributing drops to players
        var lootingTeam = player.GetCurrentTeam();
        if (lootingTeam != null)
        {
            LootGroupRules lootGroupRules = lootingTeam.GetLootGroupRules();

            switch (lootGroupRules.GetLootRule())
            {
                case LootRuleType.ROUNDROBIN:
                    int size = groupMembers.Count;
                    if (size > lootGroupRules.GetNrRoundRobin())
                        lootGroupRules.SetNrRoundRobin(lootGroupRules.GetNrRoundRobin() + 1);
                    else
                        lootGroupRules.SetNrRoundRobin(1);

                    int i = 0;
                    foreach (Player p in groupMembers)
                    {
                        i++;
                        if (i == lootGroupRules.GetNrRoundRobin())
                        {
                            allowedLooters.Add(p);
                            looter = p;
                            break;
                        }
                    }
                    break;
                case LootRuleType.FREEFORALL:
                    allowedLooters.AddRange(groupMembers);
                    break;
                case LootRuleType.LEADER:
                    Player leader = player.IsInGroup() ? player.GetPlayerGroup().GetLeaderObject() : player.GetPlayerAlliance().GetLeaderObject();
                    allowedLooters.Add(leader);
                    looter = leader;
                    break;
            }
            dropNpc.SetInRangePlayers(groupMembers);
            dropNpc.SetLootingTeam(lootingTeam);
        }
        else
        {
            allowedLooters.Add(player);
        }
        foreach (Player p in allowedLooters)
            dropNpc.SetAllowedLooter(p);
        dropRegistrationMap[npcObjId] = dropNpc;
        return looter;
    }

    public bool IsAllowedDefaultGlobalDropNpc(Npc npc, bool isChest)
    {
        // exclude most siege spawns, and inner base spawns
        if (npc.GetSpawn() is SiegeSpawnTemplate && npc.GetAbyssNpcType() != AbyssNpcType.DEFENDER)
            return false;
        if (npc.GetSpawn() is BaseSpawnTemplate && npc.GetSpawn().GetHandlerType() != SpawnHandlerType.OUTRIDER
            && npc.GetSpawn().GetHandlerType() != SpawnHandlerType.OUTRIDER_ENHANCED)
            return false;
        // if npc level == 1 means missing stats, so better exclude it from drops
        if (npc.GetLevel() < 2 && !isChest && npc.GetWorldId() != WorldMapType.POETA.GetId() && npc.GetWorldId() != WorldMapType.ISHALGEN.GetId())
            return false;
        // if abyss type npc != null or npc is chest, the npc will be excluded from drops
        if (isChest || npc.GetAbyssNpcType() != AbyssNpcType.NONE && npc.GetAbyssNpcType() != AbyssNpcType.DEFENDER)
            return false;
        return true;
    }

    private int AddGlobalDrops(int index, DropModifiers dropModifiers, Player player, Npc npc, bool isAllowedDefaultGlobalDropNpc,
        List<GlobalRule> rules, HashSet<DropItem> droppedItems, ICollection<Player> groupMembers, int winnerObj)
    {
        foreach (GlobalRule rule in rules)
        {
            // if getGlobalRuleNpcs() != null means drops are for specified npcs (like named drops) so the default restrictions will be ignored
            if (isAllowedDefaultGlobalDropNpc || rule.GetGlobalRuleNpcs() != null)
            {
                float chance = CalculateEffectiveChance(rule, npc, dropModifiers);
                if (Rnd.Chance() >= chance)
                    continue;

                index = AddDropItems(index, droppedItems, rule, npc, player, groupMembers, winnerObj, dropModifiers);
            }
        }
        return index;
    }

    private float? GetReductionDropRate(Npc npc, int highestLevel)
    {
        int dropChance = DropRewardEnumExtensions.DropRewardFrom(npc.GetLevel() - highestLevel); // reduced chance depending on level
        return dropChance == 100 ? (float?)null : dropChance / 100f;
    }

    private float CalculateBoostDropRate(Player killer, Npc npc)
    {
        // Drop rate from NPC can be boosted by Spiritmaster Erosion skill
        int boostDropRate = npc.GetGameStats().GetStat(StatEnum.BOOST_DROP_RATE, 100).GetCurrent();
        // can be exploited on duel with Spiritmaster Erosion skill
        boostDropRate = killer.GetGameStats().GetStat(StatEnum.BOOST_DROP_RATE, boostDropRate).GetCurrent();
        // Drop rate can be boosted by player buff too
        boostDropRate = killer.GetGameStats().GetStat(StatEnum.DR_BOOST, boostDropRate).GetCurrent();

        if (killer.GetCommonData().GetCurrentReposeEnergy() > 0) // EoR 5% Boost drop rate
            boostDropRate += 5;
        if (killer.GetCommonData().GetCurrentSalvationPercent() > 0) // EoS 5% Boost drop rate
            boostDropRate += 5;
        if (killer.GetActiveHouse() != null && killer.GetActiveHouse().GetHouseType() == HouseType.PALACE) // Deed to Palace 5% Boost drop rate
            boostDropRate += 5;

        return Rates.Get(killer, RatesConfig.DROP_RATES) * boostDropRate / 100f;
    }

    public float CalculateEffectiveChance(GlobalRule rule, Npc npc, DropModifiers dropModifiers)
    {
        float chance = rule.GetChance();
        // dynamic_chance means mobs will have different base chances based on their rank and rating
        if (rule.IsDynamicChance())
            chance *= GetRankModifier(npc) * GetRatingModifier(npc);
        return dropModifiers.CalculateDropChance(chance, rule.IsUseLevelBasedChanceReduction());
    }

    private int AddDropItems(int index, HashSet<DropItem> droppedItems, GlobalRule rule, Npc npc, Player player, ICollection<Player> groupMembers,
        int winnerObj, DropModifiers dropModifiers)
    {
        List<GlobalDropItem> drops = CollectDrops(rule, npc, dropModifiers);
        if (drops.Count != 0)
        {
            if (rule.GetMemberLimit() > 1 && player.IsInTeam())
            {
                List<Player> members = new List<Player>(groupMembers);
                if (rule.GetMemberLimit() > members.Count)
                    Shuffle(members);
                int distributedItems = 0;
                foreach (Player member in members)
                {
                    foreach (GlobalDropItem drop in drops)
                    {
                        DropItem dropitem = new DropItem(new Aion.GameServer.Model.Drop.Drop(drop.GetId(), 1, 1, 100));
                        dropitem.SetCount(GetItemCount(drop, npc));
                        dropitem.SetIndex(index++);
                        dropitem.SetPlayerObjId(member.GetObjectId());
                        dropitem.SetWinningPlayer(member);
                        dropitem.IsDistributeItem(true);
                        droppedItems.Add(dropitem);
                    }
                    if (++distributedItems >= rule.GetMemberLimit())
                        break;
                }
            }
            else
            {
                foreach (GlobalDropItem drop in drops)
                {
                    droppedItems.Add(RegDropItem(index++, winnerObj, npc.GetObjectId(), drop.GetId(), GetItemCount(drop, npc)));
                }
            }
        }
        return index;
    }

    public DropItem RegDropItem(int index, int playerObjId, int objId, int itemId, long count)
    {
        DropItem item = new DropItem(new Aion.GameServer.Model.Drop.Drop(itemId, 1, 1, 100));
        item.SetPlayerObjId(playerObjId);
        item.SetNpcObj(objId);
        item.SetCount(count);
        item.SetIndex(index);
        return item;
    }

    /// <summary>dropRegistrationMap</summary>
    public ConcurrentDictionary<int, DropNpc> GetDropRegistrationMap()
    {
        return dropRegistrationMap;
    }

    /// <summary>currentDropMap</summary>
    public ConcurrentDictionary<int, HashSet<DropItem>> GetCurrentDropMap()
    {
        return currentDropMap;
    }

    public static DropRegistrationService GetInstance()
    {
        return SingletonHolder.instance;
    }

    public bool HasGlobalNpcExclusions(Npc npc)
    {
        GlobalNpcExclusionData gde = DataManager.GLOBAL_EXCLUSION_DATA;
        if (!gde.IsEmpty())
        {
            if (gde.GetNpcIds().Contains(npc.GetNpcId()) || gde.GetNpcNames().Contains(npc.GetName())
                || gde.GetNpcTemplateTypes().Contains(npc.GetNpcTemplateType()) || npc.GetTribe() != null && gde.GetNpcTribes().Contains(npc.GetTribe())
                || gde.GetNpcAbyssTypes().Contains(npc.GetAbyssNpcType()))
                return true;
        }
        return false;
    }

    private bool CheckRuleRestrictions(GlobalRule rule, Race? race, Npc npc)
    {
        if (!CheckRestrictionRace(rule, race))
            return false;
        if (!CheckGlobalRuleMaps(rule, npc))
            return false;
        if (!CheckGlobalRuleWorlds(rule, npc))
            return false;
        if (!CheckGlobalRuleRatings(rule, npc))
            return false;
        if (!CheckGlobalRuleRaces(rule, npc))
            return false;
        if (!CheckGlobalRuleTribes(rule, npc))
            return false;
        if (!CheckGlobalRuleZones(rule, npc))
            return false;
        if (!CheckGlobalRuleNpcs(rule, npc))
            return false;
        if (!CheckGlobalRuleNpcGroups(rule, npc)) // drop group from npc_templates
            return false;
        if (!CheckGlobalRuleExcludedNpcs(rule, npc))
            return false;
        return true;
    }

    private bool CheckRestrictionRace(GlobalRule rule, Race? race)
    {
        if (rule.GetRestrictionRace() != null)
        {
            if (race == Race.ASMODIANS && rule.GetRestrictionRace() == GlobalRule.RestrictionRace.ELYOS
                || race == Race.ELYOS && rule.GetRestrictionRace() == GlobalRule.RestrictionRace.ASMODIANS)
                return false;
        }
        return true;
    }

    private bool CheckGlobalRuleMaps(GlobalRule rule, Npc npc)
    {
        if (rule.GetGlobalRuleMaps() != null)
        {
            foreach (GlobalDropMap gdMap in rule.GetGlobalRuleMaps().GetGlobalDropMaps())
                if (gdMap.GetMapId() == npc.GetPosition().GetMapId())
                    return true;
            return false;
        }
        return true;
    }

    private bool CheckGlobalRuleWorlds(GlobalRule rule, Npc npc)
    {
        if (rule.GetGlobalRuleWorlds() != null)
        {
            foreach (GlobalDropWorld gdWorld in rule.GetGlobalRuleWorlds().GetGlobalDropWorlds())
                if (gdWorld.GetWorldDropType().Equals(npc.GetWorldDropType()))
                    return true;
            return false;
        }
        return true;
    }

    private bool CheckGlobalRuleRatings(GlobalRule rule, Npc npc)
    {
        if (rule.GetGlobalRuleRatings() != null)
        {
            foreach (GlobalDropRating gdRating in rule.GetGlobalRuleRatings().GetGlobalDropRatings())
                if (gdRating.GetRating().Equals(npc.GetRating()))
                    return true;
            return false;
        }
        return true;
    }

    private bool CheckGlobalRuleRaces(GlobalRule rule, Npc npc)
    {
        if (rule.GetGlobalRuleRaces() != null)
        {
            foreach (GlobalDropRace gdRace in rule.GetGlobalRuleRaces().GetGlobalDropRaces())
                if (gdRace.GetRace().Equals(npc.GetRace()))
                    return true;
            return false;
        }
        return true;
    }

    private bool CheckGlobalRuleTribes(GlobalRule rule, Npc npc)
    {
        if (rule.GetGlobalRuleTribes() != null)
        {
            foreach (GlobalDropTribe gdTribe in rule.GetGlobalRuleTribes().GetGlobalDropTribes())
                if (gdTribe.GetTribe().Equals(npc.GetTribe()))
                    return true;
            return false;
        }
        return true;
    }

    private bool CheckGlobalRuleZones(GlobalRule rule, Npc npc)
    {
        if (rule.GetGlobalRuleZones() != null)
        {
            foreach (GlobalDropZone gdZone in rule.GetGlobalRuleZones().GetGlobalDropZones())
                if (npc.IsInsideZone(ZoneName.Get(gdZone.GetZone())))
                    return true;
            return false;
        }
        return true;
    }

    private bool CheckGlobalRuleNpcs(GlobalRule rule, Npc npc)
    {
        if (rule.GetGlobalRuleNpcs() != null)
        {
            foreach (GlobalDropNpc gdNpc in rule.GetGlobalRuleNpcs().GetGlobalDropNpcs())
                if (gdNpc.GetNpcId() == npc.GetNpcId())
                    return true;
            return false;
        }
        return true;
    }

    private bool CheckGlobalRuleNpcGroups(GlobalRule rule, Npc npc)
    {
        if (rule.GetGlobalRuleNpcGroups() != null)
        {
            foreach (GlobalDropNpcGroup gdGroup in rule.GetGlobalRuleNpcGroups().GetGlobalDropNpcGroups())
                if (gdGroup.GetGroup().Equals(npc.GetGroupDrop()))
                    return true;
            return false;
        }
        return true;
    }

    private bool CheckGlobalRuleExcludedNpcs(GlobalRule rule, Npc npc)
    {
        if (rule.GetGlobalRuleExcludedNpcs() != null)
            return !rule.GetGlobalRuleExcludedNpcs().GetNpcIds().Contains(npc.GetNpcId());
        return true;
    }

    public List<GlobalDropItem> CollectDrops(GlobalRule rule, Npc npc, DropModifiers dropModifiers)
    {
        int maxDrops = dropModifiers.GetMaxDropsPerGroup() == null ? rule.GetMaxDropRule() : dropModifiers.GetMaxDropsPerGroup().Value;
        List<GlobalDropItem> drops = CollectAllowedDrops(rule, npc, dropModifiers);
        if (drops.Count > maxDrops)
        {
            List<GlobalDropItem> allowedItems = new List<GlobalDropItem>();
            for (int i = 0; i < maxDrops && drops.Count != 0; i++)
            {
                GlobalDropItem item = IChance.SelectElement(drops, true);
                if (item != null)
                    allowedItems.Add(item);
            }
            return allowedItems;
        }
        return drops;
    }

    private List<GlobalDropItem> CollectAllowedDrops(GlobalRule rule, Npc npc, DropModifiers dropModifiers)
    {
        if (!CheckRuleRestrictions(rule, dropModifiers.GetDropRace(), npc))
            return new List<GlobalDropItem>();
        List<GlobalDropItem> tempItems = new List<GlobalDropItem>();
        foreach (GlobalDropItem globalItem in rule.GetDropItems())
        {
            ItemTemplate itemTemplate = DataManager.ITEM_DATA.GetItemTemplate(globalItem.GetId());
            if (itemTemplate.GetRace() == Race.PC_ALL || itemTemplate.GetRace() == dropModifiers.GetDropRace())
            {
                int diff = npc.GetLevel() - itemTemplate.GetLevel();
                if (diff >= rule.GetMinDiff() && diff <= rule.GetMaxDiff())
                    tempItems.Add(globalItem);
            }
        }
        return tempItems;
    }

    private long GetItemCount(GlobalDropItem item, Npc npc)
    {
        long count = Rnd.Get(item.GetMinCount(), item.GetMaxCount());
        if (item.GetId() == ItemId.KINAH)
            count = (long)(count * (npc.GetLevel() * System.Math.Pow(GetRankModifier(npc) * GetRatingModifier(npc), 6)));
        return count;
    }

    private float GetRankModifier(Npc npc)
    {
        return npc.GetRank() switch
        {
            NpcRank.NOVICE => 0.9f,
            NpcRank.DISCIPLINED => 1f,
            NpcRank.SEASONED => 1.05f,
            NpcRank.EXPERT => 1.1f,
            NpcRank.VETERAN => 1.15f,
            NpcRank.MASTER => 1.2f,
            _ => 1f,
        };
    }

    private float GetRatingModifier(Npc npc)
    {
        return npc.GetRating() switch
        {
            NpcRating.JUNK => 0.5f,
            NpcRating.NORMAL => 1f,
            NpcRating.ELITE => 1.3f,
            NpcRating.HERO => 1.8f,
            NpcRating.LEGENDARY => 2f,
            _ => 1f,
        };
    }

    /// <summary>Java parity: Collections.shuffle(members) — in-place Fisher-Yates via Rnd.</summary>
    private static void Shuffle(List<Player> members)
    {
        for (int i = members.Count - 1; i > 0; i--)
        {
            int j = Rnd.Get(0, i);
            (members[i], members[j]) = (members[j], members[i]);
        }
    }

    private static class SingletonHolder
    {
        internal static readonly DropRegistrationService instance = new DropRegistrationService();
    }
}
