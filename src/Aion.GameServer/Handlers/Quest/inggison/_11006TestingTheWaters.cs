using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Cheatkiller
/// </summary>
public class _11006TestingTheWaters : AbstractQuestHandler
{
    public _11006TestingTheWaters() : base(11006)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestItem(182206704, questId);
        qe.RegisterQuestItem(182206705, questId);
        qe.RegisterQuestNpc(798940).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(798940).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 798940)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 4762);
                }
                else
                {
                    return SendQuestStartDialog(env, 182206704, 1);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 798940)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (qs.GetQuestVarById(0) == 1)
                        return SendQuestDialog(env, 1352);
                }
                else if (dialogActionId == DialogAction.SETPRO2)
                {
                    GiveQuestItem(env, 182206705, 1);
                    RemoveQuestItem(env, 182206706, 1);
                    return DefaultCloseDialog(env, 1, 2);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 798940)
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
                {
                    return SendQuestDialog(env, 10002);
                }
                RemoveQuestItem(env, 182206707, 1);
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }

    public override HandlerResult OnItemUseEvent(QuestEnv env, Item item)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            if (item.GetItemTemplate().GetTemplateId() == 182206704)
            {
                return HandlerResultExtensions.FromBoolean(UseQuestItem(env, item, 0, 1, false, 182206706, 1));
            }
            else if (item.GetItemTemplate().GetTemplateId() == 182206705)
            {
                return HandlerResultExtensions.FromBoolean(UseQuestItem(env, item, 2, 2, true, 182206707, 1));
            }
        }
        return HandlerResult.FAILED;
    }
}
