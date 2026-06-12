using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Drop;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.Players.Npcfaction;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Model.Skill;
using Aion.GameServer.Model.Team.Alliance;
using Aion.GameServer.Model.Team.Common.Legacy;
using Aion.GameServer.Model.Team.Group;
using Aion.GameServer.Model.Templates;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Model.Templates.Quest;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Handlers.Models;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services.Abyss;
using Aion.GameServer.Services.Drop;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Services.Reward;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Audit;
using Aion.GameServer.Utils.Stats;
using Aion.GameServer.Utils.Time;
using static Aion.GameServer.Model.DialogAction;
using ActionType = Aion.GameServer.Network.Aion.ServerPackets.SM_QUEST_ACTION.ActionType;
using Status = Aion.GameServer.Network.Aion.ServerPackets.SM_LOOT_STATUS.Status;
using ItemUpdateType = Aion.GameServer.Services.Items.ItemPacketService.ItemUpdateType;

namespace Aion.GameServer.Services;

/// <summary>Java parity: services/QuestService (Mr. Poke, vlog, bobobear, xTz, Rolandas) — all-static quest lifecycle/reward/drop logic. using static DialogAction (int consts); nested SM_QUEST_ACTION.ActionType / SM_LOOT_STATUS.Status / ItemPacketService.ItemUpdateType via aliases; HashMap questDrop→Dictionary (getOrDefault/computeIfAbsent); Timestamp/ZonedDateTime/LocalTime/DayOfWeek→DateTimeOffset + DayOfWeek-ISO trap helper; now.with(LocalTime.of(9,0))→explicit 09:00; toEpochSecond→ToUnixTimeSeconds; new Timestamp(ms)→FromUnixTimeMilliseconds; Comparator.comparingInt/streams→LINQ OrderBy; anonymous Runnable→async delegate; Future→ScheduledTask; Rnd.chance→Rnd.Chance; instanceof x→is x; log.error(msg,ex)→LogError(ex,msg). QuestTemplate/QuestEngine/DAO/enums red-tolerated.</summary>
public sealed class QuestService
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(QuestService));
    private static Dictionary<int, List<QuestDrop>> questDrop = new Dictionary<int, List<QuestDrop>>();

    private static int IsoDayValue(DayOfWeek d) => d == DayOfWeek.Sunday ? 7 : (int)d;

    /// <summary>Finishes the quest and rewards the player.</summary>
    public static bool FinishQuest(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int id = env.GetQuestId();
        QuestState qs = player.GetQuestStateList().GetQuestState(id);

        Rewards rewards = new Rewards();
        Rewards extendedRewards = new Rewards();
        if (qs == null || qs.GetStatus() != QuestStatus.REWARD)
            return false;
        QuestTemplate template = DataManager.QUEST_DATA.GetQuestById(id);
        if (template.GetCategory() == QuestCategory.MISSION && qs.GetCompleteCount() != 0)
            return false; // prevent repeatable reward because of wrong quest handling

        ValidateAndFixRewardGroup(qs, id);
        List<QuestItems> questItems = new List<QuestItems>();
        if (template.GetExtendedRewards() != null && qs.GetCompleteCount() == template.GetRewardRepeatCount() - 1)
        { // additional reward for the Xth time
            questItems.AddRange(GetRewardItems(env, template, true, null));
            extendedRewards = template.GetExtendedRewards();
        }
        if (!template.GetRewards().IsEmpty() || template.GetBonus() != null)
        {
            questItems.AddRange(GetRewardItems(env, template, false, qs.GetRewardGroup()));
            if (qs.GetRewardGroup() != null)
                rewards = template.GetRewards()[qs.GetRewardGroup().Value];
        }
        foreach (QuestItems qi in questItems)
            ItemService.AddItem(player, qi.GetItemId(), qi.GetCount(), true);
        GiveReward(env, rewards);
        GiveReward(env, extendedRewards);
        if (template.GetCategory() == QuestCategory.CHALLENGE_TASK)
            ChallengeTaskService.GetInstance().OnChallengeQuestFinish(player, id);
        RemoveQuestWorkItems(player, qs); // remove all worker list item if finished
        qs.SetStatus(QuestStatus.COMPLETE);
        qs.SetQuestVar(0);
        if (template.IsTimeBased())
            qs.SetNextRepeatTime(CalculateRepeatDate(player, template).UtcDateTime);
        PacketSendUtility.SendPacket(player, new SM_QUEST_ACTION(ActionType.UPDATE, qs));
        QuestEngine.QuestEngine.GetInstance().OnQuestCompleted(player, id);
        if (template.GetNpcFactionId() != 0)
            player.GetNpcFactions().CompleteQuest(template);
        player.GetController().UpdateNearbyQuests();
        return true;
    }

    /// <summary>Validates and sets/corrects (if necessary) the reward group which is to be used. Must only be called in reward state.</summary>
    public static void ValidateAndFixRewardGroup(QuestState qs, int questId)
    {
        if (qs != null && qs.GetStatus() == QuestStatus.REWARD)
        {
            List<Rewards> rewardGroups = DataManager.QUEST_DATA.GetQuestById(questId).GetRewards();
            if (qs.GetRewardGroup() != null)
            {
                if (rewardGroups == null)
                {
                    log.LogWarning("Handler for quest " + questId + " has set a reward group, but there are none in quest_data.xml.");
                    qs.SetRewardGroup(null);
                }
                else if (qs.GetRewardGroup() < 0 || qs.GetRewardGroup() >= rewardGroups.Count)
                {
                    log.LogWarning("Handler for quest " + questId + " tried to reward a nonexistent reward group (index " + qs.GetRewardGroup() + ").");
                    qs.SetRewardGroup(rewardGroups.Count - 1);
                }
            }
            else
            { // you must explicitly specify the reward group when there are more than 1
                if (rewardGroups != null && rewardGroups.Count > 0)
                {
                    if (rewardGroups.Count > 1)
                        log.LogWarning("Handler for quest " + questId + " possibly rewarded the wrong reward group.");
                    qs.SetRewardGroup(0);
                }
            }
        }
    }

    private static List<QuestItems> GetRewardItems(QuestEnv env, QuestTemplate template, bool extended, int? rewardGroup)
    {
        Player player = env.GetPlayer();
        int id = env.GetQuestId();
        int dialogActionId = env.GetDialogActionId();
        List<QuestItems> questItems = new List<QuestItems>();
        if (extended)
        {
            Rewards rewards = template.GetExtendedRewards();
            questItems.AddRange(rewards.GetRewardItem());
            if (dialogActionId == SELECTED_QUEST_NOREWARD && !rewards.GetSelectableRewardItem().IsEmpty())
            {
                int index = env.GetExtendedRewardIndex();
                if (index - 8 >= 0 && index - 8 < rewards.GetSelectableRewardItem().Count)
                {
                    questItems.Add(rewards.GetSelectableRewardItem()[index - 8]);
                }
                else if ((index - 1) >= 0 && (index - 1) < rewards.GetSelectableRewardItem().Count)
                {
                    questItems.Add(rewards.GetSelectableRewardItem()[index - 1]);
                }
                else
                {
                    log.LogWarning("The extended SelectableRewardItem list has no element on index " + (index - 8) + ". See quest id " + env.GetQuestId()
                        + ". The size is: " + rewards.GetSelectableRewardItem().Count);
                }
            }
        }
        else
        {
            if (rewardGroup != null)
            {
                Rewards rewards = template.GetRewards()[rewardGroup.Value];
                questItems.AddRange(rewards.GetRewardItem());
                QuestState qs = player.GetQuestStateList().GetQuestState(id);
                PlayerClass playerClass = player.GetCommonData().GetPlayerClass();
                int rewardIndex = GetRewardIndex(env.GetDialogActionId());
                if (rewardIndex >= 0)
                {
                    bool isLastRepeat = qs.GetCompleteCount() == template.GetRewardRepeatCount() - 1;
                    if (isLastRepeat && template.IsSingleTimeClassReward() || template.IsClassRewardOnEveryRepeat())
                    {
                        if (rewardIndex < template.GetSelectableRewardByClass(playerClass).Count)
                        {
                            questItems.Add(template.GetSelectableRewardByClass(playerClass)[rewardIndex]);
                        }
                        else
                        {
                            log.LogWarning("The SelectableRewardByClass list has no element on index " + rewardIndex + ". See quest id " + env.GetQuestId()
                                + ". The size for " + playerClass + " is: " + template.GetSelectableRewardByClass(playerClass).Count);
                        }
                    }
                    else if (rewardIndex < rewards.GetSelectableRewardItem().Count)
                    {
                        questItems.Add(rewards.GetSelectableRewardItem()[rewardIndex]);
                    }
                    else
                    {
                        log.LogWarning("The SelectableRewardItem list has no element on index " + rewardIndex + ". See quest id " + env.GetQuestId());
                    }
                }
                else if (dialogActionId == SELECTED_QUEST_NOREWARD)
                {
                    rewardIndex = env.GetExtendedRewardIndex() - 8;
                    bool isLastRepeat = qs.GetCompleteCount() == template.GetRewardRepeatCount() - 1;
                    if (isLastRepeat && template.IsSingleTimeClassReward() || template.IsClassRewardOnEveryRepeat())
                    {
                        if (rewardIndex >= 0 && rewardIndex < template.GetSelectableRewardByClass(playerClass).Count)
                        {
                            questItems.Add(template.GetSelectableRewardByClass(playerClass)[rewardIndex]);
                        }
                        else
                        {
                            log.LogWarning(new Exception(), "The SelectableRewardByClass list has no element on index " + rewardIndex + ". See quest id " + env.GetQuestId());
                        }
                    }
                }
            }
            if (template.GetBonus() != null)
            {
                // Handler can add additional bonuses on repeat (for event quests no data)
                HandlerResult result = QuestEngine.QuestEngine.GetInstance().OnBonusApplyEvent(env, template.GetBonus().GetType_(), questItems);
                if (result != HandlerResult.FAILED)
                {
                    QuestItems additional = BonusService.GetQuestBonus(player, template);
                    if (additional != null)
                        questItems.Add(additional);
                }
            }
        }

        return questItems;
    }

    /// <summary>Converts the dialog action ID to the corresponding reward ID. Returns the reward index selected, starting at 0. -1 if this action is no reward action.</summary>
    public static int GetRewardIndex(int dialogActionId)
    {
        return dialogActionId >= SELECTED_QUEST_REWARD1 && dialogActionId <= SELECTED_QUEST_REWARD15 ? dialogActionId - SELECTED_QUEST_REWARD1 : -1;
    }

    private static void GiveReward(QuestEnv env, Rewards rewards)
    {
        Player player = env.GetPlayer();
        if (rewards.GetKinah() != 0)
            player.GetInventory().IncreaseKinah(Rates.QUEST_KINAH.CalcResult(player, rewards.GetKinah()), ItemUpdateType.INC_KINAH_QUEST);
        if (rewards.GetExp() != 0)
        {
            NpcTemplate npcTemplate = DataManager.NPC_DATA.GetNpcTemplate(env.GetTargetId());
            player.GetCommonData().AddExp(rewards.GetExp(), Rates.XP_QUEST, npcTemplate != null ? npcTemplate.GetL10n() : null);
        }
        if (rewards.GetTitle() != 0)
            player.GetTitleList().AddTitle(rewards.GetTitle(), true, 0);
        if (rewards.GetAp() != 0)
        {
            int ap = rewards.GetAp();
            if (DataManager.QUEST_DATA.GetQuestById(env.GetQuestId()).GetCategory() != QuestCategory.NON_COUNT) // don't multiply with quest rates for relic exchanges
                ap = Rates.AP_QUEST.CalcResult(player, ap);
            AbyssPointsService.AddAp(player, ap);
        }
        if (rewards.GetDp() != 0)
            player.GetCommonData().AddDp(rewards.GetDp());
        if (rewards.GetGp() != 0)
            GloryPointsService.AddGp(player.GetObjectId(), Rates.GP.CalcResult(player, rewards.GetGp()));
        if (rewards.GetExtendInventory() == 1)
            CubeExpandService.QuestExpand(player);
        else if (rewards.GetExtendInventory() == 2)
            WarehouseService.Expand(player, false);
    }

    private static DateTimeOffset CalculateRepeatDate(Player player, QuestTemplate template)
    {
        DateTimeOffset now = ServerTime.Now();
        DateTimeOffset repeatDate = new DateTimeOffset(now.Year, now.Month, now.Day, 9, 0, 0, now.Offset);
        if (now > repeatDate)
            repeatDate = repeatDate.AddDays(1);
        if (template.IsDaily())
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_QUEST_LIMIT_START_DAILY(9));
        }
        else
        {
            DayOfWeek baseDay = repeatDate.DayOfWeek;
            QuestRepeatCycle nextRepeatDay = FindNextRepeatDay(template.GetRepeatCycle(), baseDay);
            if (nextRepeatDay.GetDay() >= IsoDayValue(baseDay))
                repeatDate = repeatDate.AddDays(nextRepeatDay.GetDay() - IsoDayValue(baseDay));
            else
                repeatDate = repeatDate.AddDays((7 - IsoDayValue(baseDay)) + nextRepeatDay.GetDay());
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_QUEST_LIMIT_START_WEEK(nextRepeatDay.GetL10n(), 9));
        }
        return DateTimeOffset.FromUnixTimeMilliseconds(repeatDate.ToUnixTimeSeconds() * 1000);
    }

    private static QuestRepeatCycle FindNextRepeatDay(List<QuestRepeatCycle> questRepeatDays, DayOfWeek day)
    {
        List<QuestRepeatCycle> resetDaysSorted = questRepeatDays.OrderBy(c => c.GetDay()).ToList();
        foreach (QuestRepeatCycle resetDay in resetDaysSorted)
        {
            if (resetDay.GetDay() >= IsoDayValue(day))
                return resetDay;
        }
        return resetDaysSorted[0];
    }

    public static bool CheckStartConditions(Player player, int questId, bool warn)
    {
        return CheckStartConditions(player, questId, warn, 0, false, false, false);
    }

    public static bool CheckStartConditions(Player player, int questId, bool warn, int allowedDiffToMinLevel, bool skipStartedCheck,
        bool skipRepeatCountCheck, bool skipXmlPreconditionCheck)
    {
        try
        {
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs != null)
            {
                if (!skipStartedCheck && (qs.GetStatus() == QuestStatus.START || qs.GetStatus() == QuestStatus.REWARD))
                {
                    if (warn)
                        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_QUEST_ACQUIRE_ERROR_WORKING_QUEST());
                    return false;
                }
                else if (!skipRepeatCountCheck && qs.GetStatus() == QuestStatus.COMPLETE && !qs.CanRepeat())
                {
                    QuestTemplate template2 = DataManager.QUEST_DATA.GetQuestById(questId);
                    if (template2.GetMaxRepeatCount() > 1 && template2.GetMaxRepeatCount() != 255 && qs.GetCompleteCount() >= template2.GetMaxRepeatCount())
                    {
                        if (warn)
                            PacketSendUtility.SendPacket(player,
                                SM_SYSTEM_MESSAGE.STR_QUEST_ACQUIRE_ERROR_MAX_REPEAT_COUNT(ChatUtil.Quest(questId), template2.GetMaxRepeatCount()));
                    }
                    else
                    {
                        if (warn)
                            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_QUEST_ACQUIRE_ERROR_NONE_REPEATABLE(ChatUtil.Quest(questId)));
                    }
                    return false;
                }
            }

            QuestTemplate template = DataManager.QUEST_DATA.GetQuestById(questId);
            if (template.GetRacePermitted() != null && template.GetRacePermitted() != Race.PC_ALL && template.GetRacePermitted() != player.GetRace())
            {
                if (warn)
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_QUEST_ACQUIRE_ERROR_RACE());
                return false;
            }

            // min level - 2 so that the gray quest arrow shows when quest is almost available
            int levelDiff = template.GetMinlevelPermitted() - allowedDiffToMinLevel - player.GetLevel();
            if (levelDiff > 0)
            {
                if (warn)
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_QUEST_ACQUIRE_ERROR_MIN_LEVEL(template.GetMinlevelPermitted()));
                return false;
            }

            if (template.GetMaxlevelPermitted() != 0 && player.GetLevel() > template.GetMaxlevelPermitted())
            {
                if (warn)
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_QUEST_ACQUIRE_ERROR_MAX_LEVEL(template.GetMaxlevelPermitted()));
                return false;
            }

            if (!template.GetClassPermitted().IsEmpty() && !template.GetClassPermitted().Contains(player.GetPlayerClass()))
            {
                if (warn)
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_QUEST_ACQUIRE_ERROR_CLASS());
                return false;
            }

            if (template.GetGenderPermitted() != null && template.GetGenderPermitted() != player.GetGender())
            {
                if (warn)
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_QUEST_ACQUIRE_ERROR_GENDER());
                return false;
            }

            if (template.GetRequiredRank() != 0 && player.GetAbyssRank().GetRank().GetId() < template.GetRequiredRank())
            {
                if (warn)
                    PacketSendUtility.SendPacket(player,
                        SM_SYSTEM_MESSAGE.STR_QUEST_ACQUIRE_ERROR_MIN_RANK(AbyssRankEnumExtensions.GetRankL10n(player.GetRace(), template.GetRequiredRank())));
                return false;
            }

            if (!skipXmlPreconditionCheck)
            {
                int fulfilledStartConditions = 0;
                foreach (XMLStartCondition startCondition in template.GetXMLStartConditions())
                {
                    if (startCondition.Check(player, warn))
                        fulfilledStartConditions++;
                }
                if (fulfilledStartConditions < template.GetRequiredConditionCount())
                    return false;
            }

            QuestEnv env = new QuestEnv(null, player, questId);
            if (!InventoryItemCheck(env, warn))
                return false;

            if (!CheckCombineSkill(env, warn))
                return false;

            // check if NpcFaction daily quest
            if (template.GetNpcFactionId() != 0)
            {
                // check if the NpcFaction daily time limit has passed
                if (!template.IsTimeBased() && !player.GetNpcFactions().CanStartQuest(template))
                    return false;

                NpcFaction faction = player.GetNpcFactions().GetFactionById(template.GetNpcFactionId());
                if (faction == null || !faction.IsActive())
                    return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            log.LogError(ex, "QE: exception in checkStartCondition (" + player + ", questId " + questId + ")");
        }
        return false;
    }

    public static bool StartQuest(QuestEnv env)
    {
        return StartQuest(env, QuestStatus.START, env.GetDialogActionId() != NULL);
    }

    public static bool StartQuest(QuestEnv env, QuestStatus status, bool warn)
    {
        Player player = env.GetPlayer();
        int id = env.GetQuestId();
        QuestStateList qsl = player.GetQuestStateList();
        QuestState qs = qsl.GetQuestState(id);
        QuestTemplate template = DataManager.QUEST_DATA.GetQuestById(id);
        if (template.GetNpcFactionId() != 0)
        {
            NpcFaction faction = player.GetNpcFactions().GetFactionById(template.GetNpcFactionId());
            if (!faction.IsActive() || faction.GetQuestId() != id)
            {
                AuditLogger.Log(player, "possibly used packet hack to start npc faction quest");
                return false;
            }
        }
        if (!CheckStartConditions(player, id, warn))
            return false;

        if (!template.IsNoCount() && !CheckQuestListSize(qsl) && !player.HasPermission(MembershipConfig.QUEST_LIMIT_DISABLED))
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_QUEST_ACQUIRE_ERROR_MAX_NORMAL());
            return false;
        }

        ActionType actionType;
        if (qs != null)
        {
            actionType = qs.GetStatus() == QuestStatus.COMPLETE ? ActionType.ADD : ActionType.UPDATE;
            qs.SetStatus(status);
        }
        else
        {
            actionType = ActionType.ADD;
            qs = new QuestState(id, status);
            player.GetQuestStateList().AddQuest(id, qs);
        }

        if (template.GetNpcFactionId() != 0 && !template.IsTimeBased())
        {
            player.GetNpcFactions().StartQuest(template);
        }
        if (template.GetCategory() == QuestCategory.CHALLENGE_TASK)
            ChallengeTaskService.GetInstance().OnAcceptTask(player, id);

        PacketSendUtility.SendPacket(player, new SM_QUEST_ACTION(actionType, qs));
        player.GetController().UpdateNearbyQuests();
        return true;
    }

    /// <summary>Adds the quest to the players quest list.</summary>
    public static void AddOrUpdateQuest(Player player, int questId, QuestStatus status)
    {
        ActionType actionType;
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null)
        {
            actionType = ActionType.ADD;
            qs = new QuestState(questId, status);
            player.GetQuestStateList().AddQuest(questId, qs);
        }
        else
        {
            if (qs.GetStatus() == status)
                return;
            actionType = qs.GetStatus() == QuestStatus.COMPLETE ? ActionType.ADD : ActionType.UPDATE;
            qs.SetStatus(status);
            if (status == QuestStatus.COMPLETE)
                qs.SetQuestVar(0);
        }
        PacketSendUtility.SendPacket(player, new SM_QUEST_ACTION(actionType, qs));
    }

    /// <summary>Checks if the crafting/tapping skill point requirements for this quest. Returns true if the quest skill requirement meets the players skill points.</summary>
    public static bool CheckCombineSkill(QuestEnv env, bool warn)
    {
        Player player = env.GetPlayer();
        QuestTemplate template = DataManager.QUEST_DATA.GetQuestById(env.GetQuestId());

        if (template == null)
            return false;

        if (template.GetCombineSkill() != 0)
        {
            List<int> skills = new List<int>(); // skills to check
            if (template.GetCombineSkill() == -1)
            { // any skill
                if (template.GetNpcFactionId() != 12 && template.GetNpcFactionId() != 13)
                { // exclude essence/aether tapping for crafting dailies
                    skills.Add(30002);
                    skills.Add(30003);
                }
                skills.Add(40001);
                skills.Add(40002);
                skills.Add(40003);
                skills.Add(40004);
                skills.Add(40007);
                skills.Add(40008);
                skills.Add(40010);
            }
            else
            {
                skills.Add(template.GetCombineSkill());
            }
            bool result = false;
            foreach (int skillId in skills)
            {
                PlayerSkillEntry skill = player.GetSkillList().GetSkillEntry(skillId);
                if (skill != null && skill.GetSkillLevel() >= template.GetCombineSkillPoint())
                {
                    if (template.GetCategory() == QuestCategory.TASK && skill.GetSkillLevel() - 40 > template.GetCombineSkillPoint())
                        continue;
                    result = true;
                    break;
                }
            }
            if (!result)
            {
                if (warn)
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_QUEST_ACQUIRE_ERROR_TS_RANK(template.GetCombineSkillPoint().ToString()));
                return false;
            }
        }

        return true;
    }

    public static bool StartEventQuest(QuestEnv env, QuestStatus questStatus)
    {
        int id = env.GetQuestId();
        Player player = env.GetPlayer();
        QuestTemplate template = DataManager.QUEST_DATA.GetQuestById(id);
        if (template.GetCategory() != QuestCategory.EVENT)
            return false;

        if (!CheckLevelRequirement(template, player.GetLevel()))
            return false;

        if (template.GetRacePermitted() == player.GetOppositeRace())
            return false;

        if (!template.GetClassPermitted().IsEmpty())
            if (!template.GetClassPermitted().Contains(player.GetCommonData().GetPlayerClass()))
                return false;

        if (template.GetGenderPermitted() != null && template.GetGenderPermitted() != player.GetGender())
            return false;

        QuestState qs = player.GetQuestStateList().GetQuestState(id);
        if (qs == null)
        {
            qs = new QuestState(template.GetId(), questStatus);
            player.GetQuestStateList().AddQuest(id, qs);
        }
        else
        {
            qs.SetStatus(questStatus);
            qs.SetQuestVar(0);
            qs.SetRewardGroup(null);
        }
        return true;
    }

    /// <summary>Check the player's quest list size for starting a new one.</summary>
    private static bool CheckQuestListSize(QuestStateList qsl)
    {
        // The player's quest list size + the new one to start
        return (qsl.GetNormalQuests().Count + 1) <= CustomConfig.BASIC_QUEST_SIZE_LIMIT;
    }

    public static bool CollectItemCheck(QuestEnv env, bool removeItem)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(env.GetQuestId());
        if (qs == null && removeItem)
            return false;
        QuestTemplate template = DataManager.QUEST_DATA.GetQuestById(env.GetQuestId());
        CollectItems collectItems = template.GetCollectItems();
        if (collectItems == null)
        {
            // check inventoryItems to prevent exploits
            InventoryItems inventoryItems = template.GetInventoryItems();
            if (inventoryItems == null)
                return true;

            foreach (InventoryItem inventoryItem in inventoryItems.GetInventoryItems())
            {
                int itemId = inventoryItem.GetItemId();
                if (player.GetInventory().GetItemCountByItemId(itemId) < inventoryItem.GetCount())
                    return false;
            }

            if (removeItem)
            {
                foreach (InventoryItem inventoryItem in inventoryItems.GetInventoryItems())
                {
                    player.GetInventory().DecreaseByItemId(inventoryItem.GetItemId(), inventoryItem.GetCount());
                }
            }
            return true;
        }

        foreach (CollectItem collectItem in collectItems.GetCollectItem())
        {
            int itemId = collectItem.GetItemId().Value;
            long count = itemId == ItemId.KINAH ? player.GetInventory().GetKinah() : player.GetInventory().GetItemCountByItemId(itemId);
            if (collectItem.GetCount() > count)
                return false;
        }
        if (removeItem)
        {
            foreach (CollectItem collectItem in collectItems.GetCollectItem())
            {
                if (collectItem.GetItemId() == ItemId.KINAH)
                    player.GetInventory().DecreaseKinah(collectItem.GetCount().Value);
                else
                {
                    player.GetInventory().DecreaseByItemId(collectItem.GetItemId().Value, collectItem.GetCount().Value);
                }
            }
        }
        return true;
    }

    public static bool InventoryItemCheck(QuestEnv env, bool showWarning)
    {
        Player player = env.GetPlayer();
        QuestTemplate template = DataManager.QUEST_DATA.GetQuestById(env.GetQuestId());
        InventoryItems inventoryItems = template.GetInventoryItems();
        if (inventoryItems != null)
        {
            // Usually counts are 1, and if more, then collect item checks exist
            // Other quests having no collect item checks and counts greater than 1 are unused (old coin exchange quests)
            foreach (InventoryItem inventoryItem in inventoryItems.GetInventoryItems())
            {
                if (player.GetInventory().GetFirstItemByItemId(inventoryItem.GetItemId()) == null)
                {
                    if (showWarning)
                    {
                        string requiredItemL10n = DataManager.ITEM_DATA.GetItemTemplate(inventoryItem.GetItemId()).GetL10n();
                        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_QUEST_ACQUIRE_ERROR_INVENTORY_ITEM(requiredItemL10n));
                    }
                    return false;
                }
            }
        }
        return true;
    }

    /// <summary>Only used by relic reward quests. Checks if the player has any necessary items with sufficient count and starts the quest.</summary>
    public static int CheckAndGetCollectItemQuestRewardCategory(QuestEnv env)
    {
        return CheckAndGetCollectItemQuestRewardCategory(env, null);
    }

    public static int CheckAndGetCollectItemQuestRewardCategory(QuestEnv env, int? rewardIndex)
    {
        Player player = env.GetPlayer();
        QuestTemplate template = DataManager.QUEST_DATA.GetQuestById(env.GetQuestId());

        CollectItems collectItems = template.GetCollectItems();
        if (collectItems == null || template.GetRewards().IsEmpty() || rewardIndex != null && rewardIndex >= template.GetRewards().Count)
            return -1;

        if (rewardIndex == null)
        { // Verify if player has atleast one item with sufficient count and starts quest
            foreach (CollectItem cItem in collectItems.GetCollectItem())
            {
                if (player.GetInventory().GetItemCountByItemId(cItem.GetItemId().Value) >= cItem.GetCount())
                {
                    QuestState qs = player.GetQuestStateList().GetQuestState(env.GetQuestId());
                    if (qs == null || qs.IsStartable())
                    {
                        bool stateValid = true;
                        if (collectItems.GetStartCheck())
                            stateValid = StartQuest(env);
                        if (stateValid)
                            return 0;
                    }
                    else if (qs.GetStatus() != QuestStatus.START && collectItems.GetStartCheck())
                    {
                        return -1;
                    }
                }
            }
        }
        else
        {
            CollectItem selectedOption = collectItems.GetCollectItem()[rewardIndex.Value];
            if (player.GetInventory().GetItemCountByItemId(selectedOption.GetItemId().Value) < selectedOption.GetCount()
                || !player.GetInventory().DecreaseByItemId(selectedOption.GetItemId().Value, selectedOption.GetCount().Value))
            {
                string requiredItemL10n = DataManager.ITEM_DATA.GetItemTemplate(selectedOption.GetItemId().Value).GetL10n();
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_QUEST_COMPLETE_ERROR_QUEST_ITEM_RETRY(requiredItemL10n));
                return -1;
            }
            else
            {
                return rewardIndex.Value;
            }
        }
        return -1;
    }

    public static int GetQuestDrop(HashSet<DropItem> dropItems, int index, Npc npc, ICollection<Player> players, Player player)
    {
        ICollection<QuestDrop> drops = GetQuestDrop(npc.GetNpcId());
        if (drops.Count == 0)
        {
            return index;
        }
        DropNpc dropNpc = DropRegistrationService.GetInstance().GetDropRegistrationMap().GetValueOrDefault(npc.GetObjectId());
        foreach (QuestDrop drop in drops)
        {
            if (Rnd.Chance() >= drop.GetChance())
                continue;

            if (players != null && player.IsInGroup())
            {
                List<Player> pls = new List<Player>();
                if (drop.IsDropEachMemberGroup())
                {
                    foreach (Player member in players)
                    {
                        if (IsQuestDrop(member, drop))
                        {
                            pls.Add(member);
                            dropItems.Add(RegQuestDropItem(drop, index++, member.GetObjectId()));
                        }
                    }
                }
                else
                {
                    foreach (Player member in players)
                    {
                        if (IsQuestDrop(member, drop))
                        {
                            pls.Add(member);
                            break;
                        }
                    }
                }
                if (pls.Count > 0)
                {
                    DropItem dItem = null;
                    if (!drop.IsDropEachMemberGroup())
                    {
                        dItem = RegQuestDropItem(drop, index++, 0);
                        dropItems.Add(dItem);
                    }
                    AllowLooting(pls, dropNpc, dItem);
                }
            }
            else if (players != null && player.IsInAlliance())
            {
                List<Player> pls = new List<Player>();
                if (drop.IsDropEachMemberAlliance())
                {
                    foreach (Player member in players)
                    {
                        if (IsQuestDrop(member, drop))
                        {
                            pls.Add(member);
                            dropItems.Add(RegQuestDropItem(drop, index++, member.GetObjectId()));
                        }
                    }
                }
                else
                {
                    foreach (Player member in players)
                    {
                        if (IsQuestDrop(member, drop))
                        {
                            pls.Add(member);
                            break;
                        }
                    }
                }
                if (pls.Count > 0)
                {
                    DropItem dItem = null;
                    if (!drop.IsDropEachMemberAlliance())
                    {
                        dItem = RegQuestDropItem(drop, index++, 0);
                        dropItems.Add(dItem);
                    }
                    AllowLooting(pls, dropNpc, dItem);
                }
            }
            else
            {
                if (IsQuestDrop(player, drop))
                {
                    dropItems.Add(RegQuestDropItem(drop, index++, player.GetObjectId()));
                }
            }
        }
        return index;
    }

    private static void AllowLooting(List<Player> players, DropNpc dropNpc, DropItem dropItem)
    {
        foreach (Player player in players)
        {
            if (dropItem != null)
                dropItem.SetPlayerObjId(player.GetObjectId());
            dropNpc.SetAllowedLooter(player);
            if (dropNpc.GetLootGroupRules() != null && dropNpc.GetLootGroupRules().GetLootRule() != LootRuleType.FREEFORALL)
            {
                PacketSendUtility.SendPacket(player, new SM_LOOT_STATUS(dropNpc.GetObjectId(), Status.LOOT_ENABLE));
            }
        }
    }

    private static DropItem RegQuestDropItem(QuestDrop drop, int index, int? winner)
    {
        DropItem item = new DropItem(new Drop(drop.GetItemId(), 1, 1, drop.GetChance()));
        item.SetPlayerObjId(winner);
        item.SetIndex(index);
        item.SetCount(1);
        return item;
    }

    private static bool IsQuestDrop(Player player, QuestDrop drop)
    {
        int questId = drop.GetQuestId().Value;
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null || qs.GetStatus() != QuestStatus.START)
        {
            return false;
        }
        if (drop.GetCollectingStep() != 0)
        {
            if (drop.GetCollectingStep() != qs.GetQuestVarById(0))
            {
                return false;
            }
        }
        QuestTemplate qt = DataManager.QUEST_DATA.GetQuestById(questId);
        if (qt.GetTarget().Equals(QuestTarget.ALLIANCE))
        {
            if (!player.IsInAlliance())
            {
                return false;
            }
        }
        if (qt.GetMentorType() == QuestMentorType.MENTE)
        {
            if (!player.IsInGroup())
            {
                return false;
            }

            PlayerGroup group = player.GetPlayerGroup();
            if (!group.GetMembers().Any(member => member.IsMentor() && PositionUtil.IsInRange(player, member, GroupConfig.GROUP_MAX_DISTANCE)))
            {
                return false;
            }
        }
        if (drop is HandlerSideDrop handlerSideDrop)
        {
            return handlerSideDrop.GetNeededAmount() > player.GetInventory().GetItemCountByItemId(drop.GetItemId().Value);
        }

        CollectItems collectItems = DataManager.QUEST_DATA.GetQuestById(questId).GetCollectItems();
        if (collectItems == null)
            return true;

        foreach (CollectItem collectItem in collectItems.GetCollectItem())
        {
            int collectItemId = collectItem.GetItemId().Value;
            long count = player.GetInventory().GetItemCountByItemId(collectItemId);
            if (collectItem.GetCount() > count && drop.GetItemId() == collectItemId)
                return true;
        }
        return false;
    }

    public static bool CheckLevelRequirement(int questId, int playerLevel)
    {
        return CheckLevelRequirement(DataManager.QUEST_DATA.GetQuestById(questId), playerLevel);
    }

    public static bool CheckLevelRequirement(QuestTemplate qt, int playerLevel)
    {
        return playerLevel >= qt.GetMinlevelPermitted() && (qt.GetMaxlevelPermitted() == 0 || playerLevel <= qt.GetMaxlevelPermitted());
    }

    public static int GetLevelRequirementDiff(int questId, int playerLevel)
    {
        QuestTemplate template = DataManager.QUEST_DATA.GetQuestById(questId);
        return template == null ? 99 : template.GetMinlevelPermitted() - playerLevel;
    }

    public static bool QuestTimerStart(QuestEnv env, int timeInSeconds)
    {
        Player player = env.GetPlayer();

        // Schedule Action When Timer Finishes
        ScheduledTask task = ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            QuestEngine.QuestEngine.GetInstance().OnQuestTimerEnd(new QuestEnv(null, player, 0));
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(timeInSeconds * 1000));
        player.GetController().AddTask(TaskId.QUEST_TIMER, task);
        PacketSendUtility.SendPacket(player, new SM_QUEST_ACTION(env.GetQuestId(), timeInSeconds));
        return true;
    }

    public static bool InvisibleTimerStart(QuestEnv env, int timeInSeconds)
    {
        Player player = env.GetPlayer();

        // Schedule Action When Timer Finishes
        ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            QuestEngine.QuestEngine.GetInstance().OnInvisibleTimerEnd(new QuestEnv(null, player, 0));
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(timeInSeconds * 1000));
        return true;
    }

    public static bool QuestTimerEnd(QuestEnv env)
    {
        Player player = env.GetPlayer();

        player.GetController().CancelTask(TaskId.QUEST_TIMER);
        PacketSendUtility.SendPacket(player, new SM_QUEST_ACTION(env.GetQuestId(), 0));
        return true;
    }

    public static bool AbandonQuest(Player player, int questId)
    {
        QuestTemplate template = DataManager.QUEST_DATA.GetQuestById(questId);
        if (template == null)
            return false;

        if (template.IsCannotGiveup())
            return false;

        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null || qs.GetStatus() == QuestStatus.COMPLETE || qs.GetStatus() == QuestStatus.LOCKED)
            return false;

        if (qs.GetCompleteCount() > 0)
        { // set back to complete if it was completed at least once
            qs.SetStatus(QuestStatus.COMPLETE, false);
            qs.SetQuestVar(0);
            qs.SetFlags(0);
        }
        else
        { // entirely delete from players quest list
            player.GetQuestStateList().DeleteQuest(questId);
        }

        if (template.GetNpcFactionId() != 0)
            player.GetNpcFactions().AbortQuest(template);

        RemoveQuestWorkItems(player, qs);
        if (template.GetCategory() == QuestCategory.TASK)
        {
            XMLQuest xmlQuest = DataManager.XML_QUESTS.GetQuest(questId);
            if (xmlQuest is WorkOrdersData workOrdersData)
                player.GetRecipeList().DeleteRecipe(player, workOrdersData.GetRecipeId());
        }

        if (player.GetController().HasTask(TaskId.QUEST_TIMER))
            QuestTimerEnd(new QuestEnv(null, player, questId));

        PacketSendUtility.SendPacket(player, new SM_QUEST_ACTION(ActionType.ABANDON, qs));
        player.GetController().UpdateNearbyQuests();
        return true;
    }

    public static ICollection<QuestDrop> GetQuestDrop(int npcId)
    {
        return questDrop.GetValueOrDefault(npcId, new List<QuestDrop>());
    }

    public static void AddQuestDrop(int npcId, QuestDrop drop)
    {
        if (!questDrop.TryGetValue(npcId, out List<QuestDrop> drops))
        {
            drops = new List<QuestDrop>();
            questDrop[npcId] = drops;
        }
        drops.Add(drop);
    }

    /// <summary>Clears all quest drop info (used when reloading quest data)</summary>
    public static void ClearQuestDrops()
    {
        questDrop.Clear();
    }

    public static List<Player> GetEachDropMembersGroup(PlayerGroup group, int npcId, int questId)
    {
        List<Player> players = new List<Player>();
        foreach (QuestDrop qd in GetQuestDrop(npcId))
        {
            if (qd.IsDropEachMemberGroup())
            {
                foreach (Player player in group.GetMembers())
                {
                    QuestState qstel = player.GetQuestStateList().GetQuestState(questId);
                    if (qstel != null && qstel.GetStatus() == QuestStatus.START)
                    {
                        players.Add(player);
                    }
                }
                break;
            }
        }
        return players;
    }

    public static List<Player> GetEachDropMembersAlliance(PlayerAlliance alliance, int npcId, int questId)
    {
        List<Player> players = new List<Player>();
        foreach (QuestDrop qd in GetQuestDrop(npcId))
        {
            if (qd.IsDropEachMemberGroup())
            {
                foreach (Player player in alliance.GetMembers())
                {
                    QuestState qstel = player.GetQuestStateList().GetQuestState(questId);
                    if (qstel != null && qstel.GetStatus() == QuestStatus.START)
                    {
                        players.Add(player);
                    }
                }
                break;
            }
        }
        return players;
    }

    public static void RemoveQuestWorkItems(Player player, QuestState qs)
    {
        QuestWorkItems qwi = DataManager.QUEST_DATA.GetQuestById(qs.GetQuestId()).GetQuestWorkItems();
        if (qwi != null)
        {
            foreach (QuestItems qi in qwi.GetQuestWorkItem())
            {
                if (qi != null)
                {
                    long count = player.GetInventory().GetItemCountByItemId(qi.GetItemId());
                    if (count > 0)
                        player.GetInventory().DecreaseByItemId(qi.GetItemId(), count, qs.GetStatus());
                }
            }
        }
    }
}
