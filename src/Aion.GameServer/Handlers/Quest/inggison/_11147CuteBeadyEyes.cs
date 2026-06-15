using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Cheatkiller
/// </summary>
public class _11147CuteBeadyEyes : AbstractQuestHandler
{
    public _11147CuteBeadyEyes() : base(11147)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798997).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(798997).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(799079).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(799081).AddOnTalkEvent(questId);
        qe.RegisterOnEnterZone(ZoneName.Get("KLAWNICKTS_CAVE_210050000"), questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 798997)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 4762);
                }
                else
                {
                    return SendQuestStartDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 798997)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (qs.GetQuestVarById(0) == 0)
                        return SendQuestDialog(env, 1011);
                    else if (qs.GetQuestVarById(0) == 1)
                        return SendQuestDialog(env, 1352);
                }
                else if (dialogActionId == DialogAction.SETPRO1)
                {
                    SpawnForFiveMinutesInFrontOf(799079, player, 1);
                    SpawnForFiveMinutesInFrontOf(799081, player, -1);
                    return DefaultCloseDialog(env, 0, 1);
                }
                else if (dialogActionId == DialogAction.SETPRO2)
                {
                    return DefaultCloseDialog(env, 1, 2);
                }
            }
            else if (targetId == 799079)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 1693);
                }
                else if (dialogActionId == DialogAction.SETPRO3)
                {
                    Npc npc = (Npc)env.GetVisibleObject();
                    npc.GetController().Delete();
                    return DefaultCloseDialog(env, 2, 3);
                }
            }
            else if (targetId == 799081)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 2034);
                }
                else if (dialogActionId == DialogAction.SETPRO4)
                {
                    Npc npc = (Npc)env.GetVisibleObject();
                    npc.GetController().Delete();
                    return DefaultCloseDialog(env, 3, 4);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 798997)
            {
                switch (dialogActionId)
                {
                    case DialogAction.USE_OBJECT:
                        return SendQuestDialog(env, 10002);
                    default:
                    {
                        return SendQuestEndDialog(env);
                    }
                }
            }
        }
        return false;
    }

    public override bool OnEnterZoneEvent(QuestEnv env, ZoneName zoneName)
    {
        if (zoneName == ZoneName.Get("KLAWNICKTS_CAVE_210050000"))
        {
            Player player = env.GetPlayer();
            if (player == null)
                return false;
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs != null && qs.GetStatus() == QuestStatus.START)
            {
                int var = qs.GetQuestVarById(0);
                if (var == 4)
                {
                    ChangeQuestStep(env, 4, 4, true);
                    return true;
                }
            }
        }
        return false;
    }
}
