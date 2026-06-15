using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @Author Majka
/// </summary>
public class _25022SoupDeCure : AbstractQuestHandler
{
    public _25022SoupDeCure() : base(25022)
    {
    }

    public override void Register()
    {
        // Swith 804908
        // Kronag 804724
        qe.RegisterQuestNpc(804908).AddOnQuestStart(questId);
        int[] npcs = { 804908, 804724 };
        foreach (int npc in npcs)
        {
            qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
        }
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int targetId = env.GetTargetId();
        int dialogActionId = env.GetDialogActionId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 804908) // Swith
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 4762);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);

            switch (targetId)
            {
                case 804908: // Swith
                    if (var == 0)
                    {
                        if (dialogActionId == DialogAction.QUEST_SELECT)
                            return SendQuestDialog(env, 1011);

                        if (dialogActionId == DialogAction.CHECK_USER_HAS_QUEST_ITEM)
                            return CheckQuestItems(env, var, var + 1, false, 10000, 10001, 182215709, 1);
                    }
                    if (var == 1)
                    {
                        if (dialogActionId == DialogAction.QUEST_SELECT)
                            return SendQuestDialog(env, 1352);

                        if (dialogActionId == DialogAction.SET_SUCCEED)
                        {
                            qs.SetQuestVar(var + 1);
                            return DefaultCloseDialog(env, var + 1, var + 1, true, false, 0, 0, 182215709, 1);
                        }
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            switch (targetId)
            {
                case 804724: // Kronag
                    if (dialogActionId == DialogAction.USE_OBJECT)
                        return SendQuestDialog(env, 10002);

                    return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
