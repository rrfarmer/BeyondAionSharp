using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Spawns Harpback when Kratia dies, and schedules respawn of Kratia when Harpback dies.
/// Java parity: ai/worlds/eltnen/Kratia (Neon).
/// </summary>
[AIName("kratia")]
public class Kratia : AggressiveNpcAI
{
    public Kratia(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleDied()
    {
        Npc kratia = GetOwner();
        Npc harpback = (Npc)Spawn(211812, kratia.GetX(), kratia.GetY(), kratia.GetZ(), (sbyte)kratia.GetHeading());
        harpback.GetObserveController().Attach(new DeathObserver(_ => AIActions.ScheduleRespawn(this)));
        base.HandleDied();
    }

    public override bool Ask(AIQuestion question)
    {
        switch (question)
        {
            case AIQuestion.ALLOW_RESPAWN:
                return false;
            default:
                return base.Ask(question);
        }
    }
}
