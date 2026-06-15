using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Handlers.Quest;

public class _30158JotunJunket : AbstractQuestHandler
{
    public _30158JotunJunket() : base(30158)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(799383).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(799383).AddOnTalkEvent(questId);
        qe.RegisterOnEnterZone(ZoneName.Get("UNKNOWN_LANDS_600010000"), questId);
    }

    public override bool OnEnterZoneEvent(QuestEnv env, ZoneName zoneName)
    {
        if (zoneName != ZoneName.Get("UNKNOWN_LANDS_600010000"))
            return false;
        Player player = env.GetPlayer();
        if (player == null)
            return false;
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null || qs.GetQuestVars().GetQuestVars() != 0)
            return false;
        if (qs.GetStatus() != QuestStatus.START)
            return false;
        env.SetQuestId(questId);
        qs.SetStatus(QuestStatus.REWARD);
        UpdateQuestStatus(env);
        return true;
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = env.GetTargetId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();

        if (targetId == 799383)
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
