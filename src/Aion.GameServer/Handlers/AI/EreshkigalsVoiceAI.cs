using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/worlds/panesterra/ahserionsflight/EreshkigalsVoiceAI (@author Estrayl).</summary>
[AIName("ereshkigals_voice")]
public class EreshkigalsVoiceAI : NpcAI
{
    private ScheduledTask? idleTask;

    public EreshkigalsVoiceAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        idleTask = ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            PacketSendUtility.BroadcastMessage(GetOwner(), 1501160); // I won't forgive you, Fregion!!!
            GetOwner().GetController().AddTask(TaskId.DESPAWN,
                ThreadPoolManager.GetInstance().Schedule(_ =>
                {
                    GetOwner().GetController().DeleteIfAliveOrCancelRespawn();
                    return System.Threading.Tasks.ValueTask.CompletedTask;
                }, 2000L));
            return System.Threading.Tasks.ValueTask.CompletedTask;
        }, 30000L);
    }

    protected override void HandleDespawned()
    {
        if (idleTask != null && !idleTask.IsDone())
            idleTask.Cancel(true);
        base.HandleDespawned();
    }
}
