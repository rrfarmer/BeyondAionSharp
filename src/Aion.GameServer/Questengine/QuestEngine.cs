using System;
using System.Collections.Generic;
using System.Linq;
using Aion.Commons.Scripting;
using Aion.Commons.Scripting.ClassListener;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Model.Templates.Quest;
using Aion.GameServer.Model.Templates.Rewards;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Handlers.Models;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Cron;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Collections;
using Aion.GameServer.Utils.Stats;
using Aion.GameServer.World;
using Aion.GameServer.World.Zone;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Quartz;

namespace Aion.GameServer.QuestEngine;

/// <summary>Java parity: questEngine/QuestEngine (MrPoke, Hilgert, vlog, Neon). The central quest registration/dispatch god-class every handler's `qe` references. EnumMap→Dictionary; computeIfAbsent→TryGetValue+init; putIfAbsent→TryAdd; Object...→params object[]; SingletonHolder→static readonly. C# GameEngine interface is a divergent async redesign — Java's void Init() mirrored faithfully, interface satisfaction left red. ScriptManager/CronService/Quartz JobDetail/SplitList/World red-tolerated.</summary>
public class QuestEngine : GameEngine
{

    // GameEngine async lifecycle (infra adapter over the faithful Init()).
    public string Name => GetType().Name;
    public System.Threading.Tasks.ValueTask InitAsync(System.Threading.CancellationToken cancellationToken) { Init(); return System.Threading.Tasks.ValueTask.CompletedTask; }
    public System.Threading.Tasks.ValueTask ShutdownAsync(System.Threading.CancellationToken cancellationToken) => System.Threading.Tasks.ValueTask.CompletedTask;
    private static readonly ILogger log = NullLogger.Instance;
    private readonly ScriptManager scriptManager = new();
    private IJobDetail messageTask;
    private readonly Dictionary<int, AbstractQuestHandler> questHandlers = new();
    private readonly Dictionary<int, QuestNpc> questNpcs = new();
    private readonly Dictionary<int, List<int>> questItemRelated = new();
    private readonly List<int> questHouseItems = new();
    private readonly Dictionary<int, List<int>> questItems = new();
    private readonly List<int> questOnCompleted = new();
    private readonly Dictionary<Race, List<int>> questOnLevelUp = new();
    private readonly List<int> questOnDie = new();
    private readonly List<int> questOnLogOut = new();
    private readonly List<int> questOnEnterWorld = new();
    private readonly Dictionary<ZoneName, List<int>> questOnEnterZone = new();
    private readonly Dictionary<ZoneName, List<int>> questOnLeaveZone = new();
    private readonly Dictionary<string, List<int>> questOnPassFlyingRings = new();
    private readonly List<int> questOnTimerEnd = new();
    private readonly List<int> onInvisibleTimerEnd = new();
    private readonly Dictionary<AbyssRankEnum, List<int>> questOnKillRanked = new();
    private readonly Dictionary<int, List<int>> questOnKillInWorld = new();
    private readonly Dictionary<int, List<int>> questOnUseSkill = new();
    private readonly Dictionary<int, int> questOnFailCraft = new();
    private readonly Dictionary<int, List<int>> questOnEquipItem = new();
    private readonly Dictionary<int, List<int>> questCanAct = new();
    private readonly List<int> questOnDredgionReward = new();
    private readonly Dictionary<BonusType, List<int>> questOnBonusApply = new();
    private readonly List<int> questUpdateItems = new();
    private readonly List<int> reachTarget = new();
    private readonly List<int> lostTarget = new();
    private readonly List<int> questOnEnterWindStream = new();
    private readonly List<int> questRideAction = new();
    private readonly Dictionary<string, List<int>> questOnKillInZone = new();

    private QuestEngine()
    {
    }

