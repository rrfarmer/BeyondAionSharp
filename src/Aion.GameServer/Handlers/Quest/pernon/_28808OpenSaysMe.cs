using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _28808OpenSaysMe : AbstractQuestHandler
{
    public _28808OpenSaysMe() : base(28808)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(830392).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(830392).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(730534).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 830392)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                if (dialogActionId == DialogAction.QUEST_ACCEPT_SIMPLE || dialogActionId == DialogAction.QUEST_ACCEPT_1)
                {
                    if (GiveQuestItem(env, 182213216, 1))
                        return SendQuestStartDialog(env);
                    else
                        return true;
                }
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            switch (targetId)
            {
                case 730534:
                    switch (dialogActionId)
                    {
                        case DialogAction.USE_OBJECT:
                        {
                            if (var == 0)
                                return SendQuestDialog(env, 2375);
                            return false;
                        }
                        case DialogAction.SELECT_QUEST_REWARD:
                        {
                            ChangeQuestStep(env, 0, 0, true);
                            return SendQuestDialog(env, 5);
                        }
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 730534)
                return SendQuestEndDialog(env);
        }
        return false;
    }
}
