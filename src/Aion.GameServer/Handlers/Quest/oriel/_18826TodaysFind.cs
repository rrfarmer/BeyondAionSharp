using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _18826TodaysFind : AbstractQuestHandler
{
    public _18826TodaysFind() : base(18826)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(830520).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(830520).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(830660).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(830660).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(830661).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(830661).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(730522).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 830520 || targetId == 830660 || targetId == 830661)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 730522)
            {
                switch (dialogActionId)
                {
                    case DialogAction.USE_OBJECT:
                        return SendQuestDialog(env, 2375);
                    case DialogAction.SELECT_QUEST_REWARD:
                        ChangeQuestStep(env, 0, 0, true);
                        return SendQuestDialog(env, 5);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD && targetId == 730522)
        {
            return SendQuestEndDialog(env);
        }
        return false;
    }
}
