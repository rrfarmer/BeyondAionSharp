using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _1336ScoutingForDemokritos : AbstractQuestHandler
{
    public _1336ScoutingForDemokritos() : base(1336)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204006).AddOnQuestStart(questId); // Demokritos
        qe.RegisterQuestNpc(204006).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(206020).AddOnAtDistanceEvent(questId);
        qe.RegisterQuestNpc(206021).AddOnAtDistanceEvent(questId);
        qe.RegisterQuestNpc(206022).AddOnAtDistanceEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        if (env.GetVisibleObject() is Npc npc)
            targetId = npc.GetNpcId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 204006) // Demokritos
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 1011);
                }
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 204006)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 1352);
                }
                else
                    return SendQuestEndDialog(env);
            }
        }
        return false;
    }

    public override bool OnAtDistanceEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        if (env.GetVisibleObject() is Npc npc)
            targetId = npc.GetNpcId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            if (targetId == 206020 && var == 0)
            {
                PlayQuestMovie(env, 43);
                ChangeQuestStep(env, 0, 16);
                return true;
            }
            else if (targetId == 206021 && var == 16)
            {
                PlayQuestMovie(env, 44);
                ChangeQuestStep(env, 16, 48);
                return true;
            }
            else if (targetId == 206022 && var == 48)
            {
                PlayQuestMovie(env, 45);
                ChangeQuestStep(env, 48, 48, true);
                return true;
            }
        }
        return false;
    }
}
