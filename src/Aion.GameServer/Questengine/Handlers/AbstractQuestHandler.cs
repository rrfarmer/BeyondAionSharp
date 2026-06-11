using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Templates.Quest;
using Aion.GameServer.Model.Templates.Rewards;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Questengine.Model;
using Aion.GameServer.Questengine.Task;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Spawnengine;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Questengine.Handlers;

/// <summary>
/// Java parity: questEngine/handlers/AbstractQuestHandler (MrPoke, vlog, Majka). Quest-handler base: register + onX event
/// dispatch + send*/quest-state helpers. RED-by-design branch: many refs (QuestEngine/QuestService/ItemService/SM_* packets/
/// SpawnEngine/GeoService/DialogPage/EmotionId/CreatureType/TaskId/QuestTasks) are red-tolerated by the name guardrail.
/// Idioms: static import DialogAction.*→DialogAction.X; AIEventType→AiEventType; anonymous Runnable→ThreadPoolManager lambda;
/// Math.toRadians→deg*Math.PI/180; Stream.of(values()).allMatch→Enum.GetValues().All.
/// </summary>
public abstract class AbstractQuestHandler
{
    protected static readonly QuestEngine qe = QuestEngine.GetInstance();
    protected readonly int questId;
    protected List<QuestItems> workItems;
    protected HashSet<int> actionItems;

    /// <summary>Create a new AbstractQuestHandler object.</summary>
    protected AbstractQuestHandler(int questId)
    {
        this.questId = questId;
        QuestTemplate template = DataManager.QUEST_DATA.GetQuestById(questId);
        if (template != null) // Some artificial quests have dummy questIds
        {
            LoadWorkItems(template);
            LoadActionItems(template);
        }
    }

    private void LoadWorkItems(QuestTemplate template)
    {
        if (template.GetQuestWorkItems() != null)
            workItems = template.GetQuestWorkItems().GetQuestWorkItem();
    }

    private void LoadActionItems(QuestTemplate template)
    {
        foreach (QuestDrop drop in template.GetQuestDrop())
        {
            if (drop.GetNpcId() / 100000 != 7)
                continue;
            if (actionItems == null)
                actionItems = new HashSet<int>();
            actionItems.Add(drop.GetNpcId());
        }
    }

    public ISet<int> GetActionItems()
    {
        if (actionItems == null)
            return new HashSet<int>();
        return actionItems;
    }

    public int GetQuestId()
    {
        return questId;
    }

    public abstract void Register();

    public virtual bool OnDialogEvent(QuestEnv env)
    {
        switch (env.GetDialogActionId())
        {
            case DialogAction.ASK_QUEST_ACCEPT: // show quest accept dialog (coming from pre-conversation)
                SendDialogPacket(env, DialogPage.ASK_QUEST_ACCEPT_WINDOW.Id(), questId);
                return true;
            case DialogAction.QUEST_ACCEPT_1:
                return env.GetVisibleObject() is Npc ? SendQuestDialog(env, 1003) : CloseDialogWindow(env);
            case DialogAction.QUEST_REFUSE:
            case DialogAction.QUEST_REFUSE_SIMPLE:
            case DialogAction.QUEST_REFUSE_1:
                return env.GetVisibleObject() is Npc ? SendQuestDialog(env, 1004) : CloseDialogWindow(env);
            case DialogAction.QUEST_REFUSE_2:
                return env.GetVisibleObject() is Npc ? SendQuestDialog(env, 1005) : CloseDialogWindow(env);
            case DialogAction.QUEST_REFUSE_3:
                return env.GetVisibleObject() is Npc ? SendQuestDialog(env, 1006) : CloseDialogWindow(env);
            case DialogAction.QUEST_REFUSE_4:
                return env.GetVisibleObject() is Npc ? SendQuestDialog(env, 1007) : CloseDialogWindow(env);
            case DialogAction.FINISH_DIALOG: // clicking X or ^ in quest accept window
                return true;
        }
        return false;
    }

    public virtual bool OnEnterWorldEvent(QuestEnv env)
    {
        return false;
    }

    public virtual bool OnEnterZoneEvent(QuestEnv env, ZoneName zoneName)
    {
        return false;
    }

    public virtual bool OnLeaveZoneEvent(QuestEnv env, ZoneName zoneName)
    {
        return false;
    }

    public virtual HandlerResult OnItemUseEvent(QuestEnv env, Item item)
    {
        return HandlerResult.UNKNOWN;
    }

    public virtual bool OnHouseItemUseEvent(QuestEnv env)
    {
        return false;
    }

    public virtual bool OnGetItemEvent(QuestEnv env)
    {
        return false;
    }

    public virtual bool OnUseSkillEvent(QuestEnv env, int skillId)
    {
        return false;
    }

    public virtual bool OnKillEvent(QuestEnv env)
    {
        return false;
    }

    public virtual bool OnAttackEvent(QuestEnv env)
    {
        return false;
    }

    public virtual void OnLevelChangedEvent(Player player)
    {
    }

    public virtual void OnQuestCompletedEvent(QuestEnv env)
    {
    }

    public virtual bool OnDieEvent(QuestEnv env)
    {
        return false;
    }

    public virtual bool OnLogOutEvent(QuestEnv env)
    {
        return false;
    }

    public virtual bool OnNpcReachTargetEvent(QuestEnv env)
    {
        return false;
    }

    public virtual bool OnNpcLostTargetEvent(QuestEnv env)
    {
        return false;
    }

    public virtual void OnMovieEndEvent(QuestEnv env, int movieId)
    {
    }

    public virtual bool OnQuestTimerEndEvent(QuestEnv env)
    {
        return false;
    }

    public virtual bool OnInvisibleTimerEndEvent(QuestEnv env)
    {
        return false;
    }

    public virtual bool OnPassFlyingRingEvent(QuestEnv env, string flyingRing)
    {
        return false;
    }

    public virtual bool OnKillRankedEvent(QuestEnv env)
    {
        return false;
    }

    public virtual bool OnKillInWorldEvent(QuestEnv env)
    {
        return false;
    }

    public virtual bool OnKillInZoneEvent(QuestEnv env)
    {
        return false;
    }

    public virtual bool OnFailCraftEvent(QuestEnv env, int itemId)
    {
        return false;
    }

    public virtual bool OnEquipItemEvent(QuestEnv env, int itemId)
    {
        return false;
    }