    public void Init()
    {
        foreach (QuestTemplate data in DataManager.QUEST_DATA.GetQuestTemplates())
        {
            foreach (QuestDrop drop in data.GetQuestDrop())
            {
                drop.SetQuestId(data.GetId());
                QuestService.AddQuestDrop(drop.GetNpcId(), drop);
            }
            if (data.GetInventoryItems() != null)
            {
                foreach (InventoryItem inventoryItem in data.GetInventoryItems().GetInventoryItems())
                {
                    if (!questUpdateItems.Contains(inventoryItem.GetItemId()))
                        questUpdateItems.Add(inventoryItem.GetItemId());
                }
            }
        }
        AggregatedClassListener acl = new();
        acl.AddClassListener(new OnClassLoadUnloadListener());
        acl.AddClassListener(new QuestHandlerLoader());
        scriptManager.SetGlobalClassListener(acl);
        scriptManager.Load(GSConfig.QUEST_HANDLER_DIRECTORY);
        foreach (XMLQuest xmlQuest in DataManager.XML_QUESTS.GetAllQuests())
            xmlQuest.Register(this);
        log.LogInformation("Loaded " + questHandlers.Count + " quest handlers.");
        if (GSConfig.ANALYZE_QUESTHANDLERS)
            ThreadPoolManager.GetInstance().ExecuteLongRunning(() => QuestSpawnAnalyzer.Run(questHandlers.Values, questNpcs.Values, true));
        AddMessageSendingTask();
    }

    public void Reload()
    {
        scriptManager.Shutdown();
        Clear();
        Init();
    }

    public void Clear()
    {
        CronService.GetInstance().Cancel(messageTask);
        QuestService.ClearQuestDrops();
        questNpcs.Clear();
        questItemRelated.Clear();
        questItems.Clear();
        questHouseItems.Clear();
        questOnLevelUp.Clear();
        questOnCompleted.Clear();
        questOnEnterWorld.Clear();
        questOnDie.Clear();
        questOnLogOut.Clear();
        questOnEnterZone.Clear();
        questOnLeaveZone.Clear();
        questOnTimerEnd.Clear();
        onInvisibleTimerEnd.Clear();
        questOnPassFlyingRings.Clear();
        questOnKillRanked.Clear();
        questOnKillInWorld.Clear();
        questOnKillInZone.Clear();
        questOnUseSkill.Clear();
        questOnFailCraft.Clear();
        questOnEquipItem.Clear();
        questCanAct.Clear();
        questOnDredgionReward.Clear();
        questOnBonusApply.Clear();
        questUpdateItems.Clear();
        reachTarget.Clear();
        lostTarget.Clear();
        questOnEnterWindStream.Clear();
        questRideAction.Clear();
        questHandlers.Clear();
    }

    public bool OnDialog(QuestEnv env)
    {
        try
        {
            AbstractQuestHandler questHandler;
            if (env.GetQuestId() != 0)
            {
                questHandler = GetQuestHandlerByQuestId(env.GetQuestId());
                if (questHandler != null)
                    if (questHandler.OnDialogEvent(env))
                        return true;
                    else
                    {
                        QuestTemplate qt = DataManager.QUEST_DATA.GetQuestById(env.GetQuestId());
                        if (qt != null && qt.GetCategory() == QuestCategory.CHALLENGE_TASK)
                            PacketSendUtility.SendPacket(env.GetPlayer(), SM_SYSTEM_MESSAGE.STR_MSG_QUEST_LIMIT_START_DAILY(9));
                    }
            }
            else
            {
                Npc npc = (Npc)env.GetVisibleObject();
                foreach (int questId in GetQuestNpc(npc == null ? 0 : npc.GetNpcId()).GetOnTalkEvent())
                {
                    questHandler = GetQuestHandlerByQuestId(questId);
                    if (questHandler != null)
                    {
                        env.SetQuestId(questId);
                        if (questHandler.OnDialogEvent(env))
                            return true;
                    }
                }
                env.SetQuestId(0);
            }
        }
        catch (Exception ex)
        {
            log.LogError(ex, "QE: exception in onDialog");
            return false;
        }
        return false;
    }

    public bool OnKill(QuestEnv env)
    {
        try
        {
            Npc npc = (Npc)env.GetVisibleObject();
            foreach (int questId in GetQuestNpc(npc.GetNpcId()).GetOnKillEvent())
            {
                AbstractQuestHandler questHandler = GetQuestHandlerByQuestId(questId);
                if (questHandler != null)
                {
                    env.SetQuestId(questId);
                    questHandler.OnKillEvent(env);
                }
            }
        }
        catch (Exception ex)
        {
            log.LogError(ex, "QE: exception in onKill");
            return false;
        }
        return true;
    }

    public bool OnAttack(QuestEnv env)
    {
        try
        {
            Npc npc = (Npc)env.GetVisibleObject();
            foreach (int questId in GetQuestNpc(npc.GetNpcId()).GetOnAttackEvent())
            {
                AbstractQuestHandler questHandler = GetQuestHandlerByQuestId(questId);
                if (questHandler != null)
                {
                    env.SetQuestId(questId);
                    questHandler.OnAttackEvent(env);
                }
            }
        }
        catch (Exception ex)
        {
            log.LogError(ex, "QE: exception in onAttack");
            return false;
        }
        return true;
    }

