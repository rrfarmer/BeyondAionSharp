using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

public class _4051BukmirsWorry : AbstractQuestHandler
{
    public _4051BukmirsWorry() : base(4051)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(730152).AddOnQuestStart(questId); // Heavy Bukmir
        qe.RegisterQuestNpc(730152).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(205202).AddOnTalkEvent(questId); // BuBu Chi
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        if (env.GetVisibleObject() is Npc)
            targetId = ((Npc)env.GetVisibleObject()).GetNpcId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (targetId == 730152) // Heavy Bukmir
        {
            if (qs == null || qs.IsStartable())
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 4762);
                else
                    return SendQuestStartDialog(env);
            }
            else if (qs.GetStatus() == QuestStatus.REWARD)
            {
                return SendQuestEndDialog(env);
            }
        }
        else if (targetId == 205202) // BuBu Chi
        {
            if (qs != null && qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 0)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else if (env.GetDialogActionId() == DialogAction.SETPRO1)
                {
                    qs.SetQuestVar(1);
                    qs.SetStatus(QuestStatus.REWARD);
                    UpdateQuestStatus(env);
                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().ObjectId, 10));
                    return true;
                }
                else
                    return SendQuestStartDialog(env);
            }
        }

        return false;
    }
}
