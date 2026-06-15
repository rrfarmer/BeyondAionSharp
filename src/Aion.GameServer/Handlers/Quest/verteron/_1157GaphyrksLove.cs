using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Rhys2002
/// </summary>
public class _1157GaphyrksLove : AbstractQuestHandler
{
    public _1157GaphyrksLove() : base(1157)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798003).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(798003).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(210319).AddOnAttackEvent(questId);
    }

    public override bool OnAttackEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        if (qs == null || qs.GetStatus() != QuestStatus.START)
            return false;

        int targetId = 0;
        if (env.GetVisibleObject() is Npc)
            targetId = ((Npc)env.GetVisibleObject()).GetNpcId();
        if (targetId != 210319)
            return false;

        Npc npc = (Npc)env.GetVisibleObject();

        if (PositionUtil.GetDistance(892, 2024, 166, npc.GetX(), npc.GetY(), npc.GetZ()) > 13)
        {
            return false;
        }
        npc.GetController().DeleteAndScheduleRespawn();
        PlayQuestMovie(env, 17);
        return true;
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        int targetId = 0;
        if (env.GetVisibleObject() is Npc)
            targetId = ((Npc)env.GetVisibleObject()).GetNpcId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 798003)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 798003)
            {
                if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                    return SendQuestDialog(env, 2375);
                else if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD)
                    return SendQuestDialog(env, 5);
                else
                    return SendQuestEndDialog(env);
            }
            return false;
        }
        return false;
    }

    public override void OnMovieEndEvent(QuestEnv env, int movieId)
    {
        if (movieId == 17)
            ChangeQuestStep(env, 0, 0, true); // reward
    }
}