    public virtual bool OnCanAct(QuestEnv env, QuestActionType questEventType, params object[] objects)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(env.GetQuestId());
        if (qs == null || qs.GetStatus() != QuestStatus.START)
            return false;
        if (questEventType == QuestActionType.ACTION_ITEM_USE && actionItems != null)
        {
            QuestTemplate template = DataManager.QUEST_DATA.GetQuestById(env.GetQuestId());
            int droppedItem = 0;
            int dropCount = 0;
            foreach (QuestDrop drop in template.GetQuestDrop())
            {
                if (drop.GetNpcId() == env.GetTargetId())
                {
                    droppedItem = drop.GetItemId();
                    break;
                }
            }
            CollectItems collectItems = template.GetCollectItems();
            if (collectItems != null && droppedItem != 0)
            {
                foreach (CollectItem item in collectItems.GetCollectItem())
                {
                    if (item.GetItemId() == droppedItem)
                    {
                        dropCount = item.GetCount();
                        break;
                    }
                }
                if (dropCount != 0)
                {
                    long currentCount = player.GetInventory().GetItemCountByItemId(droppedItem);
                    if (currentCount >= dropCount)
                        return false;
                }
            }
        }
        return true;
    }

    public virtual bool OnAddAggroListEvent(QuestEnv env)
    {
        return false;
    }

    public virtual bool OnAtDistanceEvent(QuestEnv env)
    {
        return false;
    }

    public virtual bool OnEnterWindStreamEvent(QuestEnv env, int worldId)
    {
        return false;
    }

    public virtual bool RideAction(QuestEnv env, int rideItemId)
    {
        return false;
    }

    public virtual bool OnDredgionRewardEvent(QuestEnv env)
    {
        return false;
    }

    public virtual HandlerResult OnBonusApplyEvent(QuestEnv env, BonusType bonusType, List<QuestItems> rewardItems)
    {
        return HandlerResult.UNKNOWN;
    }

    public virtual bool OnProtectEndEvent(QuestEnv env)
    {
        return false;
    }

    public virtual bool OnProtectFailEvent(QuestEnv env)
    {
        return false;
    }

    /// <summary>Update the status of the quest in player's journal.</summary>
    public void UpdateQuestStatus(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        PacketSendUtility.SendPacket(player, new SM_QUEST_ACTION(SM_QUEST_ACTION.ActionType.UPDATE, qs));
        if (qs.GetStatus() == QuestStatus.COMPLETE || qs.GetStatus() == QuestStatus.REWARD)
            player.GetController().UpdateNearbyQuests();
    }

    public bool ChangeQuestStep(QuestEnv env, int oldStep, int newStep)
    {
        return ChangeQuestStep(env, oldStep, newStep, false, oldStep > 0x3F || newStep > 0x3F ? -1 : 0);
    }

    public bool ChangeQuestStep(QuestEnv env, int step, int nextStep, bool reward)
    {
        return ChangeQuestStep(env, step, nextStep, reward, 0);
    }

    /// <summary>Change the quest step to the next step or set quest status to reward.</summary>
    public bool ChangeQuestStep(QuestEnv env, int step, int nextStep, bool reward, int varNum)
    {
        QuestState qs = env.GetPlayer().GetQuestStateList().GetQuestState(questId);
        if (qs != null && (varNum == -1 ? qs.GetQuestVars().GetQuestVars() == step : qs.GetQuestVarById(varNum) == step))
        {
            if (nextStep != step) // quest can be rolled back if nextStep < step
            {
                if (step > nextStep && qs.GetStatus() == QuestStatus.START)
                    PacketSendUtility.SendPacket(env.GetPlayer(),
                        SM_SYSTEM_MESSAGE.STR_QUEST_SYSTEMMSG_GIVEUP(DataManager.QUEST_DATA.GetQuestById(questId).GetL10n()));
                if (varNum == -1)
                    qs.SetQuestVar(nextStep);
                else
                    qs.SetQuestVarById(varNum, nextStep);
            }
            if (reward || nextStep != step)
            {
                if (reward)
                    qs.SetStatus(QuestStatus.REWARD);
                UpdateQuestStatus(env);
            }
            return true;
        }
        return false;
    }

    /// <summary>Send dialog to the player.</summary>
    public bool SendQuestDialog(QuestEnv env, int dialogPageId)
    {
        if (dialogPageId == DialogPage.SELECT_QUEST_REWARD_WINDOW1.Id() || dialogPageId == DialogPage.SELECT_QUEST_REWARD_WINDOW2.Id()
            || dialogPageId == DialogPage.SELECT_QUEST_REWARD_WINDOW3.Id() || dialogPageId == DialogPage.SELECT_QUEST_REWARD_WINDOW4.Id()
            || dialogPageId == DialogPage.SELECT_QUEST_REWARD_WINDOW5.Id() || dialogPageId == DialogPage.SELECT_QUEST_REWARD_WINDOW6.Id()
            || dialogPageId == DialogPage.SELECT_QUEST_REWARD_WINDOW7.Id() || dialogPageId == DialogPage.SELECT_QUEST_REWARD_WINDOW8.Id()
            || dialogPageId == DialogPage.SELECT_QUEST_REWARD_WINDOW9.Id() || dialogPageId == DialogPage.SELECT_QUEST_REWARD_WINDOW10.Id())
        {
            QuestState qs = env.GetPlayer().GetQuestStateList().GetQuestState(questId);
            if (qs == null || qs.GetStatus() != QuestStatus.REWARD) // reward packet exploitation fix
                return false;
        }
        // Not using handler questId, because some quests may handle events when quests are finished (questId must be zero, e.g. Kromede entry)
        SendDialogPacket(env, dialogPageId, env.GetQuestId());
        return true;
    }

    private void SendDialogPacket(QuestEnv env, int dialogPageId, int questId)
    {
        int objId = 0;
        if (env.GetVisibleObject() != null)
        {
            objId = env.GetVisibleObject().ObjectId;
        }
        PacketSendUtility.SendPacket(env.GetPlayer(), new SM_DIALOG_WINDOW(objId, dialogPageId, questId));
    }

    public bool SendQuestSelectionDialog(QuestEnv env)
    {
        SendDialogPacket(env, 10, 0);
        return true;
    }

    public bool CloseDialogWindow(QuestEnv env)
    {
        SendDialogPacket(env, 0, 0);
        return true;
    }

    public bool SendQuestStartDialog(QuestEnv env)
    {
        return SendQuestStartDialog(env, 0, 0);
    }

    public bool SendQuestStartDialog(QuestEnv env, QuestItems workItem)
    {
        return workItem == null ? SendQuestStartDialog(env, 0, 0) : SendQuestStartDialog(env, workItem.GetItemId(), workItem.GetCount());
    }

    /// <summary>Send default start quest dialog and start it (give the item on start).</summary>
    public bool SendQuestStartDialog(QuestEnv env, int itemId, long itemCount)
    {
        switch (env.GetDialogActionId())
        {
            case DialogAction.ASK_QUEST_ACCEPT:
                return SendQuestDialog(env, 4);
            case DialogAction.QUEST_ACCEPT:
            case DialogAction.QUEST_ACCEPT_1:
            case DialogAction.QUEST_ACCEPT_SIMPLE:
                if (QuestService.StartQuest(env))
                {
                    if (itemId != 0 && itemCount != 0)
                        GiveQuestItem(env, itemId, itemCount);
                    if (env.GetDialogActionId() != DialogAction.QUEST_ACCEPT_SIMPLE && env.GetVisibleObject() is Npc)
                        return SendQuestDialog(env, 1003);
                    else
                        return CloseDialogWindow(env);
                }
                break;
            case DialogAction.QUEST_REFUSE_1:
            case DialogAction.QUEST_REFUSE_2:
                return SendQuestDialog(env, 1004);
            case DialogAction.QUEST_REFUSE_SIMPLE:
                return CloseDialogWindow(env);
            case DialogAction.FINISH_DIALOG:
                return SendQuestSelectionDialog(env);
        }
        return false;
    }

    /// <summary>Remove all quest items and send and finish the quest.</summary>
    public bool SendQuestEndDialog(QuestEnv env, int[] questItemsToRemove)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        QuestStatus? status = qs == null ? null : qs.GetStatus();
        foreach (int itemId in questItemsToRemove)
            RemoveQuestItem(env, itemId, player.GetInventory().GetItemCountByItemId(itemId), status.Value);
        return SendQuestEndDialog(env);
    }

    /// <summary>Sends reward selection dialog of the quest or finishes it (if selection dialog was active).</summary>
    public bool SendQuestEndDialog(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null || qs.GetStatus() != QuestStatus.REWARD)
            return false; // reward packet exploitation fix (or buggy quest handler)

        int dialogActionId = env.GetDialogActionId();
        if (dialogActionId >= DialogAction.SELECTED_QUEST_REWARD1 && dialogActionId <= DialogAction.SELECTED_QUEST_NOREWARD)
        {
            if (QuestService.FinishQuest(env))
            {
                Npc npc = (Npc)env.GetVisibleObject();
                QuestNpc questNpc = QuestEngine.GetInstance().GetQuestNpc(npc.GetNpcId());
                bool npcHasActiveQuest = false;
                foreach (int questId in questNpc.GetOnTalkEvent()) // all quest IDs that have registered talk events for this npc
                {
                    QuestState qs2 = player.GetQuestStateList().GetQuestState(questId);
                    if (qs2 != null && qs2.GetStatus() == QuestStatus.REWARD)
                    {
                        env.SetQuestId(questId);
                        env.SetDialogActionId(DialogAction.USE_OBJECT); // show default dialog (reward selection for next quest)
                        return QuestEngine.GetInstance().OnDialog(new QuestEnv(npc, player, questId, DialogAction.USE_OBJECT));
                    }
                    else if (!npcHasActiveQuest && qs2 != null && qs2.GetStatus() == QuestStatus.START)
                    {
                        bool isQuestStartNpc = questNpc.GetOnQuestStart().Contains(questId);
                        if (!isQuestStartNpc || DataManager.QUEST_DATA.GetQuestById(questId).IsMission() && qs2.GetQuestVars().GetQuestVars() == 0)
                            npcHasActiveQuest = true;
                    }
                }
                bool npcHasNewQuest = false;
                foreach (int questId in questNpc.GetOnQuestStart()) // all quest IDs that are registered to be started at this npc
                {
                    if (QuestService.CheckStartConditions(player, questId, false))
                    {
                        npcHasNewQuest = true;
                        QuestTemplate template = DataManager.QUEST_DATA.GetQuestById(questId);
                        foreach (XMLStartCondition startCondition in template.GetXMLStartConditions())
                        {
                            List<FinishedQuestCond> finishedQuests = startCondition.GetFinishedPreconditions();
                            if (finishedQuests != null)
                            {
                                foreach (FinishedQuestCond fcondition in finishedQuests)
                                {
                                    if (fcondition.GetQuestId() == env.GetQuestId() && IsAcceptableQuest(template))
                                    {
                                        env.SetQuestId(questId);
                                        env.SetDialogActionId(DialogAction.QUEST_SELECT);
                                        env.SetDialogContinuationFromPreQuest(true);
                                        return QuestEngine.GetInstance().OnDialog(env); // show start dialog of follow-up quest
                                    }
                                }
                            }
                        }
                    }
                }
                return npcHasActiveQuest || npcHasNewQuest ? SendQuestSelectionDialog(env) : CloseDialogWindow(env);
            }
        }
        else
        {
            switch (dialogActionId)
            {
                case DialogAction.SET_SUCCEED: // report to pre-end npc (another npc actually rewards, so close this window)
                    return CloseDialogWindow(env);
                case DialogAction.USE_OBJECT: // start talking to npc
                case DialogAction.QUEST_SELECT: // start talking to npc
                case DialogAction.SELECT_QUEST_REWARD: // report to end npc
                case DialogAction.CHECK_USER_HAS_QUEST_ITEM: // report to end npc with collect item checks
                case DialogAction.CHECK_USER_HAS_QUEST_ITEM_SIMPLE: // report to end npc with collect item checks
                    QuestService.ValidateAndFixRewardGroup(qs, questId); // fixes the reward group if necessary
                    // show reward selection page
                    return SendQuestDialog(env, DialogPage.GetRewardPageByIndex(qs.GetRewardGroup().Value).Id());
            }
        }
        return false;
    }

    private bool IsAcceptableQuest(QuestTemplate quest)
    {
        if (quest.GetMinlevelPermitted() == 99)
            return false;
        if (quest.GetRewards().Count == 0 && quest.GetExtendedRewards() == null && quest.GetBonus() == null && quest.GetQuestDrop().Count == 0
            && Enum.GetValues<PlayerClass>().All(c => quest.GetSelectableRewardByClass(c).Count == 0))
        {
            return false;
        }
        return true;
    }

    public bool DefaultCloseDialog(QuestEnv env, int step, int nextStep)
    {
        return DefaultCloseDialog(env, step, nextStep, false, false, 0, 0, 0, 0);
    }

    public bool DefaultCloseDialog(QuestEnv env, int step, int nextStep, int giveItemId, long giveItemCount)
    {
        return DefaultCloseDialog(env, step, nextStep, false, false, giveItemId, giveItemCount, 0, 0);
    }

    public bool DefaultCloseDialog(QuestEnv env, int step, int nextStep, bool reward, bool sameNpc)
    {
        return DefaultCloseDialog(env, step, nextStep, reward, sameNpc, 0, 0, 0, 0);
    }

    public bool DefaultCloseDialog(QuestEnv env, int step, int nextStep, bool reward, bool sameNpc, QuestItems questItemToAdd)
    {
        return DefaultCloseDialog(env, step, nextStep, reward, sameNpc, questItemToAdd.GetItemId(), questItemToAdd.GetCount(), 0, 0);
    }

    public bool DefaultCloseDialog(QuestEnv env, int step, int nextStep, int giveItemId, long giveItemCount, int removeItemId, long removeItemCount)
    {
        return DefaultCloseDialog(env, step, nextStep, false, false, giveItemId, giveItemCount, removeItemId, removeItemCount);
    }

    /// <summary>Handle on close dialog event, changing the quest status and giving/removing quest items.</summary>
    public bool DefaultCloseDialog(QuestEnv env, int step, int nextStep, bool reward, bool sameNpc, int giveItemId, long giveItemCount,
        int removeItemId, long removeItemCount)
    {
        QuestState qs = env.GetPlayer().GetQuestStateList().GetQuestState(questId);
        if (qs.GetQuestVarById(0) == step)
        {
            if (giveItemId != 0 && giveItemCount != 0)
            {
                if (!GiveQuestItem(env, giveItemId, giveItemCount))
                {
                    return false;
                }
            }
            RemoveQuestItem(env, removeItemId, removeItemCount, qs.GetStatus());
            ChangeQuestStep(env, step, nextStep, reward);
            if (sameNpc)
            {
                return SendQuestEndDialog(env);
            }
            if (env.GetVisibleObject() is Npc npc)
                npc.GetAi().OnCreatureEvent(AiEventType.DIALOG_FINISH, env.GetPlayer());
            return CloseDialogWindow(env);
        }
        return false;
    }

    public bool CheckQuestItems(QuestEnv env, int step, int nextStep, bool reward, int checkOkId, int checkFailId)
    {
        return CheckQuestItems(env, step, nextStep, reward, checkOkId, checkFailId, 0, 0);
    }

    /// <summary>Check if the player has quest item, listed in the quest_data.xml in his inventory.</summary>
    public bool CheckQuestItems(QuestEnv env, int step, int nextStep, bool reward, int checkOkId, int checkFailId, int giveItemId, int giveItemCount)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs.GetQuestVarById(0) == step)
        {
            if (QuestService.CollectItemCheck(env, true))
            {
                if (giveItemId != 0 && giveItemCount != 0)
                {
                    if (!GiveQuestItem(env, giveItemId, giveItemCount))
                    {
                        return false;
                    }
                }
                ChangeQuestStep(env, step, nextStep, reward);
                return SendQuestDialog(env, checkOkId);
            }
            else
            {
                return SendQuestDialog(env, checkFailId);
            }
        }
        return false;
    }

    /// <summary>Check if the player has quest item (simple version).</summary>
    public bool CheckQuestItemsSimple(QuestEnv env, int step, int nextStep, bool reward, int checkOkId, int giveItemId, int giveItemCount)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs.GetQuestVarById(0) == step)
        {
            if (QuestService.CollectItemCheck(env, true))
            {
                if (giveItemId != 0 && giveItemCount != 0)
                {
                    if (!GiveQuestItem(env, giveItemId, giveItemCount))
                    {
                        return false;
                    }
                }
                ChangeQuestStep(env, step, nextStep, reward);
                return SendQuestDialog(env, checkOkId);
            }
            else
                return CloseDialogWindow(env);
        }
        return false;
    }

    /// <summary>For checking items not listed in collect_items in the quest_data.xml.</summary>
    public bool CheckItemExistence(QuestEnv env, int step, int nextStep, bool reward, int itemId, int itemCount, bool remove, int checkOkId,
        int checkFailId, int giveItemId, int giveItemCount)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs.GetQuestVarById(0) == step)
        {
            if (CheckItemExistence(env, itemId, itemCount, remove))
            {
                if (giveItemId != 0 && giveItemCount != 0)
                {
                    if (!GiveQuestItem(env, giveItemId, giveItemCount))
                    {
                        return false;
                    }
                }
                ChangeQuestStep(env, step, nextStep, reward);
                return SendQuestDialog(env, checkOkId);
            }
            else
            {
                return SendQuestDialog(env, checkFailId);
            }
        }
        return false;
    }

    /// <summary>Check if item exists in the player's inventory and probably remove it.</summary>
    public bool CheckItemExistence(QuestEnv env, int itemId, int itemCount, bool remove)
    {
        Player player = env.GetPlayer();
        if (player.GetInventory().GetItemCountByItemId(itemId) >= itemCount)
        {
            if (remove)
            {
                if (!RemoveQuestItem(env, itemId, itemCount))
                {
                    return false;
                }
            }
            return true;
        }
        else
        {
            return false;
        }
    }

    public void SendEmotion(QuestEnv env, Creature emoteCreature, EmotionId emotion, bool broadcast)
    {
        Player player = env.GetPlayer();
        int targetId = player.Equals(emoteCreature) ? env.GetVisibleObject().ObjectId : player.ObjectId;
        PacketSendUtility.BroadcastPacket(player, new SM_EMOTION(emoteCreature, EmotionType.EMOTE, emotion.Id(), targetId), broadcast);
    }

    /// <summary>Give the quest item to player's inventory.</summary>
    public bool GiveQuestItem(QuestEnv env, int itemId, long itemCount)
    {
        return GiveQuestItem(env, itemId, itemCount, ItemAddType.QUEST_WORK_ITEM, ItemUpdateType.INC_ITEM_COLLECT);
    }

    public bool GiveQuestItem(QuestEnv env, int itemId, long itemCount, ItemAddType addType)
    {
        return GiveQuestItem(env, itemId, itemCount, addType, ItemUpdateType.INC_ITEM_COLLECT);
    }

    public bool GiveQuestItem(QuestEnv env, int itemId, long itemCount, ItemAddType addType, ItemUpdateType updateType)
    {
        Player player = env.GetPlayer();
        ItemTemplate item = DataManager.ITEM_DATA.GetItemTemplate(itemId);
        if (itemId != 0 && itemCount != 0)
        {
            long existentItemCount = player.GetInventory().GetItemCountByItemId(itemId);
            if (existentItemCount < itemCount)
            {
                long itemsToGive = itemCount - existentItemCount; // some quest work items come from multiple quests, don't add again
                ItemService.AddItem(player, itemId, itemsToGive, true, new ItemService.ItemUpdatePredicate(addType, updateType));
                return true;
            }
            else
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_CAN_NOT_GET_LORE_ITEM(item.GetL10n()));
                return true;
            }
        }
        return false;
    }

    /// <summary>Remove the specified count of this quest item from player's inventory.</summary>
    public bool RemoveQuestItem(QuestEnv env, int itemId, long itemCount)
    {
        Player player = env.GetPlayer();
        if (itemId != 0 && itemCount > 0)
        {
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            return player.GetInventory().DecreaseByItemId(itemId, itemCount, qs == null ? QuestStatus.START : qs.GetStatus());
        }
        return false;
    }

    public bool RemoveQuestItem(QuestEnv env, int itemId, long itemCount, QuestStatus questStatus)
    {
        Player player = env.GetPlayer();
        if (itemId != 0 && itemCount != 0)
        {
            return player.GetInventory().DecreaseByItemId(itemId, itemCount, questStatus);
        }
        return false;
    }

    public bool PlayQuestMovie(QuestEnv env, int movieId)
    {
        return PlayQuestMovie(env, movieId, false);
    }

    public bool PlayQuestMovie(QuestEnv env, int cutsceneId, bool isCutsceneMovie)
    {
        int targetObjectId = env.GetVisibleObject() == null ? 0 : env.GetVisibleObject().ObjectId;
        PacketSendUtility.SendPacket(env.GetPlayer(), new SM_PLAY_MOVIE(isCutsceneMovie, targetObjectId, env.GetQuestId(), cutsceneId, true));
        return false;
    }

    /// <summary>For single kill.</summary>
    public bool DefaultOnKillEvent(QuestEnv env, int npcId, int startVar, int endVar)
    {
        int[] mobids = { npcId };
        return DefaultOnKillEvent(env, mobids, startVar, endVar);
    }

    /// <summary>For multiple kills.</summary>
    public bool DefaultOnKillEvent(QuestEnv env, int[] npcIds, int startVar, int endVar)
    {
        return DefaultOnKillEvent(env, npcIds, startVar, endVar, 0);
    }

    /// <summary>For single kill on another QuestVar.</summary>
    public bool DefaultOnKillEvent(QuestEnv env, int npcId, int startVar, int endVar, int varNum)
    {
        int[] mobids = { npcId };
        return DefaultOnKillEvent(env, mobids, startVar, endVar, varNum);
    }

    /// <summary>Handle onKill event.</summary>
    public bool DefaultOnKillEvent(QuestEnv env, int[] npcIds, int startVar, int endVar, int varNum)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(varNum);
            int targetId = env.GetTargetId();
            foreach (int id in npcIds)
            {
                if (targetId == id)
                {
                    if (var >= startVar && var < endVar)
                    {
                        qs.SetQuestVarById(varNum, var + 1);
                        UpdateQuestStatus(env);
                        return true;
                    }
                }
            }
        }
        return false;
    }

    /// <summary>For single kill and reward status after it.</summary>
    public bool DefaultOnKillEvent(QuestEnv env, int npcId, int startVar, bool reward)
    {
        int[] mobids = { npcId };
        return DefaultOnKillEvent(env, mobids, startVar, reward, 0);
    }

    /// <summary>For single kill on another QuestVar and reward status after it.</summary>
    public bool DefaultOnKillEvent(QuestEnv env, int npcId, int startVar, bool reward, int varNum)
    {
        int[] mobids = { npcId };
        return DefaultOnKillEvent(env, mobids, startVar, reward, varNum);
    }

    /// <summary>For multiple kills and reward status after it.</summary>
    public bool DefaultOnKillEvent(QuestEnv env, int[] npcIds, int startVar, bool reward)
    {
        return DefaultOnKillEvent(env, npcIds, startVar, reward, 0);
    }

    /// <summary>Handle onKill event with reward status.</summary>
    public bool DefaultOnKillEvent(QuestEnv env, int[] npcIds, int startVar, bool reward, int varNum)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(varNum);
            int targetId = env.GetTargetId();
            foreach (int id in npcIds)
            {
                if (targetId == id)
                {
                    if (var == startVar)
                    {
                        if (reward)
                        {
                            qs.SetStatus(QuestStatus.REWARD);
                        }
                        else
                        {
                            qs.SetQuestVarById(varNum, var + 1);
                        }
                        UpdateQuestStatus(env);
                        return true;
                    }
                }
            }
        }
        return false;
    }

    public bool DefaultOnKillRankedEvent(QuestEnv env, int startVar, int endVar, bool reward)
    {
        return DefaultOnKillRankedEvent(env, startVar, endVar, reward, false);
    }

    public bool DefaultOnKillRankedEvent(QuestEnv env, int startVar, int endVar, bool reward, bool isDataDriven)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            if (isDataDriven)
            {
                int varKill = qs.GetQuestVarById(1);
                if (varKill >= startVar && varKill < (endVar - 1))
                {
                    ChangeQuestStep(env, varKill, varKill + 1, false, 1);
                    return true;
                }
                else if (varKill == (endVar - 1))
                {
                    if (reward)
                        qs.SetStatus(QuestStatus.REWARD);
                    qs.SetQuestVar(var + 1);
                }
            }
            else
            {
                if (var >= startVar && var < (endVar - 1))
                {
                    ChangeQuestStep(env, var, var + 1, false);
                    return true;
                }
                else if (var == (endVar - 1))
                {
                    if (reward)
                        qs.SetStatus(QuestStatus.REWARD);
                    else
                        qs.SetQuestVarById(0, var + 1);
                }
            }
            UpdateQuestStatus(env);
            return true;
        }
        return false;
    }

    public bool DefaultOnKillInZoneEvent(QuestEnv env, int startVar, int endVar, bool reward)
    {
        return DefaultOnKillRankedEvent(env, startVar, endVar, reward, false);
    }

    public bool DefaultOnKillInZoneEvent(QuestEnv env, int startVar, int endVar, bool reward, bool isDataDriven)
    {
        return DefaultOnKillRankedEvent(env, startVar, endVar, reward, isDataDriven);
    }

    public bool DefaultOnUseSkillEvent(QuestEnv env, int startVar, int endVar, int varNum)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(varNum);
            if (var >= startVar && var < endVar)
            {
                ChangeQuestStep(env, var, var + 1, false, varNum);
                return true;
            }
        }
        return false;
    }

    /// <summary>NPC starts following the player to the target. Use onLostTarget and onReachTarget for further actions.</summary>
    public bool DefaultStartFollowEvent(QuestEnv env, Npc follower, int targetNpcId, int step, int nextStep)
    {
        Player player = env.GetPlayer();
        if (!(env.GetVisibleObject() is Npc))
        {
            return false;
        }
        follower.OverrideNpcType(CreatureType.PEACE);
        follower.GetAi().OnCreatureEvent(AiEventType.FOLLOW_ME, player);
        player.GetController().AddTask(TaskId.QUEST_FOLLOW, QuestTasks.NewFollowingToTargetCheckTask(env, follower, targetNpcId));
        return step == 0 && nextStep == 0 || DefaultCloseDialog(env, step, nextStep);
    }

    /// <summary>NPC starts following the player to the target location.</summary>
    public bool DefaultStartFollowEvent(QuestEnv env, Npc follower, float x, float y, float z, int step, int nextStep)
    {
        Player player = env.GetPlayer();
        if (!(env.GetVisibleObject() is Npc))
        {
            return false;
        }
        PacketSendUtility.SendPacket(player, new SM_NPC_INFO(follower, player));
        follower.GetAi().OnCreatureEvent(AiEventType.FOLLOW_ME, player);
        player.GetController().AddTask(TaskId.QUEST_FOLLOW, QuestTasks.NewFollowingToTargetCheckTask(env, follower, x, y, z));
        if (step == 0 && nextStep == 0)
        {
            return true;
        }
        else
        {
            return DefaultCloseDialog(env, step, nextStep);
        }
    }

    /// <summary>NPC stops following the player. Used in both onLostTargetEvent and onReachTargetEvent.</summary>
    public bool DefaultFollowEndEvent(QuestEnv env, int step, int nextStep, bool reward, int movie)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            if (qs.GetQuestVarById(0) == step)
            {
                ChangeQuestStep(env, step, nextStep, reward);
                if (movie != 0)
                    PlayQuestMovie(env, movie);
                return true;
            }
        }
        return false;
    }

    public bool DefaultFollowEndEvent(QuestEnv env, int step, int nextStep, bool reward)
    {
        return DefaultFollowEndEvent(env, step, nextStep, reward, 0);
    }

    /// <summary>Changing quest step on getting item.</summary>
    public bool DefaultOnGetItemEvent(QuestEnv env, int step, int nextStep, bool reward)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            if (qs.GetQuestVarById(0) == step)
            {
                ChangeQuestStep(env, step, nextStep, reward);
                return true;
            }
        }
        return false;
    }

    public bool UseQuestObject(QuestEnv env, int step, int nextStep, bool reward, bool die)
    {
        return UseQuestObject(env, step, nextStep, reward, 0, 0, 0, 0, 0, 0, die);
    }

    public bool UseQuestObject(QuestEnv env, int step, int nextStep, bool reward, int varNum, bool die)
    {
        return UseQuestObject(env, step, nextStep, reward, varNum, 0, 0, 0, 0, 0, die);
    }

    public bool UseQuestObject(QuestEnv env, int step, int nextStep, bool reward, int varNum)
    {
        return UseQuestObject(env, step, nextStep, reward, varNum, 0, 0, 0, 0, 0, false);
    }

    public bool UseQuestObject(QuestEnv env, int step, int nextStep, bool reward, int varNum, int addItemId, int addItemCount)
    {
        return UseQuestObject(env, step, nextStep, reward, varNum, addItemId, addItemCount, 0, 0, 0, false);
    }

    public bool UseQuestObject(QuestEnv env, int step, int nextStep, bool reward, int varNum, int addItemId, int addItemCount, int removeItemId,
        int removeItemCount)
    {
        return UseQuestObject(env, step, nextStep, reward, varNum, addItemId, addItemCount, removeItemId, removeItemCount, 0, false);
    }

    public bool UseQuestObject(QuestEnv env, int step, int nextStep, bool reward, int varNum, int movieId)
    {
        return UseQuestObject(env, step, nextStep, reward, varNum, 0, 0, 0, 0, movieId, false);
    }

    /// <summary>Handle use object event.</summary>
    public bool UseQuestObject(QuestEnv env, int step, int nextStep, bool reward, int varNum, int addItemId, int addItemCount, int removeItemId,
        int removeItemCount, int movieId, bool dieObject)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null)
        {
            return false;
        }
        if (qs.GetQuestVarById(varNum) == step)
        {
            if (addItemId != 0 && addItemCount != 0)
            {
                if (!GiveQuestItem(env, addItemId, addItemCount))
                {
                    return false;
                }
            }
            if (removeItemId != 0 && removeItemCount != 0)
            {
                RemoveQuestItem(env, removeItemId, removeItemCount);
            }
            if (movieId != 0)
            {
                PlayQuestMovie(env, movieId);
            }
            if (dieObject)
            {
                Npc npc = (Npc)player.GetTarget();
                if (!env.GetVisibleObject().Equals(npc))
                    return false;
                npc.GetController().Die(player);
            }
            ChangeQuestStep(env, step, nextStep, reward, varNum);
            return true;
        }
        return false;
    }

    public bool UseQuestItem(QuestEnv env, Item item, int step, int nextStep, bool reward)
    {
        return UseQuestItem(env, item, step, nextStep, reward, 0, 0, 0);
    }

    public bool UseQuestItem(QuestEnv env, Item item, int step, int nextStep, bool reward, int addItemId, int addItemCount)
    {
        return UseQuestItem(env, item, step, nextStep, reward, addItemId, addItemCount, 0);
    }

    public bool UseQuestItem(QuestEnv env, Item item, int step, int nextStep, bool reward, int movieId)
    {
        return UseQuestItem(env, item, step, nextStep, reward, 0, 0, movieId);
    }

    public bool UseQuestItem(QuestEnv env, Item item, int step, int nextStep, bool reward, int addItemId, int addItemCount, int movieId)
    {
        return UseQuestItem(env, item, step, nextStep, reward, addItemId, addItemCount, movieId, 0);
    }

    /// <summary>Handle use item event.</summary>
    public bool UseQuestItem(QuestEnv env, Item item, int step, int nextStep, bool reward, int addItemId, int addItemCount, int movieId, int varNum)
    {
        Player player = env.GetPlayer();
        if (player == null)
        {
            return false;
        }
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null)
        {
            return false;
        }
        int itemId = item.GetItemId();
        int objectId = item.ObjectId;

        if (qs.GetQuestVarById(varNum) == step)
        {
            PacketSendUtility.BroadcastPacket(player, new SM_ITEM_USAGE_ANIMATION(player.ObjectId, objectId, itemId, 3000, 0, 0), true);
            ThreadPoolManager.GetInstance().Schedule(ct =>
            {
                PacketSendUtility.BroadcastPacket(player, new SM_ITEM_USAGE_ANIMATION(player.ObjectId, objectId, itemId, 0, 1, 0), true);
                RemoveQuestItem(env, itemId, 1);

                if (addItemId != 0 && addItemCount != 0)
                {
                    if (!GiveQuestItem(env, addItemId, addItemCount))
                    {
                        return ValueTask.CompletedTask;
                    }
                }
                if (movieId != 0)
                {
                    PlayQuestMovie(env, movieId);
                }
                ChangeQuestStep(env, step, nextStep, reward, varNum);
                return ValueTask.CompletedTask;
            }, TimeSpan.FromMilliseconds(3000));
            return true;
        }
        return false;
    }

    /// <summary>Starts or locks quest on level up (usually from campaign quest handlers).</summary>
    public bool DefaultOnLevelChangedEvent(Player player, params int[] preQuests)
    {
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        // Only null or LOCKED quests can be started
        if (qs != null && qs.GetStatus() != QuestStatus.LOCKED)
            return false;

        QuestTemplate template = DataManager.QUEST_DATA.GetQuestById(questId);
        int minLvlDiff = template.IsMission() ? 2 : 0;
        // Check all player requirements (allowed diff to quest minLevel = 2)
        if (!QuestService.CheckStartConditions(player, questId, false, minLvlDiff, false, false, template.IsMission()))
            return false;

        bool missingRequirement = false;
        foreach (int id in preQuests)
        {
            QuestState qs2 = player.GetQuestStateList().GetQuestState(id);
            if (!missingRequirement && (qs2 == null || qs2.GetStatus() != QuestStatus.COMPLETE))
            {
                if (qs != null || !template.IsMission()) // fast return if already locked or no campaign quest
                    return false;
                missingRequirement = true;
            }
            if (missingRequirement && qs2 != null && qs2.GetStatus() == QuestStatus.COMPLETE)
            {
                QuestService.AddOrUpdateQuest(player, questId, QuestStatus.LOCKED);
                return false;
            }
        }
        if (missingRequirement)
            return false;

        // Check quests that have to be done before starting this one and other start conditions
        foreach (XMLStartCondition cond in template.GetXMLStartConditions())
        {
            if (!cond.Check(player, false))
            {
                if (qs == null && template.IsMission())
                    QuestService.AddOrUpdateQuest(player, questId, QuestStatus.LOCKED);
                return false;
            }
        }

        // Send locked quest if the player is <= 2 levels below quest min level
        if (minLvlDiff > 0 && player.GetLevel() < template.GetMinlevelPermitted())
        {
            if (qs == null && template.IsMission())
                QuestService.AddOrUpdateQuest(player, questId, QuestStatus.LOCKED);
            return false;
        }

        // All conditions are met, start the quest
        QuestService.AddOrUpdateQuest(player, questId, QuestStatus.START);
        return true;
    }

    /// <summary>Starts or locks quest after quest completion (usually from campaign quest handlers).</summary>
    public bool DefaultOnQuestCompletedEvent(QuestEnv env, params int[] preQuests)
    {
        Player player = env.GetPlayer();
        int finishedQuestId = env.GetQuestId();
        QuestStateList qsl = player.GetQuestStateList();
        QuestState qs = qsl.GetQuestState(questId);

        // Only null or LOCKED quests can be started
        if (qs != null && qs.GetStatus() != QuestStatus.LOCKED)
            return false;

        QuestTemplate template = DataManager.QUEST_DATA.GetQuestById(questId);
        int minLvlDiff = template.IsMission() ? 15 : 0; // this ensures to add all follow-up quests in locked state
        // Check all player requirements first
        if (!QuestService.CheckStartConditions(player, questId, false, minLvlDiff, false, false, template.IsMission()))
            return false;

        bool missingRequirement = false;
        bool hasFinishedPreQuest = false;
        foreach (int id in preQuests)
        {
            QuestState qs2 = qsl.GetQuestState(id);
            if (!missingRequirement && (qs2 == null || qs2.GetStatus() != QuestStatus.COMPLETE))
            {
                if (qs != null || !template.IsMission())
                    return false;
                missingRequirement = true;
            }
            if (finishedQuestId == id)
                hasFinishedPreQuest = true;
            if (missingRequirement && (hasFinishedPreQuest || qs2 != null && qs2.GetStatus() == QuestStatus.COMPLETE)) // if any pre quest is finished
            {
                QuestService.AddOrUpdateQuest(player, questId, QuestStatus.LOCKED);
                return false;
            }
        }
        if (missingRequirement)
            return false;

        // Check quests that have to be done before starting this one and other start conditions
        missingRequirement = false;
        foreach (XMLStartCondition cond in template.GetXMLStartConditions())
        {
            if (!cond.Check(player, false))
            {
                if (qs != null || !template.IsMission())
                    return false;
                else if (HasAnyPreQuestFinished(qsl, cond)) // recursive check
                {
                    QuestService.AddOrUpdateQuest(player, questId, QuestStatus.LOCKED);
                    return false;
                }
                missingRequirement = true;
            }
        }
        if (missingRequirement)
            return false;

        // Send locked quest if the player's level is in the minLvlDiff range (1-15)
        if (minLvlDiff > 0 && player.GetLevel() < template.GetMinlevelPermitted())
        {
            if (qs == null && hasFinishedPreQuest)
                QuestService.AddOrUpdateQuest(player, questId, QuestStatus.LOCKED);
            return false;
        }

        // All conditions are met, start the quest
        QuestService.AddOrUpdateQuest(player, questId, QuestStatus.START);
        return true;
    }

    /// <summary>Checks recursively if any pre-quest that is required is completed.</summary>
    private static bool HasAnyPreQuestFinished(QuestStateList qsl, XMLStartCondition startCondition)
    {
        List<FinishedQuestCond> finishedQuests = startCondition.GetFinishedPreconditions();
        if (finishedQuests != null)
        {
            foreach (FinishedQuestCond finishedCond in finishedQuests)
            {
                QuestState qs = qsl.GetQuestState(finishedCond.GetQuestId());
                if (qs != null && qs.GetStatus() == QuestStatus.COMPLETE)
                    return true;
                QuestTemplate template = DataManager.QUEST_DATA.GetQuestById(finishedCond.GetQuestId());
                foreach (XMLStartCondition cond in template.GetXMLStartConditions())
                    if (HasAnyPreQuestFinished(qsl, cond))
                        return true;
            }
        }
        return false;
    }

    /// <summary>Start a mission on enter the questZone.</summary>
    public bool DefaultOnEnterZoneEvent(QuestEnv env, ZoneName currentZoneName, ZoneName questZoneName)
    {
        if (questZoneName == currentZoneName)
        {
            Player player = env.GetPlayer();
            if (player == null)
                return false;
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs == null)
            {
                env.SetQuestId(questId);
                if (QuestService.StartQuest(env))
                    return true;
            }
        }
        return false;
    }

    public bool SendQuestRewardDialog(QuestEnv env, int rewardNpcId, int reportDialogId)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (env.GetTargetId() == rewardNpcId)
            {
                if (env.GetDialogActionId() == DialogAction.USE_OBJECT && reportDialogId != 0)
                {
                    return SendQuestDialog(env, reportDialogId);
                }
                else
                {
                    return SendQuestEndDialog(env);
                }
            }
        }
        return false;
    }

    public bool SendQuestNoneDialog(QuestEnv env, int startNpcId)
    {
        QuestTemplate template = DataManager.QUEST_DATA.GetQuestById(questId);
        return SendQuestNoneDialog(env, template, startNpcId, 1011);
    }

    public bool SendQuestNoneDialog(QuestEnv env, int startNpcId, int dialogPageId)
    {
        QuestTemplate template = DataManager.QUEST_DATA.GetQuestById(questId);
        return SendQuestNoneDialog(env, template, startNpcId, dialogPageId);
    }

    public bool SendQuestNoneDialog(QuestEnv env, QuestTemplate template, int startNpcId, int dialogPageId)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null || qs.IsStartable())
        {
            if (env.GetTargetId() == startNpcId)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, dialogPageId);
                }
                else
                {
                    return SendQuestStartDialog(env);
                }
            }
        }
        return false;
    }

    public bool SendQuestNoneDialog(QuestEnv env, int startNpcId, int dialogPageId, int itemId, int itemCout)
    {
        QuestTemplate template = DataManager.QUEST_DATA.GetQuestById(questId);
        return SendQuestNoneDialog(env, template, startNpcId, dialogPageId, itemId, itemCout);
    }

    public bool SendQuestNoneDialog(QuestEnv env, int startNpcId, int itemId, int itemCout)
    {
        QuestTemplate template = DataManager.QUEST_DATA.GetQuestById(questId);
        return SendQuestNoneDialog(env, template, startNpcId, 1011, itemId, itemCout);
    }

    public bool SendQuestNoneDialog(QuestEnv env, QuestTemplate template, int startNpcId, int dialogPageId, int itemId, int itemCout)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null || qs.IsStartable())
        {
            if (env.GetTargetId() == startNpcId)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, dialogPageId);
                }
                if (itemId != 0 && itemCout != 0)
                {
                    if (env.GetDialogActionId() == DialogAction.QUEST_ACCEPT_1)
                    {
                        if (GiveQuestItem(env, itemId, itemCout))
                        {
                            return SendQuestStartDialog(env);
                        }
                        else
                        {
                            return true;
                        }
                    }
                    else
                    {
                        return SendQuestStartDialog(env);
                    }
                }
                else
                {
                    return SendQuestStartDialog(env);
                }
            }
        }
        return false;
    }

    public bool SendItemCollectingStartDialog(QuestEnv env)
    {
        switch (env.GetDialogActionId())
        {
            case DialogAction.QUEST_ACCEPT_1:
                QuestService.StartQuest(env);
                return SendQuestSelectionDialog(env);
            case DialogAction.QUEST_REFUSE_1:
                return SendQuestSelectionDialog(env);
        }
        return false;
    }

    public static VisibleObject Spawn(int templateId, VisibleObject objectToGetInstanceFrom, float x, float y, float z, byte heading)
    {
        return Spawn(templateId, objectToGetInstanceFrom.GetWorldMapInstance(), x, y, z, heading);
    }

    public static VisibleObject Spawn(int templateId, WorldMapInstance worldMapInstance, float x, float y, float z, byte heading)
    {
        SpawnTemplate template = SpawnEngine.NewSingleTimeSpawn(worldMapInstance.GetMapId(), templateId, x, y, z, heading);
        return SpawnEngine.SpawnObject(template, worldMapInstance.GetInstanceId());
    }

    public static VisibleObject SpawnInFrontOf(int templateId, VisibleObject referencePositionObject)
    {
        return SpawnInFront(templateId, referencePositionObject.GetPosition(), null, 1.5f, 0);
    }

    public static VisibleObject SpawnForFiveMinutesInFrontOf(int templateId, VisibleObject referencePositionObject, float distance)
    {
        return SpawnInFront(templateId, referencePositionObject.GetPosition(), null, distance, 5);
    }

    public static VisibleObject SpawnForFiveMinutesInFront(int templateId, VisibleObject referencePositionObject, byte heading, float distance)
    {
        return SpawnInFront(templateId, referencePositionObject.GetPosition(), heading, distance, 5);
    }

    private static VisibleObject SpawnInFront(int templateId, WorldPosition referencePosition, byte? heading, float distance, int timeInMin)
    {
        if (heading == null) // make the spawn face towards referencePosition
            heading = (byte)(referencePosition.GetHeading() < 60 ? referencePosition.GetHeading() + 60 : referencePosition.GetHeading() - 60);
        double radian = PositionUtil.ConvertHeadingToAngle(referencePosition.GetHeading()) * Math.PI / 180;
        float x = referencePosition.GetX() + (float)(Math.Cos(radian) * distance);
        float y = referencePosition.GetY() + (float)(Math.Sin(radian) * distance);
        float z = referencePosition.GetZ();
        float geoZ = GeoService.GetInstance().GetZ(referencePosition.GetMapId(), x, y, z + 2, z - 1, referencePosition.GetInstanceId());
        if (!float.IsNaN(geoZ))
            z = geoZ;
        return SpawnTemporarily(templateId, referencePosition.GetWorldMapInstance(), x, y, z, heading.Value, timeInMin);
    }

    public static VisibleObject SpawnForFiveMinutes(int templateId, WorldPosition position)
    {
        return SpawnForFiveMinutes(templateId, position, position.GetHeading());
    }

    public static VisibleObject SpawnForFiveMinutes(int templateId, WorldPosition position, byte heading)
    {
        return SpawnTemporarily(templateId, position.GetWorldMapInstance(), position.GetX(), position.GetY(), position.GetZ(), heading, 5);
    }

    public static VisibleObject SpawnForFiveMinutes(int templateId, WorldMapInstance worldMapInstance, float x, float y, float z, byte heading)
    {
        return SpawnTemporarily(templateId, worldMapInstance, x, y, z, heading, 5);
    }

    public static VisibleObject SpawnTemporarily(int templateId, WorldMapInstance worldMapInstance, float x, float y, float z, byte heading, int timeInMin)
    {
        VisibleObject obj = Spawn(templateId, worldMapInstance, x, y, z, heading);
        if (timeInMin > 0)
            ThreadPoolManager.GetInstance().Schedule(ct => { obj.GetController().DeleteIfAliveOrCancelRespawn(); return ValueTask.CompletedTask; }, TimeSpan.FromMilliseconds(60000 * timeInMin));
        return obj;
    }
}
