using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Mr. Poke, Nephis and quest helper team, vlog
/// </summary>
public class _2232TheBrokenHoneyJar : AbstractQuestHandler
{
    public _2232TheBrokenHoneyJar() : base(2232)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203613).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(203613).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203622).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(700061).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = env.GetTargetId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 203613)
            { // Gilungk
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
            int var = qs.GetQuestVarById(0);
            if (targetId == 203613)
            { // Gilungk
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 1)
                        {
                            return SendQuestDialog(env, 2375);
                        }
                        return false;
                    case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                        return CheckQuestItems(env, 1, 1, true, 5, 2716); // reward
                    case DialogAction.FINISH_DIALOG:
                        return SendQuestSelectionDialog(env);
                }
            }
            else if (targetId == 700061)
            { // Beehive
                if (dialogActionId == DialogAction.USE_OBJECT)
                {
                    return true; // loot
                }
            }
            else if (targetId == 203622)
            { // Tatural
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (var == 0)
                    {
                        return SendQuestDialog(env, 1352);
                    }
                }
                else if (env.GetDialogActionId() == DialogAction.SETPRO1)
                {
                    return DefaultCloseDialog(env, 0, 1); // 1
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203613)
            { // Gilungk
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
