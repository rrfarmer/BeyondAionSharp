using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Handlers.Quest;

public class _16987SeekOuttheCorridor : AbstractQuestHandler
{
    private static readonly int npcId = 804865;

    public _16987SeekOuttheCorridor() : base(16987)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(npcId).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(npcId).AddOnTalkEvent(questId);
        qe.RegisterOnEnterZone(ZoneName.Get("DANUAR_SANCTUARY_INVESTIGATION_AREA_220080000"), questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == npcId)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 4762);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == npcId)
                return SendQuestEndDialog(env);
        }
        return false;
    }

    public override bool OnEnterZoneEvent(QuestEnv env, ZoneName zoneName)
    {
        QuestState qs = env.GetPlayer().GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            qs.SetQuestVarById(0, 1);
            qs.SetStatus(QuestStatus.REWARD);
            UpdateQuestStatus(env);
            return true;
        }
        return false;
    }
}
