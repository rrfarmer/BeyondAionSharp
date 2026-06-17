using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.QuestEngine.Task;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Cheatkiller
/// </summary>
public class _3212TheMissingCubeCraftsman : AbstractQuestHandler
{
    public _3212TheMissingCubeCraftsman() : base(3212)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798321).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(203838).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798011).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798337).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(730208).AddOnTalkEvent(questId);
        qe.RegisterAddOnReachTargetEvent(questId);
        qe.RegisterAddOnLostTargetEvent(questId);
        qe.RegisterOnDie(questId);
        qe.RegisterOnLogOut(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 798321)
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
            if (targetId == 203838)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (qs.GetQuestVarById(0) == 0)
                    {
                        return SendQuestDialog(env, 1011);
                    }
                }
                else if (dialogActionId == DialogAction.SETPRO1)
                {
                    return DefaultCloseDialog(env, 0, 1);
                }
            }
            else if (targetId == 798011)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (qs.GetQuestVarById(0) == 1)
                        return SendQuestDialog(env, 1352);
                }
                else if (dialogActionId == DialogAction.SETPRO2)
                {
                    return DefaultCloseDialog(env, 1, 2);
                }
            }
            else if (targetId == 798337)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (qs.GetQuestVarById(0) == 2)
                        return SendQuestDialog(env, 1693);
                }
                else if (dialogActionId == DialogAction.SETPRO3)
                {
                    Npc npc = (Npc) env.GetVisibleObject();
                    npc.GetSpawn().SetWalkerId("4212");
                    WalkManager.StartWalking((NpcAI) npc.GetAi());
                    PacketSendUtility.BroadcastPacket(npc, new SM_EMOTION(npc, EmotionType.CHANGE_SPEED, 0, npc.GetObjectId()));
                    player.GetController().AddTask(TaskId.QUEST_FOLLOW, QuestTasks.NewFollowingToTargetCheckTask(env, npc, 505.69427f, 437.69382f, 885.1844f));
                    return DefaultCloseDialog(env, 2, 3);
                }
            }
            else if (targetId == 730208)
            {
                Npc npc = (Npc) env.GetVisibleObject();
                npc.GetController().Delete();
                return true;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 798011)
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
                {
                    return SendQuestDialog(env, 10002);
                }
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }

    public override bool OnDieEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            if (var == 3)
            {
                qs.SetQuestVar(2);
                UpdateQuestStatus(env);
                return true;
            }
        }
        return false;
    }

    public override bool OnLogOutEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            if (var == 3)
            {
                qs.SetQuestVar(2);
                UpdateQuestStatus(env);
            }
        }
        return false;
    }

    public override bool OnNpcReachTargetEvent(QuestEnv env)
    {
        return DefaultFollowEndEvent(env, 3, 4, true); // reward
    }

    public override bool OnNpcLostTargetEvent(QuestEnv env)
    {
        return DefaultFollowEndEvent(env, 3, 2, false);
    }
}
