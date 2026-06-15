using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Spawns.Siegespawns;
using Aion.GameServer.Services;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/siege/SiegeNpcAI (ATracer).</summary>
public class SiegeNpcAI : AggressiveNpcAI
{
    public SiegeNpcAI(Npc owner)
        : base(owner)
    {
    }

    public override bool Ask(AIQuestion question)
    {
        switch (question)
        {
            case AIQuestion.ALLOW_DECAY:
            case AIQuestion.ALLOW_RESPAWN:
            case AIQuestion.REWARD_LOOT:
            case AIQuestion.REMOVE_EFFECTS_ON_MAP_REGION_DEACTIVATE:
                return false;
            default:
                return base.Ask(question);
        }
    }

    protected Aion.GameServer.Services.Siege.Siege GetSiege()
    {
        return SiegeService.GetInstance().GetSiege(GetSpawnTemplate().GetSiegeId());
    }

    protected new SiegeSpawnTemplate GetSpawnTemplate()
    {
        return (SiegeSpawnTemplate)base.GetSpawnTemplate();
    }
}
