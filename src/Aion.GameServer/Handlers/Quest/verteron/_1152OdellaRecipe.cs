using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// Quest starter: Nemia (203132). Give the Odella (182200526) (1) to Eradis (203130) and ask him to cook it. Buy some Verteron Pepper (169400112) for
/// Eradis.
///
/// @author vlog
/// </summary>
public class _1152OdellaRecipe : AbstractQuestHandler
{
    public _1152OdellaRecipe() : base(1152)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203132).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(203132).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203130).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 203132)
            { // Nemia
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 1011);
                }
                else
                {
                    return SendQuestStartDialog(env, 182200526, 1);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            if (targetId == 203130)
            { // Eradis
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 0)
                        {
                            return SendQuestDialog(env, 1352);
                        }
                        else if (var == 1)
                        {
                            return SendQuestDialog(env, 2375);
                        }
                        return false;
                    case DialogAction.SETPRO1:
                        return DefaultCloseDialog(env, 0, 1, 0, 0, 182200526, 1); // 1
                    case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                        return CheckQuestItems(env, 1, 1, true, 5, 2716); // reward
                    case DialogAction.FINISH_DIALOG:
                        return SendQuestSelectionDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203130)
            { // Eradis
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
