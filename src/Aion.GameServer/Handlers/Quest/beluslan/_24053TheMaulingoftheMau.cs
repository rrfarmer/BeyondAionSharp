using Aion.GameServer.Ai.Event;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.QuestEngine.Task;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Ritsu
/// </summary>
public class _24053TheMaulingoftheMau : AbstractQuestHandler
{
    private static readonly int[] npc_ids = { 204787, 204795, 204796, 204700 };

    public _24053TheMaulingoftheMau() : base(24053)
    {
    }

    public override void Register()
    {
        qe.RegisterOnLogOut(questId);
        qe.RegisterAddOnReachTargetEvent(questId);
        qe.RegisterAddOnLostTargetEvent(questId);
        qe.RegisterOnQuestCompleted(questId);
        qe.RegisterOnLevelChanged(questId);
        foreach (int npc_id in npc_ids)
            qe.RegisterQuestNpc(npc_id).AddOnTalkEvent(questId);
    }

    public override bool OnNpcReachTargetEvent(QuestEnv env)
    {
        return DefaultFollowEndEvent(env, 3, 4, false, 253);
    }

    public override bool OnNpcLostTargetEvent(QuestEnv env)
    {
        return DefaultFollowEndEvent(env, 3, 2, false);
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
                ChangeQuestStep(env, 3, 2);
            }
        }
        return false;
    }

    public override void OnQuestCompletedEvent(QuestEnv env)
    {
        DefaultOnQuestCompletedEvent(env, 24050);
    }

    public override void OnLevelChangedEvent(Player player)
    {
        DefaultOnLevelChangedEvent(player, 24050);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null)
            return false;

        int var = qs.GetQuestVarById(0);
        int targetId = env.GetTargetId();
        int dialogActionId = env.GetDialogActionId();

        if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 204700)
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
                    return SendQuestDialog(env, 10002);
                else
                    return SendQuestEndDialog(env);
            }
            return false;
        }
        else if (qs.GetStatus() != QuestStatus.START)
        {
            return false;
        }
        if (targetId == 204787)
        {
            switch (dialogActionId)
            {
                case DialogAction.QUEST_SELECT:
                    if (var == 0)
                        return SendQuestDialog(env, 1011);
                    else if (var == 4)
                        return SendQuestDialog(env, 2375);
                    return false;
                case DialogAction.SELECT1_1:
                    PlayQuestMovie(env, 252);
                    break;
                case DialogAction.SETPRO1:
                    if (var == 0)
                    {
                        return DefaultCloseDialog(env, 0, 1); // 1
                    }
                    return false;
                case DialogAction.SET_SUCCEED:
                    if (var == 4)
                    {
                        return DefaultCloseDialog(env, 4, 4, true, false); // reward
                    }
                    break;
            }
        }
        else if (targetId == 204795)
        {
            switch (dialogActionId)
            {
                case DialogAction.QUEST_SELECT:
                    if (var == 1)
                        return SendQuestDialog(env, 1352);
                    return false;
                case DialogAction.SETPRO2:
                    if (var == 1)
                    {
                        return DefaultCloseDialog(env, 1, 2); // 2
                    }
                    break;
            }
        }
        else if (targetId == 204796)
        {
            switch (dialogActionId)
            {
                case DialogAction.QUEST_SELECT:
                    if (var == 2)
                        return SendQuestDialog(env, 1693);
                    return false;
                case DialogAction.SETPRO3:
                    if (var == 2)
                    {
                        Npc survivor = (Npc) SpawnInFrontOf(204806, player);
                        survivor.GetAi().OnCreatureEvent(AiEventType.FOLLOW_ME, player);
                        player.GetController().AddTask(TaskId.QUEST_FOLLOW, QuestTasks.NewFollowingToTargetCheckTask(env, survivor, 204813));
                        return DefaultCloseDialog(env, 2, 3);
                    }
                    break;
            }
        }
        return false;
    }
}
