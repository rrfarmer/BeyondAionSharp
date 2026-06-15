using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Ritsu
/// </summary>
public class _28410FortressUnsecured : AbstractQuestHandler
{
    public _28410FortressUnsecured() : base(28410)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(799587).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(799587).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(799563).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(799558).AddOnTalkEvent(questId);
        qe.RegisterOnEnterZone(ZoneName.Get("DRANA_PRODUCTION_LAB_300250000"), questId);
    }

    public override bool OnEnterZoneEvent(QuestEnv env, ZoneName zoneName)
    {
        Player player = env.GetPlayer();
        if (player == null)
            return false;
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (zoneName != ZoneName.Get("DRANA_PRODUCTION_LAB_300250000"))
            return false;
        if (qs == null || qs.GetQuestVars().GetQuestVars() != 1)
            return false;
        if (qs.GetStatus() != QuestStatus.START)
            return false;
        qs.SetStatus(QuestStatus.REWARD);
        UpdateQuestStatus(env);
        return true;
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = env.GetTargetId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        if (targetId == 799587)
        {
            if (qs == null || qs.IsStartable())
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 4762);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (targetId == 799563)
        {
            if (qs != null && qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 0)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else if (env.GetDialogActionId() == DialogAction.SETPRO1)
                    return DefaultCloseDialog(env, 0, 1);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (targetId == 799558)
        {
            if (qs != null && qs.GetStatus() == QuestStatus.REWARD)
            {
                if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                    return SendQuestDialog(env, 10002);
                else if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD)
                    return SendQuestDialog(env, 5);
                else
                    return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
