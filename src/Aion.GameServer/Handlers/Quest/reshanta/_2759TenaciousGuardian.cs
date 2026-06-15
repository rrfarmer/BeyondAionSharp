using System.Collections.Generic;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author vlog
/// </summary>
public class _2759TenaciousGuardian : AbstractQuestHandler
{
    private readonly List<int> killedMobs = new List<int>();

    public _2759TenaciousGuardian() : base(2759)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(264769).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(264769).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(278588).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(278589).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(278590).AddOnKillEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 264769) // Gudharten
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 1011);
                }
                else
                {
                    return SendQuestStartDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            if (targetId == 264769) // Gudharten
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (var == 3)
                    {
                        return SendQuestDialog(env, 1352);
                    }
                }
                else if (dialogActionId == DialogAction.SELECT_QUEST_REWARD)
                {
                    ChangeQuestStep(env, 3, 3, true); // reward
                    killedMobs.Clear();
                    return SendQuestDialog(env, 5);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 264769) // Gudharten
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }

    public override bool OnKillEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int targetId = env.GetTargetId();
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            if (var < 3)
            {
                switch (targetId)
                {
                    case 278588:
                        if (!killedMobs.Contains(278588))
                        {
                            killedMobs.Add(278588);
                            return DefaultOnKillEvent(env, 278588, var, var + 1);
                        }
                        break;
                    case 278589:
                        if (!killedMobs.Contains(278589))
                        {
                            killedMobs.Add(278589);
                            return DefaultOnKillEvent(env, 278589, var, var + 1);
                        }
                        break;
                    case 278590:
                        if (!killedMobs.Contains(278590))
                        {
                            killedMobs.Add(278590);
                            return DefaultOnKillEvent(env, 278590, var, var + 1);
                        }
                        break;
                }
            }
        }
        return false;
    }
}
