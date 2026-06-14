using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _26985GraveyardTreasure : AbstractQuestHandler
{
    private static readonly int[] npcIds = { 804866, 804863 };

    public _26985GraveyardTreasure() : base(26985)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(npcIds[0]).AddOnQuestStart(questId);
        foreach (int npcId in npcIds)
            qe.RegisterQuestNpc(npcId).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == npcIds[0])
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 4762);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == npcIds[1])
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else if (dialogActionId == DialogAction.SETPRO1)
                    return DefaultCloseDialog(env, 0, 1);
            }
            else if (targetId == npcIds[0])
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1352);
                else if (dialogActionId == DialogAction.CHECK_USER_HAS_QUEST_ITEM)
                    return CheckQuestItems(env, 1, 1, true, 10002, 10001);
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == npcIds[0])
                return SendQuestEndDialog(env);
        }
        return false;
    }
}
