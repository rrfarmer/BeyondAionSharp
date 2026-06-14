using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _28826FreetoaGoodHome : AbstractQuestHandler
{
    public _28826FreetoaGoodHome() : base(28826)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(830663).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(830663).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(830521).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(830521).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(830662).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(830662).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(730525).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 830662 || targetId == 830663 || targetId == 830521)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 730525)
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
        else if (qs.GetStatus() == QuestStatus.REWARD && targetId == 730525)
        {
            return SendQuestEndDialog(env);
        }
        return false;
    }
}
