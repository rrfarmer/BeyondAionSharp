using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _30263DaevasFearToTread : AbstractQuestHandler
{
    public _30263DaevasFearToTread() : base(30263)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(278651).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(278651).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(207945).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();
        if (qs == null || qs.IsStartable())
        {
            if (targetId == 278651) // Aurunerk
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 4762);
                }
                else
                {
                    return SendQuestStartDialog(env, 182209801, 1);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            if (targetId == 207945) // Fallen Shugo
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 0)
                        {
                            return SendQuestDialog(env, 1011);
                        }
                        return false;
                    case DialogAction.SETPRO1:
                        return DefaultCloseDialog(env, 0, 0, true, false, 0, 0, 182209801, 1);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 278651) // Aurunerk
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
}
