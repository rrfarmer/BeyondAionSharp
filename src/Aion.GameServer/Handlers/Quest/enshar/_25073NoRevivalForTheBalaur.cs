using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @Author Majka
/// </summary>
public class _25073NoRevivalForTheBalaur : AbstractQuestHandler
{
    public _25073NoRevivalForTheBalaur() : base(25073)
    {
    }

    public override void Register()
    {
        // Drak tribe's heart 731556
        // Sorg 804918
        // Cenute 804732
        qe.RegisterQuestNpc(804918).AddOnQuestStart(questId);
        int[] npcs = { 731556, 804918, 804732 };
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
            if (targetId == 804918) // Sorg
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
                case 731556: // Drak tribe's heart
                    if (var == 0)
                    {
                        if (dialogActionId == DialogAction.QUEST_SELECT)
                        {
                            return SendQuestDialog(env, 1011);
                        }

                        if (dialogActionId == DialogAction.SET_SUCCEED)
                        {
                            if (QuestService.CollectItemCheck(env, true))
                            {
                                qs.SetQuestVar(var + 1);
                                return DefaultCloseDialog(env, var + 1, var + 1, true, false);
                            }
                            return CloseDialogWindow(env);
                        }
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            switch (targetId)
            {
                case 804732: // Cenute
                    if (dialogActionId == DialogAction.USE_OBJECT)
                        return SendQuestDialog(env, 10002);

                    return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
