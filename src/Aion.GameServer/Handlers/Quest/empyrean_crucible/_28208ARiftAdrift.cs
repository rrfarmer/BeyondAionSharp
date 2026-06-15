using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author vlog
/// </summary>
public class _28208ARiftAdrift : AbstractQuestHandler
{
    public _28208ARiftAdrift() : base(28208)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(205320).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(205320).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(205321).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(217819).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(218185).AddOnKillEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 205320) // Inggness
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 4762);
                }
                else
                {
                    return SendQuestStartDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 205321) // Anja
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
                {
                    return SendQuestDialog(env, 10002);
                }
                else
                {
                    return SendQuestEndDialog(env);
                }
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
            if (var == 0)
            {
                int var1 = qs.GetQuestVarById(1);
                if (var1 < 4)
                {
                    return DefaultOnKillEvent(env, 217819, 0, 4, 1); // 1: 0 - 4
                }
                else if (var1 == 4 && targetId == 217819)
                {
                    qs.SetQuestVar(1); // 1
                    UpdateQuestStatus(env);
                    return true;
                }
            }
            else if (var == 1 && targetId == 218185)
            {
                qs.SetQuestVarById(2, 1); // 2: 1
                qs.SetStatus(QuestStatus.REWARD); // reward
                UpdateQuestStatus(env);
                return true;
            }
        }
        return false;
    }
}
