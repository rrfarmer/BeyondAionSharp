using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Rhys2002
/// </summary>
public class _1114TheNymphsGown : AbstractQuestHandler
{
    private static readonly int[] npc_ids = { 203075, 203058, 700008 };

    public _1114TheNymphsGown() : base(1114)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestItem(182200214, questId);
        foreach (int npc_id in npc_ids)
            qe.RegisterQuestNpc(npc_id).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = env.GetTargetId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        if (targetId == 0)
        {
            if (qs == null || qs.IsStartable())
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_ACCEPT_1)
                {
                    QuestService.StartQuest(env);
                    GiveQuestItem(env, 182200226, 1);
                    RemoveQuestItem(env, 182200214, 1); // Namus's Diary (which started the quest via double-click)
                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(0, 0));
                    return true;
                }
                else
                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(0, 0));
            }
        }

        if (qs == null)
            return false;

        int var = qs.GetQuestVarById(0);

        if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203075 && var == 4)
            { // Namus
                if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                    return SendQuestDialog(env, 2375);
                else if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD)
                    return SendQuestDialog(env, 6);
                else
                    return SendQuestEndDialog(env);
            }
            else if (targetId == 203058 && var == 3) // Asteros
                return SendQuestEndDialog(env);
        }
        else if (qs.GetStatus() != QuestStatus.START)
            return false;

        if (targetId == 203075)
        { // Namus
            switch (env.GetDialogActionId())
            {
                case DialogAction.QUEST_SELECT:
                    if (var == 0)
                        return SendQuestDialog(env, 1011);
                    else if (var == 2)
                        return SendQuestDialog(env, 1693);
                    else if (var == 3)
                        return SendQuestDialog(env, 2375);
                    return false;
                case DialogAction.SELECT_QUEST_REWARD:
                    if (var == 2 || var == 3)
                    {
                        qs.SetQuestVarById(0, 4);
                        qs.SetStatus(QuestStatus.REWARD);
                        qs.SetRewardGroup(1);
                        UpdateQuestStatus(env);
                        RemoveQuestItem(env, 182200217, 1);
                        return SendQuestDialog(env, 6);
                    }
                    return false;
                case DialogAction.SETPRO1:
                    if (var == 0)
                    {
                        qs.SetQuestVarById(0, var + 1);
                        UpdateQuestStatus(env);
                        RemoveQuestItem(env, 182200226, 1);
                        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                        return true;
                    }
                    return false;
                case DialogAction.SETPRO2:
                    if (var == 2)
                    {
                        qs.SetQuestVarById(0, var + 1);
                        UpdateQuestStatus(env);
                        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                        return true;
                    }
                    break;
            }
        }
        else if (targetId == 700008)
        { // Seirenia's clothes
            switch (env.GetDialogActionId())
            {
                case DialogAction.USE_OBJECT:
                    if (var == 1)
                    {
                        player.GetKnownList().ForEachNpc(npc =>
                        {
                            if (npc.GetNpcId() != 203175) // Seirenia
                                return;
                            npc.GetAggroList().AddHate(player, 50);
                        });
                        GiveQuestItem(env, 182200217, 1); // Nymph's Dress
                        qs.SetQuestVarById(0, 2);
                        UpdateQuestStatus(env);
                    }
                    return true;
            }
        }
        if (targetId == 203058)
        { // Asteros
            switch (env.GetDialogActionId())
            {
                case DialogAction.QUEST_SELECT:
                    if (var == 3)
                        return SendQuestDialog(env, 2034);
                    return false;
                case DialogAction.SETPRO3:
                    if (var == 3)
                    {
                        qs.SetStatus(QuestStatus.REWARD);
                        qs.SetRewardGroup(0);
                        UpdateQuestStatus(env);
                        RemoveQuestItem(env, 182200217, 1);
                        return SendQuestDialog(env, 5);
                    }
                    return false;
                case DialogAction.SETPRO2:
                    if (var == 3)
                    {
                        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                        return true;
                    }
                    break;
            }
        }
        return false;
    }

    public override HandlerResult OnItemUseEvent(QuestEnv env, Item item)
    {
        Player player = env.GetPlayer();
        int id = item.GetItemTemplate().GetTemplateId();
        int itemObjId = item.GetObjectId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        if (id != 182200214)
            return HandlerResult.UNKNOWN;

        PacketSendUtility.BroadcastPacket(player, new SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), itemObjId, id, 20, 1, 0), true);
        if (qs == null || qs.IsStartable())
        {
            QuestService.StartQuest(env);
        }
        return HandlerResult.SUCCESS;
    }
}
