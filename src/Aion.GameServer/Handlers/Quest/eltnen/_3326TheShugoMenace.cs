using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _3326TheShugoMenace : AbstractQuestHandler
{
    public _3326TheShugoMenace() : base(3326)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798053).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(798053).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(210897).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(210939).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(210873).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(210919).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(211754).AddOnKillEvent(questId);
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
            if (targetId == 798053)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 4);
                }
                else
                    return SendQuestStartDialog(env);
            }
        }

        if (qs == null)
            return false;

        if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 798053)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 10002);
                    case DialogAction.SELECT_QUEST_REWARD:
                        if (qs.GetQuestVarById(0) != 20)
                        {
                            return false;
                        }
                        qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                        qs.SetStatus(QuestStatus.REWARD);
                        UpdateQuestStatus(env);
                        return SendQuestEndDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 798053)
            {
                if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD)
                    return SendQuestDialog(env, 5);
                else
                    return SendQuestEndDialog(env);
            }
        }
        return false;
    }

    public override bool OnKillEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null || qs.GetStatus() != QuestStatus.START)
            return false;

        int var = qs.GetQuestVarById(0);
        int targetId = 0;
        if (env.GetVisibleObject() is Npc npc)
            targetId = npc.GetNpcId();

        if (targetId == 210897 || targetId == 210939 || targetId == 210873 || targetId == 210919 || targetId == 211754)
        {
            if (var >= 0 && var < 20)
            {
                qs.SetQuestVarById(0, var + 1);
                UpdateQuestStatus(env);
                return true;
            }
        }
        return false;
    }
}
