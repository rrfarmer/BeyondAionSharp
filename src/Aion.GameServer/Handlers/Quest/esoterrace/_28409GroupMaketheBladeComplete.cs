using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Ritsu
/// </summary>
public class _28409GroupMaketheBladeComplete : AbstractQuestHandler
{
    public _28409GroupMaketheBladeComplete() : base(28409)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(799558).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(799558).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(799557).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(205237).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(215795).AddOnKillEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = env.GetTargetId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        if (targetId == 799558)
        {
            if (qs == null || qs.IsStartable())
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 4762);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (targetId == 799557)
        {
            if (qs != null && qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 0)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else if (env.GetDialogActionId() == DialogAction.SETPRO1)
                    return DefaultCloseDialog(env, 0, 1);
                else
                    return SendQuestStartDialog(env);
            }
            else if (qs != null && qs.GetStatus() == QuestStatus.REWARD)
            {
                if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                    return SendQuestDialog(env, 10002);
                else if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD)
                    return SendQuestDialog(env, 5);
                else
                    return SendQuestEndDialog(env);
            }
        }
        else if (targetId == 205237)
        {
            if (qs != null && qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 1)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1352);
                else if (env.GetDialogActionId() == DialogAction.SETPRO2)
                    return DefaultCloseDialog(env, 1, 2, 182215019, 1, 182215018, 1);
                else
                    return SendQuestStartDialog(env);
            }
        }
        return false;
    }

    public override bool OnKillEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null || qs.GetStatus() != QuestStatus.START)
            return false;

        int targetId = env.GetTargetId();

        switch (targetId)
        {
            case 215795:
                if (qs.GetQuestVarById(0) == 2)
                {
                    GiveQuestItem(env, 182215020, 1);
                    player.GetInventory().DecreaseByItemId(182215019, 1);
                    qs.SetStatus(QuestStatus.REWARD);
                    UpdateQuestStatus(env);
                }
                break;
        }
        return false;
    }
}
