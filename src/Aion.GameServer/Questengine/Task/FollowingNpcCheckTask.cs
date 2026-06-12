using Aion.GameServer.Ai.Event;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.QuestEngine.Task.Checker;
using Aion.GameServer.Utils;

namespace Aion.GameServer.QuestEngine.Task;

/// <summary>Java parity: questEngine/task/FollowingNpcCheckTask (ATracer). Runnable→method Run; AiEventType→AiEventType; QuestEngine/PositionUtil/Npc.GetAi red-tolerated.</summary>
public class FollowingNpcCheckTask
{
    private readonly QuestEnv env;
    private readonly DestinationChecker destinationChecker;

    internal FollowingNpcCheckTask(QuestEnv env, DestinationChecker destinationChecker)
    {
        this.env = env;
        this.destinationChecker = destinationChecker;
    }

    public void Run()
    {
        Player player = env.GetPlayer();
        Npc npc = (Npc)destinationChecker.GetFollower();
        if (player.IsDead() || npc.IsDead())
        {
            OnFail(env);
        }
        if (!PositionUtil.IsInRange(player, npc, 50))
        {
            OnFail(env);
        }

        if (destinationChecker.Check())
        {
            OnSuccess(env);
        }
    }

    /// <summary>Following task succeeded, proceed with quest</summary>
    private void OnSuccess(QuestEnv env)
    {
        StopFollowing(env);
        Aion.GameServer.QuestEngine.QuestEngine.GetInstance().OnNpcReachTarget(env);
    }

    /// <summary>Following task failed, abort further progress</summary>
    protected void OnFail(QuestEnv env)
    {
        StopFollowing(env);
        Aion.GameServer.QuestEngine.QuestEngine.GetInstance().OnNpcLostTarget(env);
    }

    private void StopFollowing(QuestEnv env)
    {
        Player player = env.GetPlayer();
        Npc npc = (Npc)destinationChecker.GetFollower();
        player.GetController().CancelTask(TaskId.QUEST_FOLLOW);
        npc.GetAi().OnCreatureEvent(AiEventType.STOP_FOLLOW_ME, player);
        if (!npc.GetAi().GetName().Equals("following"))
            npc.GetController().Delete();
    }
}
