using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Spawns.Siegespawns;
using Aion.GameServer.Services;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/siege/SiegeNpcAI (ATracer).</summary>
/// <remarks>
/// Every override below is the Java class verbatim. What changed is the base: it was
/// <c>AggressiveNpcAI</c> and is now <see cref="Aion.GameServer.Ai.Pattern.PatternAi"/>, which derives
/// from it and adds nothing when the table is empty — every pattern hook returns immediately on a
/// zero-length branch list.
/// <para>
/// The reason is <see cref="AbstractSiegeProtectorAI"/> and retail's <b>30002</b>. That broadcast hangs
/// off a battle-timer chain, timers are what <c>PatternAi</c> has and this class did not, and the
/// alternative was hand-rolling a timer inside a siege class. <see cref="AbyssGuardSimpleAI"/> was
/// rebased for the same reason and its remark records the same reasoning; this is the second time, which
/// is a fair argument that the pattern base should have been the default.
/// </para>
/// </remarks>
public class SiegeNpcAI : Aion.GameServer.Ai.Pattern.PatternAi
{
    /// <summary>Nothing, unless a subclass says otherwise.</summary>
    private static readonly Aion.GameServer.Ai.Pattern.AiPattern Nothing =
        new Aion.GameServer.Ai.Pattern.AiPattern();

    protected override Aion.GameServer.Ai.Pattern.AiPattern Pattern => Nothing;

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
