using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _1537FishOnTheLine : AbstractQuestHandler
{
    public _1537FishOnTheLine() : base(1537)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204588).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(204588).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(730189).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(730190).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(730191).AddOnTalkEvent(questId);
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
            if (targetId == 204588)
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
                case 730189:
                    if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                    {
                        return UseQuestObject(env, 0, 1, false, 0); // 1
                    }
                    break;
                case 730190:
                    if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                    {
                        return UseQuestObject(env, 1, 2, false, 0); // 2
                    }
                    break;
                case 730191:
                    if (qs.GetQuestVarById(0) == 2 && env.GetDialogActionId() == DialogAction.USE_OBJECT)
                    {
                        qs.SetQuestVarById(0, 3);
                        qs.SetStatus(QuestStatus.REWARD);
                        UpdateQuestStatus(env);
                        return true;
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 204588)
                return SendQuestEndDialog(env);
        }
        return false;
    }
}
