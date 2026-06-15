using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

public class _4077PorgusRoundup : AbstractQuestHandler
{
    public _4077PorgusRoundup() : base(4077)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(205158).AddOnQuestStart(questId); // Holekk
        qe.RegisterQuestNpc(205158).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(214732).AddOnAttackEvent(questId);
    }

    public override bool OnAttackEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        int targetId = 0;
        if (env.GetVisibleObject() is Npc visNpc)
            targetId = visNpc.GetNpcId();
        if (targetId != 214732)
            return false;

        Npc npc = (Npc)env.GetVisibleObject();
        if (qs != null && qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 0)
        {
            if (PositionUtil.GetDistance(1356, 1901, 46, npc.GetX(), npc.GetY(), npc.GetZ()) > 10)
            {
                return false;
            }
            qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
            UpdateQuestStatus(env);
            npc.GetController().DeleteAndScheduleRespawn();
            return true;
        }
        else if (qs != null && qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 1)
        {
            if (PositionUtil.GetDistance(1356, 1901, 46, npc.GetX(), npc.GetY(), npc.GetZ()) > 10)
            {
                return false;
            }
            qs.SetStatus(QuestStatus.REWARD);
            UpdateQuestStatus(env);
            npc.GetController().DeleteAndScheduleRespawn();
            return true;
        }

        return false;
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        int targetId = 0;
        if (env.GetVisibleObject() is Npc npc)
            targetId = npc.GetNpcId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 205158) // Holekk
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 4762);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 205158)
                return SendQuestEndDialog(env);
        }
        return false;
    }
}
