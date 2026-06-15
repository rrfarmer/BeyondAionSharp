using Aion.GameServer.Ai;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Container;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/siege/ArtifactProtectorAI (ATracer).</summary>
[AIName("artifact_protector")]
public class ArtifactProtectorAI : AbstractSiegeProtectorAI
{
    public ArtifactProtectorAI(Npc owner)
        : base(owner)
    {
    }

    public override void ModifyOwnerStat(Stat2 stat)
    {
        if (stat.GetStat() == StatEnum.MAXHP && GetOwner().GetLevel() >= 65)
            stat.SetBaseRate(SiegeConfig.ARTIFACT_PROTECTOR_HEALTH_MULTIPLIER);
    }
}
