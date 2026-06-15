using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/tiamatStrongHold/TiamatEyeAI (@author Cheatkiller).</summary>
[AIName("tiamateye")]
public class TiamatEyeAI : NpcAI
{
    public TiamatEyeAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        int owner = GetOwner().GetNpcId();
        switch (owner)
        {
            case 283913:
                PacketSendUtility.BroadcastMessage(GetOwner(), 1500679, 2000);
                break;
            case 283914:
                PacketSendUtility.BroadcastMessage(GetOwner(), 1500680, 2000);
                break;
            case 283915:
                PacketSendUtility.BroadcastMessage(GetOwner(), 1500681, 2000);
                break;
            case 283916:
                PacketSendUtility.BroadcastMessage(GetOwner(), 1500682, 2000);
                break;
        }
        Despawn();
    }

    private void Despawn()
    {
        ThreadPoolManager.GetInstance().Schedule(() =>
        {
            if (!IsDead())
            {
                AIActions.DeleteOwner(this);
            }
        }, 5000L);
    }
}
