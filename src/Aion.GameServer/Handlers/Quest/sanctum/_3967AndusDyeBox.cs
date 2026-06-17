using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Rolandas
/// </summary>
public class _3967AndusDyeBox : AbstractQuestHandler
{
    public _3967AndusDyeBox() : base(3967)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798391).AddOnQuestStart(questId);// Andu
        qe.RegisterQuestNpc(798309).AddOnTalkEvent(questId);// Arenzes
        qe.RegisterQuestNpc(798391).AddOnTalkEvent(questId);// Andu
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;

        QuestState qs2 = player.GetQuestStateList().GetQuestState(3966);
        if (qs2 == null || qs2.GetStatus() != QuestStatus.COMPLETE)
            return false;

        if (env.GetVisibleObject() is Npc npc)
            targetId = npc.GetNpcId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        if (targetId == 798391)// Andu
        {
            if (qs == null || qs.IsStartable())
            {
                if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                    return SendQuestDialog(env, 1011);
                else
                    return SendQuestStartDialog(env);
            }
        }

        if (qs == null)
            return false;

        int var = qs.GetQuestVarById(0);

        if (targetId == 798309)// Arenzes
        {
            if (qs.GetStatus() == QuestStatus.START && var == 0)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1352);
                else if (env.GetDialogActionId() == DialogAction.SETPRO1)
                {
                    if (GiveQuestItem(env, 182206122, 1))
                    {
                        qs.SetQuestVar(++var);
                        qs.SetStatus(QuestStatus.REWARD);
                        UpdateQuestStatus(env);
                    }
                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                    return true;
                }
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (targetId == 798391 && qs.GetStatus() == QuestStatus.REWARD)
        { // Andu
            if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                return SendQuestDialog(env, 2375);
            else if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD)
            {
                RemoveQuestItem(env, 182206122, 1);
                return SendQuestEndDialog(env);
            }
            else
                return SendQuestEndDialog(env);
        }
        return false;
    }
}
