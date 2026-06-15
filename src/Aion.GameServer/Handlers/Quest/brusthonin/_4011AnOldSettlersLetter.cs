using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

public class _4011AnOldSettlersLetter : AbstractQuestHandler
{
    private static readonly int[] npc_ids = { 730139, 205132, 203522 };

    public _4011AnOldSettlersLetter() : base(4011)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(730139).AddOnQuestStart(questId);
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
        if (targetId == 730139)
        {
            if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                return SendQuestDialog(env, 1011);
            else if (env.GetDialogActionId() == DialogAction.SETPRO1)
            {
                QuestService.StartQuest(env);
                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 0));
                return true;
            }
        }
        if (qs == null)
            return false;

        int var = qs.GetQuestVarById(0);
        if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 205132)
            {
                if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                    return SendQuestDialog(env, 10002);
                else if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD)
                    return SendQuestDialog(env, 5);
                else
                    return SendQuestEndDialog(env);
            }
        }
        else if (qs.GetStatus() != QuestStatus.START)
        {
            return false;
        }
        if (targetId == 205132)
        {
            switch (env.GetDialogActionId())
            {
                case DialogAction.QUEST_SELECT:
                    if (var == 0)
                        return SendQuestDialog(env, 1352);
                    return false;
                case DialogAction.SETPRO2:
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
        else if (targetId == 203522)
        {
            switch (env.GetDialogActionId())
            {
                case DialogAction.QUEST_SELECT:
                    if (var == 1)
                        return SendQuestDialog(env, 1693);
                    return false;
                case DialogAction.SET_SUCCEED:
                    if (var == 1)
                    {
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
