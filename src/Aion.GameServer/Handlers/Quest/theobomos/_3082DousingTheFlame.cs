using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _3082DousingTheFlame : AbstractQuestHandler
{
    public _3082DousingTheFlame() : base(3082)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798116).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(204030).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(700416).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798155).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 798116)
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
            if (targetId == 204030)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (qs.GetQuestVarById(0) == 0)
                    {
                        return SendQuestDialog(env, 1011);
                    }
                    else if (qs.GetQuestVarById(0) == 1)
                    {
                        return SendQuestDialog(env, 1352);
                    }
                }
                else if (dialogActionId == DialogAction.CHECK_USER_HAS_QUEST_ITEM)
                {
                    return CheckQuestItems(env, 0, 1, false, 10000, 10001, 182208060, 1);
                }
                else if (dialogActionId == DialogAction.SETPRO2)
                {
                    return DefaultCloseDialog(env, 1, 2);
                }
            }
            else if (targetId == 700416 && qs.GetQuestVarById(0) == 2)
            {
                SpawnForFiveMinutes(700417, env.GetVisibleObject().GetPosition());
                RemoveQuestItem(env, 182208060, 1);
                return UseQuestObject(env, 2, 2, true, false);
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 798155)
            {
                switch (dialogActionId)
                {
                    case DialogAction.USE_OBJECT:
                        return SendQuestDialog(env, 10002);
                    default:
                    {
                        return SendQuestEndDialog(env);
                    }
                }
            }
        }
        return false;
    }
}
