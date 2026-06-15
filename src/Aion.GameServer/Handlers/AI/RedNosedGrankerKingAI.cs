using System.Linq;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/events/RedNosedGrankerKingAI (Tibald, Neon).</summary>
[AIName("rednosedgrankerking")]
public class RedNosedGrankerKingAI : OneDmgAI, HpPhases.PhaseHandler
{
    private readonly HpPhases hpPhases = new HpPhases(50);

    public RedNosedGrankerKingAI(Npc owner)
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
        switch (GetNpcId())
        {
            case 219292:
            case 219294:
                int npcId = GetNpcId() + 1;
                RndSpawnInRange(npcId, 1, 3);
                RndSpawnInRange(npcId, 1, 3);
                RndSpawnInRange(npcId, 1, 3);
                break;
        }
    }

    protected override void HandleBackHome()
    {
        base.HandleBackHome();
        hpPhases.Reset();
    }

    protected override void HandleDied()
    {
        long chestSpawnCount = GetAggroList().Stream().Count(a => a.GetAttacker() is Player p && IsInRange(p, 50));
        for (int i = 0; i < chestSpawnCount; i++)
            RndSpawnInRange(701457, 1, 6); // stolen solorius box

        GetOwner().GetKnownList().ForEachNpc(n =>
        {
            if (n.GetNpcId() == 219293 || n.GetNpcId() == 219295)
                n.GetController().Delete();
        });
        base.HandleDied();
    }
}
