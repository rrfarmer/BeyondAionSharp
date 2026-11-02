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
                PacketSendUtility.BroadcastMessage(GetOwner(), 1500251, 92000);
                break;
            case 217749:
                PacketSendUtility.BroadcastMessage(GetOwner(), 1500252, 8000);
                PacketSendUtility.BroadcastMessage(GetOwner(), 1500253, 16000);
                PacketSendUtility.BroadcastToMap(GetOwner(), 1400982, 24000);
                PacketSendUtility.BroadcastToMap(GetOwner(), 1400988, 26000);
                PacketSendUtility.BroadcastToMap(GetOwner(), 1400989, 28000);
                PacketSendUtility.BroadcastToMap(GetOwner(), 1400990, 30000);
                break;
        }
    }
}
