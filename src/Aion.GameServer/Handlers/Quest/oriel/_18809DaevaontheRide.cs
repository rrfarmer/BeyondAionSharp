using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author zhkchi
/// </summary>
public class _18809DaevaontheRide : AbstractQuestHandler
{
    public _18809DaevaontheRide() : base(18809)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(830168).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(830168).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(830263).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(830201).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 830168)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 1011);
                    case DialogAction.QUEST_ACCEPT_1:
                    case DialogAction.QUEST_ACCEPT_SIMPLE:
                        GiveQuestItem(env, 190100013, 1);
                        return SendQuestStartDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 830263:
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            return SendQuestDialog(env, 1352);
                        case DialogAction.SETPRO1:
                            return DefaultCloseDialog(env, 0, 1);
                    }
                    return false;
                case 830201:
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            return SendQuestDialog(env, 1693);
                        case DialogAction.SETPRO2:
                            return DefaultCloseDialog(env, 1, 2);
                    }
                    return false;
                case 830168:
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            return SendQuestDialog(env, 2375);
                        case DialogAction.SELECT_QUEST_REWARD:
                            ChangeQuestStep(env, 2, 2, true);
                            return SendQuestEndDialog(env);
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 830168)
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
