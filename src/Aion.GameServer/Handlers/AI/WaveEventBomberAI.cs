using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/drakenspire/WaveEventBomberAI (@author Estrayl).</summary>
[AIName("wave_event_bomber")]
public class WaveEventBomberAI : GeneralNpcAI
{
    public WaveEventBomberAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        PacketSendUtility.BroadcastMessage(GetOwner(), 1501312, 4000);
    }
}
