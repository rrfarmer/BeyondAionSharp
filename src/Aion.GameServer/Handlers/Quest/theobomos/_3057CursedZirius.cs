using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

public class _3057CursedZirius : AbstractQuestHandler
{
    public _3057CursedZirius() : base(3057)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798213).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(798213).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(214576).AddOnAttackEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 798213)
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
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 798213)
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

    public override bool OnAttackEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            int targetId = env.GetTargetId();
            if (targetId == 214576)
            {
                if (PositionUtil.GetDistance(env.GetVisibleObject(), 1691.41f, 219.09f, 72.62f) <= 30)
                {
                    ((Npc)env.GetVisibleObject()).GetController().Die(player);
                    ChangeQuestStep(env, 0, 1, true);
                    return true;
                }
            }
        }
        return false;
    }
}
