using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.GameServer.Handlers.Quest;

public class _28802BeItEverSoHumble : AbstractQuestHandler
{
    public _28802BeItEverSoHumble() : base(28802)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(830102).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(830102).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(830153).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 830102)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 1011);
                    case DialogAction.QUEST_ACCEPT_1:
                    case DialogAction.QUEST_ACCEPT_SIMPLE:
                        return SendQuestStartDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 830153:
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                        {
                            return SendQuestDialog(env, 2375);
                        }
                        case DialogAction.SELECT_QUEST_REWARD:
                        {
                            ChangeQuestStep(env, 0, 0, true);
                            return SendQuestEndDialog(env);
                        }
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 830153)
            {
                if (dialogActionId == DialogAction.SELECTED_QUEST_NOREWARD)
                {
                    HousingService.GetInstance().RegisterPlayerStudio(player);
                }
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
