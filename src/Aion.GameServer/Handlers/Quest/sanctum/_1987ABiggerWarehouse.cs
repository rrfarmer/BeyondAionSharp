using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _1987ABiggerWarehouse : AbstractQuestHandler
{
    public _1987ABiggerWarehouse() : base(1987)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203700).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(203700).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203749).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null)
        {
            if (targetId == 203700) // Fasimedes
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
            if (targetId == 203749) // Bustant
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 2375);
                    case DialogAction.SELECT_QUEST_REWARD:
                        ChangeQuestStep(env, 0, 0, true); // reward
                        return SendQuestDialog(env, 5);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203749) // Bustant
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
