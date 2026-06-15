using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _30003SecretOfTheUdasTemple : AbstractQuestHandler
{
    public _30003SecretOfTheUdasTemple() : base(30003)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(799029).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(799029).AddOnTalkEvent(questId);
        qe.RegisterOnGetItem(182209161, questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 799029) // Honeus
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
            if (targetId == 799029) // Honeus
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (var == 1)
                    {
                        return SendQuestDialog(env, 2375);
                    }
                }
                else if (dialogActionId == DialogAction.SELECT_QUEST_REWARD)
                {
                    ChangeQuestStep(env, 1, 1, true); // reward
                    return SendQuestDialog(env, 5);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 799029) // Honeus
            {
                RemoveQuestItem(env, 182209161, 1);
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }

    public override bool OnGetItemEvent(QuestEnv env)
    {
        return DefaultOnGetItemEvent(env, 0, 1, false); // 1
    }
}
