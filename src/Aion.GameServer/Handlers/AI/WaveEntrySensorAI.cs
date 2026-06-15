using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/drakenspire/WaveEntrySensorAI (@author Estrayl).</summary>
[AIName("wave_entry_sensor")]
public class WaveEntrySensorAI : AggressiveNpcAI
{
    public WaveEntrySensorAI(Npc owner)
        : base(owner)
    {
    }

    public override void HandleCreatureDetected(Creature creature)
    {
        base.HandleCreatureDetected(creature);
        if (creature is Player)
            GetOwner().GetController().Delete();
    }
}
