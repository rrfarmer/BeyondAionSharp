using Aion.GameServer.Ai.Event;
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
public class _2634TheDraupnirRedemption : AbstractQuestHandler
{
    private static readonly int[] npcs = { 204828, 700350, 204830 };

    public _2634TheDraupnirRedemption() : base(2634)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204828).AddOnQuestStart(questId);
        foreach (int npc in npcs)
        {
            qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
        }
        qe.RegisterOnLogOut(questId);
        qe.RegisterAddOnReachTargetEvent(questId);
        qe.RegisterAddOnLostTargetEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        int targetId = 0;
        if (env.GetVisibleObject() is Npc visNpc)
            targetId = visNpc.GetNpcId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 204828)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 4762);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 700350:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.USE_OBJECT:
                            {
                                if (qs.GetQuestVarById(0) == 0)
                                    return SendQuestDialog(env, 1011);
                                return false;
                            }
                        case DialogAction.SETPRO1:
                            {
                                Npc npc = (Npc) env.GetVisibleObject();
                                npc.GetController().Delete();
                                Npc survivor = (Npc) SpawnInFrontOf(204830, player);
                                PacketSendUtility.SendPacket(player, new SM_NPC_INFO(survivor, player));
                                survivor.GetAi().OnCreatureEvent(AiEventType.FOLLOW_ME, player);
                                player.GetController().AddTask(TaskId.QUEST_FOLLOW, QuestTasks.NewFollowingToTargetCheckTask(env, survivor, 204828));
                                return DefaultCloseDialog(env, 0, 1); // 1
                            }
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 204828)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 10002);
                else
                    return SendQuestEndDialog(env);
            }
        }
        return false;
    }

    public override bool OnNpcReachTargetEvent(QuestEnv env)
    {
        return DefaultFollowEndEvent(env, 1, 2, true); // reward
    }

    public override bool OnNpcLostTargetEvent(QuestEnv env)
    {
        return DefaultFollowEndEvent(env, 1, 0, false);
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
                ChangeQuestStep(env, 1, 0);
            }
        }
        return false;
    }
}
