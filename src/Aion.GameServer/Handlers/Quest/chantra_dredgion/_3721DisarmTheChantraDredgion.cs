using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _3721DisarmTheChantraDredgion : AbstractQuestHandler
{
    public _3721DisarmTheChantraDredgion() : base(3721)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798928).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(798928).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(799069).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(216886).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(700948).AddOnTalkEvent(questId);
        qe.AddHandlerSideQuestDrop(questId, 700948, 182202193, 1, 100, 1);
        qe.RegisterOnGetItem(182202193, questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 798928) // Yulia
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
            int var = qs.GetQuestVarById(0);
            if (targetId == 799069) // Yannis
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 0)
                        {
                            return SendQuestDialog(env, 1011);
                        }
                        return false;
                    case DialogAction.SETPRO1:
                        return DefaultCloseDialog(env, 0, 1); // 1
                }
            }
            else if (targetId == 700948)
            {
                return true;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 798928) // Yulia
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
                {
                    return SendQuestDialog(env, 10002);
                }
                else
                {
                    return SendQuestEndDialog(env);
                }
            }
        }
        return false;
    }

    public override bool OnGetItemEvent(QuestEnv env)
    {
        return DefaultOnGetItemEvent(env, 1, 2, false); // 2
    }

    public override bool OnKillEvent(QuestEnv env)
    {
        return DefaultOnKillEvent(env, 216886, 2, true); // reward
    }
}
