using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

public class _3969SexiestManAlive : AbstractQuestHandler
{
    public _3969SexiestManAlive() : base(3969)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798390).AddOnQuestStart(questId); // Palentine
        qe.RegisterQuestNpc(798391).AddOnTalkEvent(questId); // Andu
        qe.RegisterQuestNpc(798390).AddOnTalkEvent(questId); // Palentine
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;

        QuestState qs2 = player.GetQuestStateList().GetQuestState(3968);
        if (qs2 == null || qs2.GetStatus() != QuestStatus.COMPLETE)
            return false;

        if (env.GetVisibleObject() is Npc)
            targetId = ((Npc)env.GetVisibleObject()).GetNpcId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        if (targetId == 798390) // Palentine
        {
            if (qs == null || qs.IsStartable())
            {
                if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                    return SendQuestDialog(env, 1011);
                else if (env.GetDialogActionId() == DialogAction.QUEST_ACCEPT_1)
                {
                    if (GiveQuestItem(env, 182206126, 1))
                    {
                        return SendQuestStartDialog(env);
                    }
                    return true;
                }
                else
                    return SendQuestStartDialog(env);
            }
        }

        if (qs == null)
            return false;

        int var = qs.GetQuestVarById(0);

        if (targetId == 798391) // Andu
        {
            if (qs.GetStatus() == QuestStatus.START && var == 0)
            {
                if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                    return SendQuestDialog(env, 1352);
                else if (env.GetDialogActionId() == DialogAction.SETPRO1)
                {
                    if (player.GetInventory().GetItemCountByItemId(182206126) > 0)
                    {
                        RemoveQuestItem(env, 182206126, 1);
                        qs.SetQuestVar(++var);
                        qs.SetStatus(QuestStatus.REWARD);
                        UpdateQuestStatus(env);
                        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                    }
                    return true;
                }
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (targetId == 798390)
        { // Palentine
            if (qs.GetStatus() == QuestStatus.REWARD)
            {
                if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                    return SendQuestDialog(env, 2375);
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
