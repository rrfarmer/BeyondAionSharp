using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Mr. Poke, vlog
/// </summary>
public class _2221ManirsUncle : AbstractQuestHandler
{
    public _2221ManirsUncle() : base(2221)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203607).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(203607).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203608).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(700214).AddOnTalkEvent(questId);
        qe.RegisterOnGetItem(182203215, questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = env.GetTargetId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 203607)
            { // Manir
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
            switch (targetId)
            {
                case 203608: // Groken
                    if (dialogActionId == DialogAction.QUEST_SELECT)
                    {
                        if (var == 0)
                        {
                            return SendQuestDialog(env, 1352);
                        }
                        else if (var == 2)
                        {
                            return SendQuestDialog(env, 2375);
                        }
                    }
                    else if (dialogActionId == DialogAction.SETPRO1)
                    {
                        return DefaultCloseDialog(env, 0, 1); // 1
                    }
                    else if (dialogActionId == DialogAction.SELECT_QUEST_REWARD)
                    {
                        RemoveQuestItem(env, 182203215, 1);
                        ChangeQuestStep(env, 2, 2, true); // reward
                        return SendQuestDialog(env, 5);
                    }
                    break;
                case 700214: // Groken's Safe
                    if (dialogActionId == DialogAction.USE_OBJECT)
                    {
                        if (var == 1)
                        {
                            return SendQuestDialog(env, 1693);
                        }
                    }
                    else if (dialogActionId == DialogAction.SETPRO2)
                    {
                        return CloseDialogWindow(env);
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203608)
            { // Groken
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }

    public override bool OnGetItemEvent(QuestEnv env)
    {
        return DefaultOnGetItemEvent(env, 1, 2, false); // 2
    }
}
