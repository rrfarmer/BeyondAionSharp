using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Cheatkiller
/// </summary>
public class _4712EscapeFromTheDredgion : AbstractQuestHandler
{
    public _4712EscapeFromTheDredgion() : base(4712)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(279042).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(279042).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798327).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798328).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798329).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798330).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 279042)
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
            if (targetId == 798327 || targetId == 798328 || targetId == 798329 || targetId == 798330)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (qs.GetQuestVarById(0) == 0)
                    {
                        return SendQuestDialog(env, 1011);
                    }
                }
                else if (dialogActionId == DialogAction.SETPRO1)
                {
                    Npc npc = (Npc)env.GetVisibleObject();
                    npc.GetController().Delete();
                    return DefaultCloseDialog(env, 0, 1, true, false);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 279042)
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
        return DefaultOnKillEvent(env, 214823, 2, true);
    }
}
