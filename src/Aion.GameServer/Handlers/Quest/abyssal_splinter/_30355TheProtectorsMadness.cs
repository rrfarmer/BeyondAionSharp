using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/**
 * @author Rikka & vlog
 */
public class _30355TheProtectorsMadness : AbstractQuestHandler
{
    public _30355TheProtectorsMadness() : base(30355)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(260265).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(260265).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(700858).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(278001).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(216952).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(216960).AddOnKillEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 260265) // Gwal
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
        else if (qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            if (targetId == 700858) // Artifact of Protection
            {
                switch (dialogActionId)
                {
                    case DialogAction.USE_OBJECT:
                        if (var == 1)
                        {
                            return UseQuestObject(env, 1, 1, true, false);
                        }
                        break;
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 278001) // Votan
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 10002);
                    default:
                        {
                            return SendQuestEndDialog(env);
                        }
                }
            }
        }
        return false;
    }

    public override bool OnKillEvent(QuestEnv env)
    {
        int[] mobs = { 216952, 216960 };
        return DefaultOnKillEvent(env, mobs, 0, 1); // 1
    }
}
