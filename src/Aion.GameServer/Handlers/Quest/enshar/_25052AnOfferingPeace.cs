using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @Author Majka
/// </summary>
public class _25052AnOfferingPeace : AbstractQuestHandler
{
    public _25052AnOfferingPeace() : base(25052)
    {
    }

    public override void Register()
    {
        // Sea Jotun's treasure 731561
        // Redelf 804913
        // Soglo 804915
        qe.RegisterQuestNpc(804913).AddOnQuestStart(questId);
        int[] npcs = { 731561, 804913, 804915 };
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
            if (targetId == 804913) // Redelf
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
                case 731561: // Sea Jotun's treasure
                    if (var == 0)
                    {
                        if (dialogActionId == DialogAction.QUEST_SELECT)
                            return SendQuestDialog(env, 1011);

                        if (dialogActionId == DialogAction.SET_SUCCEED)
                        {
                            SpawnForFiveMinutes(220032, env.GetVisibleObject().GetPosition(), (byte) 10);
                            GiveQuestItem(env, 182215721, 1);
                            qs.SetQuestVar(var + 1);
                            return DefaultCloseDialog(env, var + 1, var + 1, true, false);
                        }
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            switch (targetId)
            {
                case 804915: // Soglo
                    if (dialogActionId == DialogAction.USE_OBJECT)
                        return SendQuestDialog(env, 10002);

                    RemoveQuestItem(env, 182215721, 1);
                    return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
