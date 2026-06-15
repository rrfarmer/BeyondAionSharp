using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Handlers.Quest;

public class _30057JotunJaunt : AbstractQuestHandler
{
    public _30057JotunJaunt() : base(30057)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(799381).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(799381).AddOnTalkEvent(questId);
        qe.RegisterOnEnterZone(ZoneName.Get("SILENTERA_WESTGATE_600010000"), questId);
    }

    public override bool OnEnterZoneEvent(QuestEnv env, ZoneName zoneName)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            if (var == 0)
            {
                ChangeQuestStep(env, 0, 1, true);
                return true;
            }
        }
        return false;
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = env.GetTargetId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();

        if (targetId == 799381)
        {
            if (qs == null || qs.IsStartable())
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 4762);
                else
                    return SendQuestStartDialog(env);
            }
            else if (qs.GetStatus() == QuestStatus.REWARD)
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
                    return SendQuestDialog(env, 10002);
                else if (dialogActionId == DialogAction.SELECT_QUEST_REWARD)
                    return SendQuestDialog(env, 5);
                else
                    return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
