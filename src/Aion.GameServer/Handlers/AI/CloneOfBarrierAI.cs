using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/worlds/inggison/CloneOfBarrierAI (Luzien).</summary>
[AIName("omegaclone")]
public class CloneOfBarrierAI : AggressiveNpcAI
{
    public CloneOfBarrierAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        // delay for spawn animation and because KnownList isn't initialized yet
        ThreadPoolManager.GetInstance().Schedule(
            _ =>
            {
                if (GetKnownList().GetObject(GetCreatorId()) is Npc omega)
                {
                    GetOwner().SetTarget(omega);
                    AIActions.UseSkill(this, 18671);
                }

                return ValueTask.CompletedTask;
            },
            3000L);
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        if (GetKnownList().GetObject(GetCreatorId()) is Npc omega)
            omega.GetEffectController().RemoveEffect(18671);
    }
}
