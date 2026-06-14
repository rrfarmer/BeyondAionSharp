using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _80009TheCakeIsTheTruth : AbstractQuestHandler
{
    public _80009TheCakeIsTheTruth() : base(80009)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798417).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();

        if (env.GetTargetId() == 0)
            return SendQuestStartDialog(env);

        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null)
            return false;

        int var = qs.GetQuestVarById(0);

        if (qs.GetStatus() == QuestStatus.START)
        {
            if (env.GetTargetId() == 798417)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 0)
                            return SendQuestDialog(env, 2375);
                        return false;
                    case DialogAction.SELECT_QUEST_REWARD:
                        RemoveQuestItem(env, 182214007, 1);
                        return DefaultCloseDialog(env, 0, 1, true, true);
                }
            }
        }
        return SendQuestRewardDialog(env, 798417, 0);
    }
}
