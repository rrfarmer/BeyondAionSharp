using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @Author Majka
/// </summary>
public class _25062OminousAdvice : AbstractQuestHandler
{
    public _25062OminousAdvice() : base(25062)
    {
    }

    public override void Register()
    {
        // Runlil 804917
        // Sorg 804918
        qe.RegisterQuestNpc(804917).AddOnQuestStart(questId);
        int[] npcs = { 804917, 804918 };
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
            if (targetId == 804917) // Runlil
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
                case 804917: // Runlil
                    if (var == 0)
                    {
                        if (dialogActionId == DialogAction.QUEST_SELECT)
                            return SendQuestDialog(env, 1011);

                        if (dialogActionId == DialogAction.CHECK_USER_HAS_QUEST_ITEM)
                        {
                            if (QuestService.CollectItemCheck(env, true))
                            {
                                qs.SetStatus(QuestStatus.REWARD);
                                qs.SetQuestVar(var + 1);
                                UpdateQuestStatus(env);
                                return SendQuestDialog(env, 10000);
                            }
                            else
                            {
                                return SendQuestDialog(env, 10001);
                            }
                        }
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            switch (targetId)
            {
                case 804918: // Sorg
                    if (dialogActionId == DialogAction.USE_OBJECT)
                        return SendQuestDialog(env, 10002);

                    return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
