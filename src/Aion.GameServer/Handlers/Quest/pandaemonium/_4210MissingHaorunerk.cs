using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/**
 * @author Cheatkiller
 */
public class _4210MissingHaorunerk : AbstractQuestHandler
{
    public _4210MissingHaorunerk() : base(4210)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204283).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(798331).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798333).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(215056).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(215080).AddOnKillEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 204283)
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
            if (targetId == 798333 && qs.GetQuestVarById(0) == 0) // Haorunerk's Corpse
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 1011);
                }
                else if (dialogActionId == DialogAction.SETPRO1)
                {
                    return DefaultCloseDialog(env, 0, 1);
                }
            }
            else if (targetId == 798331)
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
                {
                    if (qs.GetQuestVarById(1) == 1 && qs.GetQuestVarById(2) == 1)
                    {
                        return SendQuestDialog(env, 10002);
                    }
                }
                else if (dialogActionId == DialogAction.SELECT_QUEST_REWARD)
                {
                    return DefaultCloseDialog(env, 1, 1, true, true);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 798331)
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
                {
                    return SendQuestDialog(env, 10002);
                }
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }

    public override bool OnKillEvent(QuestEnv env)
    {
        if (DefaultOnKillEvent(env, 215056, 0, 1, 1) || DefaultOnKillEvent(env, 215080, 0, 1, 2))
        {
            return true;
        }
        return false;
    }
}
