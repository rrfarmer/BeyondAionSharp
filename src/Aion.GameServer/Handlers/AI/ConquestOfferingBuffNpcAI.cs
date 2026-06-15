using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.SkillEngine;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/ConquestOfferingBuffNpcAI (@author Yeats).</summary>
[AIName("conquest_offering_buff_npc")]
public class ConquestOfferingBuffNpcAI : ActionItemNpcAI
{
    private readonly AtomicBoolean used = new AtomicBoolean(false);
    private ScheduledTask despawnTask;

    public ConquestOfferingBuffNpcAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        SendWakeUpMsg();
        despawnTask = ThreadPoolManager.GetInstance().Schedule(ct => { GetOwner().GetController().Delete(); return System.Threading.Tasks.ValueTask.CompletedTask; }, 65000L);
    }

    protected override void HandleUseItemFinish(Player player)
    {
        if (used.CompareAndSet(false, true))
        {
            SendTalkedMsg();
            int skillId = 21924 + Rnd.Get(0, 3);
            SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), skillId, 1, player).UseSkill();
            GetOwner().GetController().Delete();
        }
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        CancelTask();
    }

    protected override void HandleDespawned()
    {
        CancelTask();
        base.HandleDespawned();
    }

    private void CancelTask()
    {
        if (despawnTask != null && !despawnTask.IsDone())
            despawnTask.Cancel(true);
    }

    private void SendWakeUpMsg()
    {
        int msg = (1501279 + (Rnd.Get(0, 2) * 2));
        PacketSendUtility.BroadcastMessage(GetOwner(), msg, 1500);
    }

    private void SendTalkedMsg()
    {
        int msg = (1501280 + (Rnd.Get(0, 2) * 2));
        PacketSendUtility.BroadcastMessage(GetOwner(), msg);
    }
}
