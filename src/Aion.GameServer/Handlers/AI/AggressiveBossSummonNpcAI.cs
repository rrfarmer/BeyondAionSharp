using Aion.GameServer.Ai;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/AggressiveBossSummonNpcAI (Yeats).</summary>
[AIName("aggressive_boss_summon")]
public class AggressiveBossSummonNpcAI : AggressiveNpcAI
{
    public AggressiveBossSummonNpcAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleAttackComplete()
    {
        base.HandleAttackComplete();
        if (!IsCreatorStillFighting())
            GetOwner().GetController().Delete();
    }

    protected override void HandleFinishAttack()
    {
        GetOwner().GetController().Delete();
    }

    private bool IsCreatorStillFighting()
    {
        return GetKnownList().GetObject(GetCreatorId()) is Creature creator && !creator.IsDead() && creator.GetAggroList().GetTarget(AggroTarget.MOST_HATED) != null;
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        GetOwner().GetController().Delete();
    }
}
