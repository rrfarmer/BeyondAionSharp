using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Cheatkiller
/// </summary>
public class _3205FortheBlackCloudTraders : AbstractQuestHandler
{
    private static readonly int[] npcIds = { 279010, 203735, 798321 };
    private static readonly int mobId = 219024;

    public _3205FortheBlackCloudTraders() : base(3205)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(npcIds[0]).AddOnQuestStart(questId);
        foreach (int npcId in npcIds)
            qe.RegisterQuestNpc(npcId).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(mobId).AddOnKillEvent(questId);
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
            if (targetId == npcIds[0])
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 1352);
                    case DialogAction.SETPRO2:
                        return DefaultCloseDialog(env, 15, 16);
                }
            }
            else if (targetId == npcIds[1])
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 1693);
                    case DialogAction.SET_SUCCEED:
                        return DefaultCloseDialog(env, 16, 16, true, false);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == npcIds[2])
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

    public override bool OnKillEvent(QuestEnv env)
    {
        return DefaultOnKillEvent(env, mobId, 0, 15);
    }
}
