using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Rhys2002
/// </summary>
public class _1722RastinsHomesickness : AbstractQuestHandler
{
    private static readonly int[] npc_ids = { 278547, 278560, 278517, 278544, 278532, 278539, 278524, 278555, 278567 };

    public _1722RastinsHomesickness() : base(1722)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(278547).AddOnQuestStart(questId);
        foreach (int npc_id in npc_ids)
            qe.RegisterQuestNpc(npc_id).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        if (env.GetVisibleObject() is Npc npc)
            targetId = npc.GetNpcId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (targetId == 278547)
        {
            if (qs == null || qs.IsStartable())
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 4762);
                else
                    return SendQuestStartDialog(env);
            }
        }
        if (qs == null)
            return false;

        int var = qs.GetQuestVarById(0);
        if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 278547)
                RemoveQuestItem(env, 182202101, 1);
            return SendQuestEndDialog(env);
        }
        else if (qs.GetStatus() != QuestStatus.START)
        {
            return false;
        }
        if (targetId == 278560)
        {
            switch (env.GetDialogActionId())
            {
                case DialogAction.QUEST_SELECT:
                    if (var == 0)
                        return SendQuestDialog(env, 1011);
                    return false;
                case DialogAction.SETPRO1:
                    if (var == 0)
                    {
                        qs.SetQuestVarById(0, var + 1);
                        UpdateQuestStatus(env);
                        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                        return true;
                    }
                    return false;
            }
        }
        else if (targetId == 278517)
        {
            switch (env.GetDialogActionId())
            {
                case DialogAction.QUEST_SELECT:
                    if (var == 1)
                        return SendQuestDialog(env, 1352);
                    return false;
                case DialogAction.SETPRO2:
                    if (var == 1)
                    {
                        qs.SetQuestVarById(0, var + 1);
                        UpdateQuestStatus(env);
                        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                        return true;
                    }
                    return false;
            }
        }
        else if (targetId == 278544)
        {
            switch (env.GetDialogActionId())
            {
                case DialogAction.QUEST_SELECT:
                    if (var == 2)
                        return SendQuestDialog(env, 1693);
                    return false;
                case DialogAction.SELECT_QUEST_REWARD:
                    if (var == 2)
                    {
                        qs.SetQuestVarById(0, var + 1);
                        UpdateQuestStatus(env);
                        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                        return true;
                    }
                    return false;
            }
        }
        else if (targetId == 278532)
        {
            switch (env.GetDialogActionId())
            {
                case DialogAction.QUEST_SELECT:
                    if (var == 3)
                        return SendQuestDialog(env, 2034);
                    return false;
                case DialogAction.SETPRO4:
                    if (var == 3)
                    {
                        qs.SetQuestVarById(0, var + 1);
                        UpdateQuestStatus(env);
                        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                        return true;
                    }
                    return false;
            }
        }
        else if (targetId == 278539)
        {
            switch (env.GetDialogActionId())
            {
                case DialogAction.QUEST_SELECT:
                    if (var == 4)
                        return SendQuestDialog(env, 2375);
                    return false;
                case DialogAction.SETPRO5:
                    if (var == 4)
                    {
                        qs.SetQuestVarById(0, var + 1);
                        UpdateQuestStatus(env);
                        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                        return true;
                    }
                    return false;
            }
        }
        else if (targetId == 278524)
        {
            switch (env.GetDialogActionId())
            {
                case DialogAction.QUEST_SELECT:
                    if (var == 5)
                        return SendQuestDialog(env, 2716);
                    return false;
                case DialogAction.SETPRO6:
                    if (var == 5)
                    {
                        qs.SetQuestVarById(0, var + 1);
                        UpdateQuestStatus(env);
                        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                        return true;
                    }
                    return false;
            }
        }
        else if (targetId == 278555)
        {
            switch (env.GetDialogActionId())
            {
                case DialogAction.QUEST_SELECT:
                    if (var == 6)
                        return SendQuestDialog(env, 3057);
                    return false;
                case DialogAction.SETPRO7:
                    if (var == 6)
                    {
                        qs.SetQuestVarById(0, var + 1);
                        UpdateQuestStatus(env);
                        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                        return true;
                    }
                    return false;
            }
        }
        else if (targetId == 278567)
        {
            switch (env.GetDialogActionId())
            {
                case DialogAction.QUEST_SELECT:
                    if (var == 7)
                        return SendQuestDialog(env, 3398);
                    return false;
                case DialogAction.SET_SUCCEED:
                    if (var == 7)
                    {
                        if (!GiveQuestItem(env, 182202101, 1))
                            return true;
                        qs.SetStatus(QuestStatus.REWARD);
                        UpdateQuestStatus(env);
                        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                        return true;
                    }
                    return false;
            }
        }
        return false;
    }
}
