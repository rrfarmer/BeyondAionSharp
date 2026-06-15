using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Ritsu
/// </summary>
public class _24025CrisisinMorheim : AbstractQuestHandler
{
    public _24025CrisisinMorheim() : base(24025)
    {
    }

    public override void Register()
    {
        int[] npc_ids = { 204388, 204414, 204304, 204345 };
        qe.RegisterOnQuestCompleted(questId);
        qe.RegisterOnLevelChanged(questId);
        foreach (int npc_id in npc_ids)
            qe.RegisterQuestNpc(npc_id).AddOnTalkEvent(questId);
    }

    public override void OnQuestCompletedEvent(QuestEnv env)
    {
        DefaultOnQuestCompletedEvent(env, 24020);
    }

    public override void OnLevelChangedEvent(Player player)
    {
        DefaultOnLevelChangedEvent(player, 24020);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null)
            return false;

        int var = qs.GetQuestVarById(0);
        int targetId = env.GetTargetId();
        int dialogActionId = env.GetDialogActionId();

        if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 204388:
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 0)
                                return SendQuestDialog(env, 1011);
                            else if (var == 2)
                                return SendQuestDialog(env, 1693);
                            return false;
                        case DialogAction.SETPRO1:
                            if (var == 0)
                            {
                                return DefaultCloseDialog(env, 0, 1); // 1
                            }
                            return false;
                        case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                            return CheckQuestItems(env, 2, 3, false, 10000, 10001);
                    }
                    break;
                case 204345:
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 3)
                                return SendQuestDialog(env, 2034);
                            return false;
                        case DialogAction.SET_SUCCEED:
                            if (var == 3)
                            {
                                return DefaultCloseDialog(env, 3, 3, true, false);
                            }
                            break;
                    }
                    break;
                case 204414:
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 1)
                                return SendQuestDialog(env, 1352);
                            return false;
                        case DialogAction.SELECT2_1_1:
                            PlayQuestMovie(env, 85);
                            break;
                        case DialogAction.SETPRO2:
                            if (var == 1)
                            {
                                return DefaultCloseDialog(env, 1, 2); // 2
                            }
                            break;
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 204304)
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
                    return SendQuestDialog(env, 10002);
                else
                {
                    RemoveQuestItem(env, 182215370, 1);
                    return SendQuestEndDialog(env);
                }
            }
        }
        return false;
    }
}
