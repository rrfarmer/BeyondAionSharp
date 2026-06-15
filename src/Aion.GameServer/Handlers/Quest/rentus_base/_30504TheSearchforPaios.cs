using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Pad
/// </summary>
public class _30504TheSearchforPaios : AbstractQuestHandler
{
    private static readonly int[] npcIds = { 205438, 701098, 799536 };

    public _30504TheSearchforPaios() : base(30504)
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
                if (dialogActionId == DialogAction.USE_OBJECT)
                    return UseQuestObject(env, 0, 1, false, false);
            }
            else if (targetId == npcIds[2])
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1352);
                else if (dialogActionId == DialogAction.SET_SUCCEED)
                    return DefaultCloseDialog(env, 1, 1, true, false);
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == npcIds[0])
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
                    return SendQuestDialog(env, 5);
                else
                    return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
