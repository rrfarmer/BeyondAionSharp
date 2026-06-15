using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/empyreanCrucible/EmpyreanAdministratorArminosAI (@author xTz).</summary>
[AIName("empadministratorarminos")]
public class EmpyreanAdministratorArminosAI : NpcAI
{
    public EmpyreanAdministratorArminosAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        StartEvent();
    }

    private void StartEvent()
    {
        switch (GetNpcId())
        {
            case 217744:
                PacketSendUtility.BroadcastMessage(GetOwner(), 1500247, 8000);
                PacketSendUtility.BroadcastMessage(GetOwner(), 1500250, 20000);
                PacketSendUtility.BroadcastMessage(GetOwner(), 1500251, 60000);
                break;
            case 217749:
                PacketSendUtility.BroadcastMessage(GetOwner(), 1500252, 8000);
                PacketSendUtility.BroadcastMessage(GetOwner(), 1500253, 16000);
                PacketSendUtility.BroadcastToMap(GetOwner(), 1400982, 25000);
                PacketSendUtility.BroadcastToMap(GetOwner(), 1400988, 27000);
                PacketSendUtility.BroadcastToMap(GetOwner(), 1400989, 29000);
                PacketSendUtility.BroadcastToMap(GetOwner(), 1400990, 31000);
                PacketSendUtility.BroadcastToMap(GetOwner(), 1401013, 93000);
                PacketSendUtility.BroadcastToMap(GetOwner(), 1401014, 113000);
                PacketSendUtility.BroadcastToMap(GetOwner(), 1401015, 118000);
                PacketSendUtility.BroadcastMessage(GetOwner(), 1500255, 118000);
                break;
            // case
            // despawn after 1min
        }
    }
}
