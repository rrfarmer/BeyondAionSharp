using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Nephis and AU team helper
/// </summary>
public class _2911SongOfBlessing : AbstractQuestHandler
{
    public _2911SongOfBlessing() : base(2911)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204079).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(204079).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(204193).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        if (env.GetVisibleObject() is Npc)
            targetId = ((Npc)env.GetVisibleObject()).GetNpcId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (targetId == 204079)
        {
            if (qs == null || qs.IsStartable())
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (targetId == 204193)
        {
            if (qs != null && qs.GetStatus() != QuestStatus.COMPLETE)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT && qs.GetStatus() == QuestStatus.START)
                    return SendQuestDialog(env, 1352);
                else if (env.GetDialogActionId() == DialogAction.SETPRO1)
                {
                    qs.SetQuestVar(2);
                    qs.SetRewardGroup(0);
                    qs.SetStatus(QuestStatus.REWARD);
                    UpdateQuestStatus(env);
                    return SendQuestDialog(env, 5);
                }
                else if (env.GetDialogActionId() == DialogAction.SETPRO2)
                {
                    qs.SetQuestVar(3);
                    qs.SetRewardGroup(1);
                    qs.SetStatus(QuestStatus.REWARD);
                    UpdateQuestStatus(env);
                    return SendQuestDialog(env, 6);
                }
                else
                    return SendQuestEndDialog(env); // different rewards and follow-up quests, depending on the decision
            }
        }
        return false;
    }
}
