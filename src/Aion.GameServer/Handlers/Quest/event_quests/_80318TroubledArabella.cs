using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Enomine, Artur
/// </summary>
public class _80318TroubledArabella : AbstractQuestHandler
{
    private static readonly int[] npc_ids = { 831425, 831426 };

    public _80318TroubledArabella() : base(80318)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(831425).AddOnQuestStart(questId);
        qe.RegisterQuestItem(182215301, questId);
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
            if (targetId == 831425)
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
        if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 831425: // Arabella
                    switch (dialogActionId)
                    {
                        case DialogAction.USE_OBJECT:
                            return SendQuestDialog(env, 1011);
                        case DialogAction.ASK_QUEST_ACCEPT:
                            return SendQuestDialog(env, 4);
                        case DialogAction.QUEST_ACCEPT_1:
                            if (!GiveQuestItem(env, 182215301, 1))
                                return true;
                            qs.SetStatus(QuestStatus.REWARD);
                            UpdateQuestStatus(env);
                            return CloseDialogWindow(env);
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 831426) // ziba
            {
                switch (dialogActionId)
                {
                    case DialogAction.USE_OBJECT:
                        if (player.GetInventory().GetItemCountByItemId(182215301) == 1)
                        {
                            RemoveQuestItem(env, 182215301, 1);
                            return SendQuestDialog(env, 1352);
                        }
                        return false;
                    case DialogAction.SELECT2_1:
                        return SendQuestDialog(env, 5);
                    default:
                        return SendQuestEndDialog(env);
                }
            }
        }
        return false;
    }
}