    public void SendCompletedQuests(Player player)
    {
        SplitList<QuestState> questStateSplitList = new DynamicServerPacketBodySplitList<QuestState>(player.GetQuestStateList().GetCompletedQuests(), true,
            SM_QUEST_COMPLETED_LIST.STATIC_BODY_SIZE, SM_QUEST_COMPLETED_LIST.DYNAMIC_BODY_PART_SIZE_CALCULATOR);
        questStateSplitList.ForEach(part => PacketSendUtility.SendPacket(player, new SM_QUEST_COMPLETED_LIST(part.IsFirst() ? 0 : 1, part)));
    }

    /// <summary>
    /// Notifies all quest handlers (which registered the event), that the player level changed
    /// </summary>
    /// <param name="player">The player who leveled up</param>
    public void OnLevelChanged(Player player)
    {
        try
        {
            List<int> raceQuestsOnLevelUp = GetOrCreateOnLevelUpForRace(player.GetRace());
            foreach (int questId in raceQuestsOnLevelUp)
            {
                QuestState qs = player.GetQuestStateList().GetQuestState(questId);
                if (qs == null || qs.GetStatus() != QuestStatus.COMPLETE)
                {
                    AbstractQuestHandler questHandler = GetQuestHandlerByQuestId(questId);
                    if (questHandler != null)
                        questHandler.OnLevelChangedEvent(player);
                }
            }
        }
        catch (Exception ex)
        {
            log.LogError(ex, "QE: exception in onLevelChanged");
        }
    }

    /// <summary>
    /// Notifies all quest handlers (which registered the event), that the quest with the specified ID completed
    /// </summary>
    /// <param name="player">Player who completed the quest</param>
    /// <param name="questId">The quest that the player completed</param>
    public void OnQuestCompleted(Player player, int questId)
    {
        try
        {
            QuestEnv env = new(null, player, questId);
            foreach (int onCompletedQuestId in questOnCompleted)
            {
                AbstractQuestHandler questHandler = GetQuestHandlerByQuestId(onCompletedQuestId);
                if (questHandler != null)
                    questHandler.OnQuestCompletedEvent(env);
            }
        }
        catch (Exception ex)
        {
            log.LogError(ex, "QE: exception in onQuestCompleted");
        }
    }

    public void OnDie(QuestEnv env)
    {
        try
        {
            foreach (int questId in questOnDie)
            {
                AbstractQuestHandler questHandler = GetQuestHandlerByQuestId(questId);
                if (questHandler != null)
                {
                    env.SetQuestId(questId);
                    questHandler.OnDieEvent(env);
                }
            }
        }
        catch (Exception ex)
        {
            log.LogError(ex, "QE: exception in onDie");
        }
    }

    public void OnLogOut(QuestEnv env)
    {
        try
        {
            foreach (int questId in questOnLogOut)
            {
                AbstractQuestHandler questHandler = GetQuestHandlerByQuestId(questId);
                if (questHandler != null)
                {
                    env.SetQuestId(questId);
                    questHandler.OnLogOutEvent(env);
                }
            }
        }
        catch (Exception ex)
        {
            log.LogError(ex, "QE: exception in onLogOut");
        }
    }

    public void OnNpcReachTarget(QuestEnv env)
    {
        try
        {
            foreach (int questId in reachTarget)
            {
                AbstractQuestHandler questHandler = GetQuestHandlerByQuestId(questId);
                if (questHandler != null)
                {
                    env.SetQuestId(questId);
                    questHandler.OnNpcReachTargetEvent(env);
                }
            }
        }
        catch (Exception ex)
        {
            log.LogError(ex, "QE: exception in onNpcReachTarget");
        }
    }

    public void OnNpcLostTarget(QuestEnv env)
    {
        try
        {
            foreach (int questId in lostTarget)
            {
                AbstractQuestHandler questHandler = GetQuestHandlerByQuestId(questId);
                if (questHandler != null)
                {
                    env.SetQuestId(questId);
                    questHandler.OnNpcLostTargetEvent(env);
                }
            }
        }
        catch (Exception ex)
        {
            log.LogError(ex, "QE: exception in onNpcLostTarget");
        }
    }

