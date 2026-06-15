using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/eternalBastion/EternalBastionCommanderAI (@author Estrayl).</summary>
[AIName("eternal_bastion_commander")]
public class EternalBastionCommanderAI : EternalBastionAggressiveNpcAI
{
    private ScheduledTask? hpRestoreTask;
    private bool wasAttacked;

    public EternalBastionCommanderAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleFinishAttack()
    {
        if (!CanThink())
            return;
        EmoteManager.EmoteStopAttacking(GetOwner());
        GetOwner().GetController().LoseAggro(false);
        hpRestoreTask = ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            GetOwner().GetLifeStats().TriggerRestoreTask();
            wasAttacked = false;
            return ValueTask.CompletedTask;
        }, 120000L);
    }

    private void CancelTask()
    {
        if (hpRestoreTask != null && !hpRestoreTask.IsDone())
            hpRestoreTask.Cancel(false);
    }

    protected override void HandleCreatureAggro(Creature creature)
    {
        if (creature is Npc npc)
        {
            if (npc.GetNpcId() == 231130) // Pashid
            {
                GetAggroList().AddHate(npc, 10000000); // Retail
                PacketSendUtility.BroadcastMessage(GetOwner(), 1500768 + GetRace().GetRaceId() * 4);
            }
        }
        CancelTask();
        base.HandleCreatureAggro(creature);
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        CancelTask();
        if (!wasAttacked || Rnd.Chance() < 1)
            PacketSendUtility.BroadcastToMap(GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_IDLDF5b_TD_Notice_05());
        wasAttacked = true;
    }

    protected override void HandleBackHome()
    {
        base.HandleBackHome();
        int msgId = GetNpcId() == 209516 ? 1500763 : 1500767;
        PacketSendUtility.BroadcastMessage(GetOwner(), msgId);
    }

    protected override void HandleDespawned()
    {
        base.HandleDespawned();
        CancelTask();
    }
}
