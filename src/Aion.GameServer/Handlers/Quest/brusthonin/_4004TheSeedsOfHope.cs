using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _4004TheSeedsOfHope : AbstractQuestHandler
{
    public _4004TheSeedsOfHope() : base(4004)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(205128).AddOnQuestStart(questId); // Randet
        qe.RegisterQuestNpc(205128).AddOnTalkEvent(questId); // Randet
        qe.RegisterQuestNpc(700340).AddOnTalkEvent(questId); // Earth Mound
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        if (env.GetVisibleObject() is Npc npc)
            targetId = npc.GetNpcId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 205128) // Randet
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 4762);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 205128)
            {
                if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                    return SendQuestDialog(env, 10002);
                else
                    return SendQuestEndDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            switch (targetId)
            {
                case 700340: // Earth Mound
                    if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                    {
                        if (var < 4)
                        {
                            return UseQuestObject(env, var, var + 1, false, true);
                        }
                        else if (var == 4)
                        {
                            return UseQuestObject(env, 4, 4, true, true); // reward
                        }
                    }
                    break;
            }
        }
        return false;
    }
}
