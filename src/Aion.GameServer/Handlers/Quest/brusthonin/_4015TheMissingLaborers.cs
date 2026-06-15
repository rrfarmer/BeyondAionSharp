using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _4015TheMissingLaborers : AbstractQuestHandler
{
    public _4015TheMissingLaborers() : base(4015)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(205130).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(205130).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(730107).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(205130).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        if (env.GetVisibleObject() is Npc npc)
            targetId = npc.GetNpcId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null)
        {
            if (targetId == 205130)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 730107:
                    if (qs.GetQuestVarById(0) == 0 && env.GetDialogActionId() == DialogAction.USE_OBJECT)
                    {
                        return UseQuestObject(env, 0, 1, false, false); // 1
                    }
                    return false;
                case 205130:
                    if (qs.GetQuestVarById(0) == 1)
                    {
                        if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                            return SendQuestDialog(env, 2375);
                        else if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD)
                        {
                            qs.SetStatus(QuestStatus.REWARD);
                            UpdateQuestStatus(env);
                            return SendQuestEndDialog(env);
                        }
                        else
                            return SendQuestEndDialog(env);
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 205130)
                return SendQuestEndDialog(env);
        }
        return false;
    }
}
