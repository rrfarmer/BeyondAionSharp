using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Cheatkiller
/// </summary>
public class _2963OnBehalfOfAFriend : AbstractQuestHandler
{
    public _2963OnBehalfOfAFriend() : base(2963)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204253).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(204253).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(278137).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 204253)
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
            if (targetId == 278137)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (qs.GetQuestVarById(0) == 0)
                    {
                        return SendQuestDialog(env, 1352);
                    }
                }
                else if (dialogActionId == DialogAction.SETPRO1)
                {
                    qs.SetQuestVar(2);
                    return DefaultCloseDialog(env, 2, 2, true, false);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 204253)
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
                {
                    return SendQuestDialog(env, 2375);
                }
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
