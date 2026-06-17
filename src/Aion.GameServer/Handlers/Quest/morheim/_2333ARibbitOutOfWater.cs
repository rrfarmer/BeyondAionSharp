using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.QuestEngine.Task;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Cheatkiller
/// </summary>
public class _2333ARibbitOutOfWater : AbstractQuestHandler
{
    public _2333ARibbitOutOfWater() : base(2333)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798084).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(798084).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(701147).AddOnTalkEvent(questId);
        qe.RegisterQuestItem(182204130, questId);
        qe.RegisterAddOnLostTargetEvent(questId);
        qe.RegisterAddOnReachTargetEvent(questId);
        qe.RegisterOnDie(questId);
        qe.RegisterOnLogOut(questId);
        qe.AddHandlerSideQuestDrop(questId, 212420, 182204130, 1, 100);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 798084)
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
            int var = qs.GetQuestVarById(0);
            if (targetId == 798084)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (var == 0)
                        return SendQuestDialog(env, 1011);
                    else if (var == 1)
                        return SendQuestDialog(env, 1352);
                }
                else if (dialogActionId == DialogAction.CHECK_USER_HAS_QUEST_ITEM)
                {
                    return CheckQuestItems(env, 0, 1, false, 10000, 10001);
                }
                else if (dialogActionId == DialogAction.SETPRO2)
                {
                    Npc debrie = (Npc) SpawnInFrontOf(204416, player);
                    WalkManager.StartWalking((NpcAI) debrie.GetAi());
                    debrie.GetAi().OnCreatureEvent(AiEventType.FOLLOW_ME, player);
                    PacketSendUtility.BroadcastPacket(debrie, new SmEmotion(debrie, EmotionType.CHANGE_SPEED, 0, debrie.GetObjectId()));
                    player.GetController().AddTask(TaskId.QUEST_FOLLOW,
                        QuestTasks.NewFollowingToTargetCheckTask(env, debrie, ZoneName.Get("DF2_SENSORYAREA_Q2333_206057_1_220020000")));
                    return DefaultCloseDialog(env, 1, 2);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 798084)
            {
                return SendQuestEndDialog(env);
            }
        }

        return false;
    }

    public override HandlerResult OnItemUseEvent(QuestEnv env, Item item)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            return HandlerResultExtensions.FromBoolean(UseQuestItem(env, item, 0, 0, false, 182204131, 1, 0, 0));
        }
        return HandlerResult.FAILED;
    }

    public override bool OnDieEvent(QuestEnv env)
    {
        return ResetStep(env);
    }

    public override bool OnLogOutEvent(QuestEnv env)
    {
        return ResetStep(env);
    }

    public override bool OnNpcReachTargetEvent(QuestEnv env)
    {
        return DefaultFollowEndEvent(env, 2, 2, true);
    }

    public override bool OnNpcLostTargetEvent(QuestEnv env)
    {
        return DefaultFollowEndEvent(env, 2, 1, false); // 0
    }

    // Reset step from 2 to 1 (following ribbit)
    private bool ResetStep(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            if (var == 2)
            {
                qs.SetQuestVar(1);
                UpdateQuestStatus(env);
                return true;
            }
        }
        return false;
    }
}
