using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/beshmundirTemple/ManadarAI (xTz).</summary>
[AIName("manadar")]
public class ManadarAI : AggressiveNpcAI, HpPhases.PhaseHandler
{
    private readonly HpPhases hpPhases = new HpPhases(90);

    public ManadarAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        hpPhases.TryEnterNextPhase(this);
    }

    public void HandleHpPhase(int phaseHpPercent)
    {
        TrySpawnTraps();
    }

    protected override void HandleBackHome()
    {
        base.HandleBackHome();
        hpPhases.Reset();
    }

    private void TrySpawnTraps()
    {
        if (GetPosition().IsSpawned() && !IsDead() && hpPhases.GetCurrentPhase() > 0)
        {
            for (int i = 0; i < 5; i++)
            {
                RndSpawnInRange(Rnd.NextBoolean() ? 281545 : 281756, 4, 11);
            }
            ThreadPoolManager.GetInstance().Schedule(_ => { TrySpawnTraps(); return ValueTask.CompletedTask; }, 6000L);
        }
    }
}
