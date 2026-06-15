using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Cheatkiller
/// </summary>
public class _11063QuellMastarius : AbstractQuestHandler
{
    public _11063QuellMastarius() : base(11063)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestItem(182206849, questId);
        qe.RegisterQuestNpc(799049).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(258220).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(798926).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 0)
            {
                if (dialogActionId == DialogAction.QUEST_ACCEPT_1)
                {
                    QuestService.StartQuest(env);
                    return CloseDialogWindow(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 799049)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (qs.GetQuestVarById(0) == 0)
                        return SendQuestDialog(env, 1011);
                    else if (qs.GetQuestVarById(0) == 2)
                        return SendQuestDialog(env, 1693);
                }
                else if (dialogActionId == DialogAction.SETPRO1)
                {
                    return DefaultCloseDialog(env, 0, 1);
                }
                else if (dialogActionId == DialogAction.SET_SUCCEED)
                {
                    GiveQuestItem(env, 182206850, 1);
                    return DefaultCloseDialog(env, 2, 3, true, false);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 798926)
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
                    return SendQuestDialog(env, 10002);
                RemoveQuestItem(env, 182206849, 1);
                RemoveQuestItem(env, 182206850, 1);
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }

    public override bool OnKillEvent(QuestEnv env)
    {
        return DefaultOnKillEvent(env, 258220, 1, 2);
    }

    public override HandlerResult OnItemUseEvent(QuestEnv env, Item item)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null || qs.IsStartable())
        {
            return HandlerResultExtensions.FromBoolean(SendQuestDialog(env, 4));
        }
        return HandlerResult.FAILED;
    }
}
