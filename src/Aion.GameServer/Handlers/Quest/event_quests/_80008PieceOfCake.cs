using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.GameServer.Handlers.Quest;

public class _80008PieceOfCake : AbstractQuestHandler
{
    public _80008PieceOfCake() : base(80008)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798415).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();

        if (env.GetTargetId() == 0)
        {
            if (env.GetDialogActionId() == DialogAction.QUEST_ACCEPT_1)
            {
                QuestService.StartEventQuest(env, QuestStatus.START);
                CloseDialogWindow(env);
                return true;
            }
        }

        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null)
            return false;

        int var = qs.GetQuestVarById(0);

        if (qs.GetStatus() == QuestStatus.START)
        {
            if (env.GetTargetId() == 798415)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 0)
                            return SendQuestDialog(env, 2375);
                        return false;
                    case DialogAction.SELECT_QUEST_REWARD:
                        RemoveQuestItem(env, 182214006, 1);
                        return DefaultCloseDialog(env, 0, 1, true, true);
                }
            }
        }
        return SendQuestRewardDialog(env, 798415, 0);
    }
}