    public void OnPassFlyingRing(QuestEnv env, string flyRing)
    {
        try
        {
            if (questOnPassFlyingRings.TryGetValue(flyRing, out List<int> questIds))
            {
                foreach (int questId in questIds)
                {
                    AbstractQuestHandler questHandler = GetQuestHandlerByQuestId(questId);
                    if (questHandler != null)
                    {
                        env.SetQuestId(questId);
                        questHandler.OnPassFlyingRingEvent(env, flyRing);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            log.LogError(ex, "QE: exception in onFlyRingPassEvent");
        }
    }

    public void OnEnterWorld(Player player)
    {
        try
        {
            foreach (int questId in questOnEnterWorld)
            {
                AbstractQuestHandler questHandler = GetQuestHandlerByQuestId(questId);
                if (questHandler != null)
                    questHandler.OnEnterWorldEvent(new QuestEnv(null, player, questId));
            }
        }
        catch (Exception ex)
        {
            log.LogError(ex, "QE: exception in onEnterWorld");
        }
    }

    public HandlerResult OnItemUseEvent(QuestEnv env, Item item)
    {
        try
        {
            if (questItemRelated.TryGetValue(item.GetItemId(), out List<int> questIds))
            {
                foreach (int questId in questIds)
                {
                    AbstractQuestHandler questHandler = GetQuestHandlerByQuestId(questId);
                    if (questHandler != null)
                    {
                        env.SetQuestId(questId);
                        HandlerResult result = questHandler.OnItemUseEvent(env, item);
                        // allow other quests to process, the same item can be used in multiple quests
                        if (result != HandlerResult.UNKNOWN)
                            return result;
                    }
                }
            }
            return HandlerResult.UNKNOWN;
        }
        catch (Exception ex)
        {
            log.LogError(ex, "QE: exception in onItemUseEvent");
            return HandlerResult.FAILED;
        }
    }

    public void OnHouseItemUseEvent(QuestEnv env)
    {
        try
        {
            foreach (int questHouseItem in questHouseItems)
            {
                AbstractQuestHandler questHandler = GetQuestHandlerByQuestId(questHouseItem);
                if (questHandler != null)
                {
                    env.SetQuestId(questHouseItem);
                    questHandler.OnHouseItemUseEvent(env);
                }
            }
        }
        catch (Exception ex)
        {
            log.LogError(ex, "QE: exception in onHouseItemUseEvent");
        }
    }

    public void OnItemGet(Player player, int itemId)
    {
        if (questItems.TryGetValue(itemId, out List<int> questIds))
        {
            for (int i = 0; i < questIds.Count; i++)
            {
                int questId = questItems[itemId][i];
                AbstractQuestHandler questHandler = GetQuestHandlerByQuestId(questId);
                if (questHandler != null)
                    questHandler.OnGetItemEvent(new QuestEnv(null, player, questId));
            }
        }
        if (questUpdateItems.Contains(itemId))
            player.GetController().UpdateNearbyQuests();
    }

    public void OnItemRemoved(Player player, int itemId)
    {
        if (questUpdateItems.Contains(itemId))
            player.GetController().UpdateNearbyQuests();
    }

    public bool OnKillRanked(QuestEnv env, AbyssRankEnum playerRank)
    {
        try
        {
            if (playerRank != null)
            {
                if (questOnKillRanked.TryGetValue(playerRank, out List<int> questIds))
                {
                    foreach (int questId in questIds)
                    {
                        AbstractQuestHandler questHandler = GetQuestHandlerByQuestId(questId);
                        if (questHandler != null)
                        {
                            env.SetQuestId(questId);
                            questHandler.OnKillRankedEvent(env);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            log.LogError(ex, "QE: exception in onKillRanked");
            return false;
        }
        return true;
    }

    public bool OnKillInWorld(QuestEnv env, int worldId)
    {
        try
        {
            if (questOnKillInWorld.TryGetValue(worldId, out List<int> questIds))
            {
                foreach (int questId in questIds)
                {
                    AbstractQuestHandler questHandler = GetQuestHandlerByQuestId(questId);
                    if (questHandler != null)
                    {
                        env.SetQuestId(questId);
                        questHandler.OnKillInWorldEvent(env);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            log.LogError(ex, "QE: exception in onKillInWorld");
            return false;
        }
        return true;
    }

    public bool OnKillInZone(QuestEnv env, string zoneName)
    {
        try
        {
            if (questOnKillInZone.TryGetValue(zoneName, out List<int> questIds))
            {
                foreach (int questId in questIds)
                {
                    AbstractQuestHandler questHandler = GetQuestHandlerByQuestId(questId);
                    if (questHandler != null)
                    {
                        env.SetQuestId(questId);
                        questHandler.OnKillInZoneEvent(env);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            log.LogError(ex, "QE: exception in onKillInZone");
            return false;
        }
        return true;
    }

    public bool OnEnterZone(QuestEnv env, ZoneName zoneName)
    {
        try
        {
            if (questOnEnterZone.TryGetValue(zoneName, out List<int> questIds))
            {
                foreach (int questId in questIds)
                {
                    AbstractQuestHandler questHandler = GetQuestHandlerByQuestId(questId);
                    if (questHandler != null)
                    {
                        env.SetQuestId(questId);
                        questHandler.OnEnterZoneEvent(env, zoneName);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            log.LogError(ex, "QE: exception in onEnterZone");
            return false;
        }
        return true;
    }

    public bool OnLeaveZone(QuestEnv env, ZoneName zoneName)
    {
        try
        {
            if (questOnLeaveZone.TryGetValue(zoneName, out List<int> questIds))
            {
                foreach (int questId in questIds)
                {
                    AbstractQuestHandler questHandler = GetQuestHandlerByQuestId(questId);
                    if (questHandler != null)
                    {
                        env.SetQuestId(questId);
                        questHandler.OnLeaveZoneEvent(env, zoneName);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            log.LogError(ex, "QE: exception in onLeaveZone");
            return false;
        }
        return true;
    }

    public void OnMovieEnd(QuestEnv env, int movieId)
    {
        try
        {
            AbstractQuestHandler questHandler = GetQuestHandlerByQuestId(env.GetQuestId());
            if (questHandler != null)
                questHandler.OnMovieEndEvent(env, movieId);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "QE: exception in onMovieEnd");
        }
    }

    public void OnQuestTimerEnd(QuestEnv env)
    {
        try
        {
            foreach (int questId in questOnTimerEnd)
            {
                AbstractQuestHandler questHandler = GetQuestHandlerByQuestId(questId);
                if (questHandler != null)
                {
                    env.SetQuestId(questId);
                    questHandler.OnQuestTimerEndEvent(env);
                }
            }
        }
        catch (Exception ex)
        {
            log.LogError(ex, "QE: exception in onQuestTimerEnd");
        }
    }

    public void OnInvisibleTimerEnd(QuestEnv env)
    {
        try
        {
            foreach (int questId in onInvisibleTimerEnd)
            {
                AbstractQuestHandler questHandler = GetQuestHandlerByQuestId(questId);
                if (questHandler != null)
                {
                    env.SetQuestId(questId);
                    questHandler.OnInvisibleTimerEndEvent(env);
                }
            }
        }
        catch (Exception ex)
        {
            log.LogError(ex, "QE: exception in onInvisibleTimerEnd");
        }
    }

    public bool OnUseSkill(QuestEnv env, int skillId)
    {
        try
        {
            if (questOnUseSkill.TryGetValue(skillId, out List<int> questIds))
            {
                foreach (int questId in questIds)
                {
                    AbstractQuestHandler questHandler = GetQuestHandlerByQuestId(questId);
                    if (questHandler != null)
                    {
                        env.SetQuestId(questId);
                        questHandler.OnUseSkillEvent(env, skillId);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            log.LogError(ex, "QE: exception in onUseSkill");
            return false;
        }
        return true;
    }

    public void OnFailCraft(QuestEnv env, int itemId)
    {
        if (questOnFailCraft.TryGetValue(itemId, out int questId))
        {
            AbstractQuestHandler questHandler = GetQuestHandlerByQuestId(questId);
            if (questHandler != null)
            {
                if (env.GetPlayer().GetInventory().GetItemCountByItemId(itemId) == 0)
                {
                    env.SetQuestId(questId);
                    questHandler.OnFailCraftEvent(env, itemId);
                }
            }
        }
    }

    public void OnEquipItem(QuestEnv env, int itemId)
    {
        if (questOnEquipItem.TryGetValue(itemId, out List<int> questIds))
        {
            foreach (int questId in questIds)
            {
                AbstractQuestHandler questHandler = GetQuestHandlerByQuestId(questId);
                if (questHandler != null)
                {
                    env.SetQuestId(questId);
                    questHandler.OnEquipItemEvent(env, itemId);
                }
            }
        }
    }

    public bool OnCanAct(QuestEnv env, int templateId, QuestActionType questActionType, params object[] objects)
    {
        if (questCanAct.TryGetValue(templateId, out List<int> questIds))
        {
            foreach (int questId in questIds)
            {
                AbstractQuestHandler questHandler = GetQuestHandlerByQuestId(questId);
                if (questHandler != null)
                {
                    env.SetQuestId(questId);
                    if (questHandler.OnCanAct(env, questActionType, objects))
                        return true;
                }
            }
        }
        return false;
    }

    public void OnDredgionReward(QuestEnv env)
    {
        try
        {
            foreach (int questId in questOnDredgionReward)
            {
                AbstractQuestHandler questHandler = GetQuestHandlerByQuestId(questId);
                if (questHandler != null)
                {
                    env.SetQuestId(questId);
                    questHandler.OnDredgionRewardEvent(env);
                }
            }
        }
        catch (Exception ex)
        {
            log.LogError(ex, "QE: exception in onDredgionReward");
        }
    }

    public HandlerResult OnBonusApplyEvent(QuestEnv env, BonusType bonusType, List<QuestItems> rewardItems)
    {
        try
        {
            if (questOnBonusApply.TryGetValue(bonusType, out List<int> questIds))
            {
                foreach (int questId in questIds)
                {
                    AbstractQuestHandler questHandler = GetQuestHandlerByQuestId(questId);
                    if (questHandler != null)
                    {
                        env.SetQuestId(questId);
                        return questHandler.OnBonusApplyEvent(env, bonusType, rewardItems);
                    }
                }
            }
            return HandlerResult.UNKNOWN;
        }
        catch (Exception ex)
        {
            log.LogError(ex, "QE: exception in onBonusApply");
            return HandlerResult.FAILED;
        }
    }

    public bool OnAddAggroList(QuestEnv env)
    {
        try
        {
            Npc npc = (Npc)env.GetVisibleObject();
            foreach (int questId in GetQuestNpc(npc.GetNpcId()).GetOnAddAggroListEvent())
            {
                AbstractQuestHandler questHandler = GetQuestHandlerByQuestId(questId);
                if (questHandler != null)
                {
                    env.SetQuestId(questId);
                    questHandler.OnAddAggroListEvent(env);
                }
            }
        }
        catch (Exception ex)
        {
            log.LogError(ex, "QE: exception in onAddAggroList");
            return false;
        }
        return true;
    }

    public bool OnAtDistance(QuestEnv env)
    {
        QuestNpc questNpc;
        Npc npc = (Npc)env.GetVisibleObject();
        if (!questNpcs.ContainsKey(npc.GetNpcId()))
        {
            return false;
        }
        questNpc = GetQuestNpc(npc.GetNpcId());
        if (GetQuestNpc(npc.GetNpcId()).GetOnDistanceEvent().Count == 0)
            return false;
        Player player = env.GetPlayer();
        if (!PositionUtil.IsInRange(npc, player, questNpc.GetQuestRange()))
            return false;
        try
        {
            foreach (int questId in questNpc.GetOnDistanceEvent())
            {
                AbstractQuestHandler questHandler = GetQuestHandlerByQuestId(questId);
                if (questHandler != null)
                {
                    env.SetQuestId(questId);
                    questHandler.OnAtDistanceEvent(env);
                }
            }
        }
        catch (Exception ex)
        {
            log.LogError(ex, "QE: exception in onAtDistance");
            return false;
        }
        return true;
    }

    public void OnEnterWindStream(QuestEnv env, int loc)
    {
        try
        {
            foreach (int questId in questOnEnterWindStream)
            {
                AbstractQuestHandler questHandler = GetQuestHandlerByQuestId(questId);
                if (questHandler != null)
                {
                    env.SetQuestId(questId);
                    questHandler.OnEnterWindStreamEvent(env, loc);
                }
            }
        }
        catch (Exception ex)
        {
            log.LogError(ex, "QE: exception in onWindStream");
        }
    }

    public void RideAction(QuestEnv env, int itemId)
    {
        try
        {
            foreach (int questId in questRideAction)
            {
                AbstractQuestHandler questHandler = GetQuestHandlerByQuestId(questId);
                if (questHandler != null)
                {
                    env.SetQuestId(questId);
                    questHandler.RideAction(env, itemId);
                }
            }
        }
        catch (Exception ex)
        {
            log.LogError(ex, "QE: exception in rideAction");
        }
    }

    public QuestNpc RegisterQuestNpc(int npcId)
    {
        if (!questNpcs.ContainsKey(npcId))
        {
            questNpcs[npcId] = new QuestNpc(npcId);
        }
        return questNpcs[npcId];
    }

    public QuestNpc RegisterQuestNpc(int npcId, int range)
    {
        if (!questNpcs.ContainsKey(npcId))
        {
            questNpcs[npcId] = new QuestNpc(npcId, range);
        }
        return questNpcs[npcId];
    }

    public void RegisterQuestItem(int itemId, int questId)
    {
        GetOrCreate(questItemRelated, itemId).Add(questId);
    }

    public bool IsRegisteredQuestItem(int itemId)
    {
        return questItemRelated.ContainsKey(itemId);
    }

    public void RegisterQuestHouseItem(int questId)
    {
        if (!questHouseItems.Contains(questId))
            questHouseItems.Add(questId);
    }

    public void RegisterOnGetItem(int itemId, int questId)
    {
        GetOrCreate(questItems, itemId).Add(questId);
    }

    public void RegisterOnLevelChanged(int questId)
    {
        QuestTemplate template = DataManager.QUEST_DATA.GetQuestById(questId);
        Race racePermitted = template.GetRacePermitted();
        List<int> quests;
        if (racePermitted == null)
        {
            quests = GetOrCreateOnLevelUpForRace(Race.ASMODIANS);
            if (!quests.Contains(questId))
                quests.Add(questId);
            quests = GetOrCreateOnLevelUpForRace(Race.ELYOS);
            if (!quests.Contains(questId))
                quests.Add(questId);
        }
        else
        {
            quests = GetOrCreateOnLevelUpForRace(racePermitted);
            if (!quests.Contains(questId))
                quests.Add(questId);
        }
    }

    private List<int> GetOrCreateOnLevelUpForRace(Race race)
    {
        return GetOrCreate(questOnLevelUp, race);
    }

    public void RegisterOnQuestCompleted(int questId)
    {
        if (!questOnCompleted.Contains(questId))
            questOnCompleted.Add(questId);
    }

    public void RegisterOnEnterWorld(int questId)
    {
        if (!questOnEnterWorld.Contains(questId))
            questOnEnterWorld.Add(questId);
    }

    public void RegisterOnDie(int questId)
    {
        if (!questOnDie.Contains(questId))
            questOnDie.Add(questId);
    }

    public void RegisterOnLogOut(int questId)
    {
        if (!questOnLogOut.Contains(questId))
            questOnLogOut.Add(questId);
    }

    public void RegisterOnEnterZone(ZoneName zoneName, int questId)
    {
        GetOrCreate(questOnEnterZone, zoneName).Add(questId);
    }

    public void RegisterOnKillInZone(string zone, int questId)
    {
        GetOrCreate(questOnKillInZone, zone).Add(questId);
    }

    public void RegisterOnLeaveZone(ZoneName zoneName, int questId)
    {
        GetOrCreate(questOnLeaveZone, zoneName).Add(questId);
    }

    public void RegisterOnKillRanked(AbyssRankEnum playerRank, int questId)
    {
        foreach (AbyssRankEnum rank in AbyssRankEnumExtensions.Values())
        {
            if (rank.GetId() >= playerRank.GetId())
            {
                GetOrCreate(questOnKillRanked, rank).Add(questId);
            }
        }
    }

    public void RegisterOnKillInWorld(int worldId, int questId)
    {
        GetOrCreate(questOnKillInWorld, worldId).Add(questId);
    }

    public void RegisterOnPassFlyingRings(string flyingRing, int questId)
    {
        GetOrCreate(questOnPassFlyingRings, flyingRing).Add(questId);
    }

    public void RegisterOnQuestTimerEnd(int questId)
    {
        if (!questOnTimerEnd.Contains(questId))
            questOnTimerEnd.Add(questId);
    }

    public void RegisterOnInvisibleTimerEnd(int questId)
    {
        if (!onInvisibleTimerEnd.Contains(questId))
            onInvisibleTimerEnd.Add(questId);
    }

    public void RegisterQuestSkill(int skillId, int questId)
    {
        GetOrCreate(questOnUseSkill, skillId).Add(questId);
    }

    public void RegisterOnFailCraft(int itemId, int questId)
    {
        questOnFailCraft.TryAdd(itemId, questId);
    }

    public void RegisterOnEquipItem(int itemId, int questId)
    {
        GetOrCreate(questOnEquipItem, itemId).Add(questId);
    }

    public bool RegisterCanAct(int questId, int npcId)
    {
        NpcTemplate template = DataManager.NPC_DATA.GetNpcTemplate(npcId);
        if (template == null)
        {
            log.LogWarning("[QuestEngine] No such NPC template for " + npcId + " in Q" + questId);
            return false;
        }
        if ("quest_use_item".Equals(template.GetAiName()))
        {
            GetOrCreate(questCanAct, npcId).Add(questId);
            return true;
        }
        return false;
    }

    public void RegisterOnDredgionReward(int questId)
    {
        if (!questOnDredgionReward.Contains(questId))
            questOnDredgionReward.Add(questId);
    }

    public void RegisterOnBonusApply(int questId, BonusType bonusType)
    {
        GetOrCreate(questOnBonusApply, bonusType).Add(questId);
    }

    public void RegisterAddOnReachTargetEvent(int questId)
    {
        if (!reachTarget.Contains(questId))
            reachTarget.Add(questId);
    }

    public void RegisterAddOnLostTargetEvent(int questId)
    {
        if (!lostTarget.Contains(questId))
            lostTarget.Add(questId);
    }

    public void RegisterOnEnterWindStream(int questId)
    {
        if (!questOnEnterWindStream.Contains(questId))
            questOnEnterWindStream.Add(questId);
    }

    public void RegisterOnRide(int questId)
    {
        if (!questRideAction.Contains(questId))
            questRideAction.Add(questId);
    }

    public QuestNpc GetQuestNpc(int npcId)
    {
        if (questNpcs.TryGetValue(npcId, out QuestNpc questNpc))
        {
            return questNpc;
        }
        return new QuestNpc(npcId);
    }

    private AbstractQuestHandler GetQuestHandlerByQuestId(int questId)
    {
        return questHandlers.GetValueOrDefault(questId);
    }

    public int GetQuestHandlerCount()
    {
        return questHandlers.Count;
    }

    public bool IsHaveHandler(int questId)
    {
        return questHandlers.ContainsKey(questId);
    }

    public void AddQuestHandler(AbstractQuestHandler questHandler)
    {
        int questId = questHandler.GetQuestId();
        if (!questHandlers.TryAdd(questId, questHandler))
            log.LogWarning("Duplicate handler for quest: " + questId);
        else
            questHandler.Register();
    }

    /// <summary>Add handler side drop (if not already in xml)</summary>
    public void AddHandlerSideQuestDrop(int questId, int npcId, int itemId, int amount, int chance)
    {
        HandlerSideDrop hsd = new(questId, npcId, itemId, amount, chance);
        QuestService.AddQuestDrop(hsd.GetNpcId(), hsd);
    }

    public void AddHandlerSideQuestDrop(int questId, int npcId, int itemId, int amount, int chance, int step)
    {
        HandlerSideDrop hsd = new(questId, npcId, itemId, amount, chance, step);
        QuestService.AddQuestDrop(hsd.GetNpcId(), hsd);
    }

    private void AddMessageSendingTask()
    {
        messageTask = CronService.GetInstance().Schedule(() =>
        {
            World.GetInstance().ForEachPlayer(player =>
            {
                bool daily = false, weekly = false;
                foreach (QuestState qs in player.GetQuestStateList().GetCompletedQuests())
                {
                    if (qs.IsStartable())
                    {
                        QuestTemplate template = DataManager.QUEST_DATA.GetQuestById(qs.GetQuestId());
                        if (!daily && template.IsDaily())
                            daily = true;
                        else if (!weekly && template.IsWeekly())
                            weekly = true;
                        if (daily && weekly)
                            break;
                    }
                }
                if (daily)
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_QUEST_LIMIT_RESET_DAILY());
                if (weekly)
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_QUEST_LIMIT_RESET_WEEK());
                if (daily || weekly)
                    player.GetController().UpdateNearbyQuests();
                player.GetNpcFactions().SendDailyQuest();
            });
        }, "0 0 9 ? * *");
    }

    /// <summary>computeIfAbsent(key, k -&gt; new ArrayList&lt;&gt;()) idiom.</summary>
    private static List<int> GetOrCreate<TKey>(Dictionary<TKey, List<int>> map, TKey key)
    {
        if (!map.TryGetValue(key, out List<int> list))
        {
            list = new List<int>();
            map[key] = list;
        }
        return list;
    }

    public static QuestEngine GetInstance()
    {
        return SingletonHolder.instance;
    }

    private static class SingletonHolder
    {
        internal static readonly QuestEngine instance = new();
    }
}
