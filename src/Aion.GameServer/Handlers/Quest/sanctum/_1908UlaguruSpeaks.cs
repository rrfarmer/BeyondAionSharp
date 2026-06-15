using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _1908UlaguruSpeaks : AbstractQuestHandler
{
    public _1908UlaguruSpeaks() : base(1908)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203864).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(203864).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203890).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 203864)
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
            if (targetId == 203890)
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
                    return DefaultCloseDialog(env, 0, 1);
                }
            }
            else if (targetId == 203864)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (qs.GetQuestVarById(0) == 1)
                    {
                        return SendQuestDialog(env, 2375);
                    }
                }
                else if (dialogActionId == DialogAction.SETPRO21)
                {
                    qs.SetQuestVar(21);
                    qs.SetRewardGroup(0);
                    return SendQuestDialog(env, 2376);
                }
                else if (dialogActionId == DialogAction.SETPRO22)
                {
                    qs.SetQuestVar(22);
                    qs.SetRewardGroup(1);
                    return SendQuestDialog(env, 2461);
                }
                else if (dialogActionId == DialogAction.SETPRO23)
                {
                    qs.SetQuestVar(23);
                    qs.SetRewardGroup(2);
                    return SendQuestDialog(env, 2546);
                }
                else if (dialogActionId == DialogAction.SELECT_QUEST_REWARD)
                {
                    qs.SetStatus(QuestStatus.REWARD);
                    UpdateQuestStatus(env);
                    return SendQuestEndDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203864)
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
