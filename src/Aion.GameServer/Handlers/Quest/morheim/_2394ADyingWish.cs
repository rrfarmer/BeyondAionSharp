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
public class _2394ADyingWish : AbstractQuestHandler
{
    public _2394ADyingWish() : base(2394)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204343).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(204343).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(204381).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(701147).AddOnTalkEvent(questId);
        qe.RegisterQuestItem(182204130, questId);
        qe.RegisterAddOnLostTargetEvent(questId);
        qe.RegisterAddOnReachTargetEvent(questId);
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
            if (targetId == 204343)
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
            if (targetId == 204381)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 1011);
                }
                else if (dialogActionId == DialogAction.SETPRO1)
                {
                    Npc orlan = (Npc) SpawnInFrontOf(790021, player);
                    WalkManager.StartWalking((NpcAI) orlan.GetAi());
                    orlan.GetAi().OnCreatureEvent(AiEventType.FOLLOW_ME, player);
                    PacketSendUtility.BroadcastPacket(orlan, new SmEmotion(orlan, EmotionType.CHANGE_SPEED, 0, orlan.GetObjectId()));
                    player.GetController().AddTask(TaskId.QUEST_FOLLOW,
                        QuestTasks.NewFollowingToTargetCheckTask(env, orlan, ZoneName.Get("HALABANA_HOT_SPRINGS_220020000")));
                    return DefaultCloseDialog(env, 0, 1);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 204343)
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
                {
                    return SendQuestDialog(env, 5);
                }
                else
                {
                    return SendQuestEndDialog(env);
                }
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
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            if (var == 1)
            {
                qs.SetQuestVar(0);
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
            if (var == 1)
            {
                qs.SetQuestVar(0);
                UpdateQuestStatus(env);
            }
        }
        return false;
    }

    public override bool OnNpcReachTargetEvent(QuestEnv env)
    {
        return DefaultFollowEndEvent(env, 1, 1, true);
    }

    public override bool OnNpcLostTargetEvent(QuestEnv env)
    {
        return DefaultFollowEndEvent(env, 1, 0, false); // 0
    }
}
