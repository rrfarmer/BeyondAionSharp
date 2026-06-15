using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Cheatkiller
/// </summary>
public class _21080MessageInAWindstream : AbstractQuestHandler
{
    public _21080MessageInAWindstream() : base(21080)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(799231).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(799231).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(799427).AddOnTalkEvent(questId);
        qe.RegisterOnEnterZone(ZoneName.Get("ANTAGOR_CANYON_220070000"), questId);
        qe.RegisterOnEnterZone(ZoneName.Get("GELKMAROS_FORTRESS_220070000"), questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 799231)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 4762);
                else
                    return SendQuestStartDialog(env, 182207939, 1);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 799427)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (qs.GetQuestVarById(0) == 3)
                        return SendQuestDialog(env, 2034);
                }
                else if (dialogActionId == DialogAction.SETPRO4)
                {
                    RemoveQuestItem(env, 182207939, 1);
                    return DefaultCloseDialog(env, 3, 4);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 799231)
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
                    return SendQuestDialog(env, 10002);
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }

    public override bool OnEnterZoneEvent(QuestEnv env, ZoneName zoneName)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            if (zoneName == ZoneName.Get("ANTAGOR_CANYON_220070000"))
            {
                if (var < 3)
                {
                    ChangeQuestStep(env, var, var + 1);
                    return true;
                }
            }
            else if (zoneName == ZoneName.Get("GELKMAROS_FORTRESS_220070000") && var == 4)
            {
                ChangeQuestStep(env, 4, 4, true);
                return true;
            }
        }
        return false;
    }
}
