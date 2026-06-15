using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Enomine, Artur
/// </summary>
public class _80320ForTheAsmodians : AbstractQuestHandler
{
    private static readonly int[] npc_ids = { 831427 };

    public _80320ForTheAsmodians() : base(80320)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(831427).AddOnQuestStart(questId);
        foreach (int npc_id in npc_ids)
            qe.RegisterQuestNpc(npc_id).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int targetId = env.GetTargetId();
        int dialogActionId = env.GetDialogActionId();
        if (qs == null || qs.IsStartable())
        {
            if (targetId == 831427)
            {
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

        if (qs == null)
            return false;

        else if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 831427:
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            return SendQuestDialog(env, 1011);
                        case DialogAction.ASK_QUEST_ACCEPT:
                            return SendQuestDialog(env, 4);
                        case DialogAction.QUEST_ACCEPT_1:
                            qs.SetQuestVar(1);
                            UpdateQuestStatus(env);
                            return CloseDialogWindow(env);
                    }
                    break;
            }
            if (player.GetInventory().GetItemCountByItemId(182215303) > 11)
            {
                qs.SetStatus(QuestStatus.REWARD);
                UpdateQuestStatus(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 831427)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 2375);
                    case DialogAction.SELECT_QUEST_REWARD:
                        return SendQuestDialog(env, 5);
                    default:
                        return SendQuestEndDialog(env);
                }
            }
        }
        return false;
    }
}
