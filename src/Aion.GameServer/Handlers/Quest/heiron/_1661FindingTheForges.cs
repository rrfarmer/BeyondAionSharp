using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Cheatkiller
/// </summary>
public class _1661FindingTheForges : AbstractQuestHandler
{
    public _1661FindingTheForges() : base(1661)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204600).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(204600).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(206045).AddOnAtDistanceEvent(questId);
        qe.RegisterQuestNpc(206046).AddOnAtDistanceEvent(questId);
        qe.RegisterQuestNpc(206047).AddOnAtDistanceEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        if (env.GetVisibleObject() is Npc)
            targetId = ((Npc)env.GetVisibleObject()).GetNpcId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 204600)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 1011);
                }
                else if (dialogActionId == DialogAction.QUEST_ACCEPT_1)
                {
                    PlayQuestMovie(env, 200);
                    return SendQuestStartDialog(env);
                }
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 204600)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 1352);
                }
                else
                    return SendQuestEndDialog(env);
            }
        }
        return false;
    }

    public override bool OnAtDistanceEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        if (env.GetVisibleObject() is Npc)
            targetId = ((Npc)env.GetVisibleObject()).GetNpcId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            if (targetId == 206045 && var == 0)
            {
                ChangeQuestStep(env, 0, 16);
                return true;
            }
            else if (targetId == 206046 && var == 16)
            {
                ChangeQuestStep(env, 16, 48);
                return true;
            }
            else if (targetId == 206047 && var == 48)
            {
                ChangeQuestStep(env, 48, 48, true);
                return true;
            }
        }
        return false;
    }
}
