using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author HellBoy, vlog
/// </summary>
public class _21073ListentoMySongStrigiks : AbstractQuestHandler
{
    public _21073ListentoMySongStrigiks() : base(21073)
    {
    }

    public override void Register()
    {
        int[] npcs = { 799407, 799408 };
        foreach (int npc in npcs)
        {
            qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
        }
        qe.RegisterQuestNpc(799407).AddOnQuestStart(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 799407) // Skilving
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            if (targetId == 799408) // Svasuth
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (var == 0)
                    {
                        return SendQuestDialog(env, 1352);
                    }
                }
                else if (dialogActionId == DialogAction.SETPRO1)
                {
                    return DefaultCloseDialog(env, 0, 1); // 1
                }
            }
            else if (targetId == 799407) // Skilving
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (var == 1)
                    {
                        return SendQuestDialog(env, 2375);
                    }
                }
                else if (dialogActionId == DialogAction.SELECT_QUEST_REWARD)
                {
                    ChangeQuestStep(env, 1, 1, true); // reward
                    return SendQuestDialog(env, 5);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 799407) // Skilving
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
