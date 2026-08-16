using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The summon spot that calls up a faithful subordinate (281262). Retail pattern
/// <c>Dragon_G1SlaveSuCy</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Tahabata places four of these at once while he is
/// between 31% and 60%, each for ten seconds, and each calls up one cyclops (281258) on the mark it is
/// standing on. Nothing in our server spawned either the spot or, by this route, the subordinate.
/// <para>
/// <b>Its cast resolves, unusually cleanly.</b> The pattern addresses one index and the npc has one
/// skill, which alone would only be a count match — but the skill is <b>18222 "Summon"</b>, whose
/// stack name is <c>BNWI_SPELLATKTA5_APPEAR_NR</c>. A summon spot casting Summon as it makes something
/// appear is corroboration, not coincidence.
/// </para>
/// <para>
/// <b>It does not remove itself.</b> Retail's branch is a spawn and a cast with no despawn; the ten
/// seconds come from Tahabata's <c>live_time</c> on the spawn, so the spot is removed by whoever
/// placed it. Dying or resetting Tahabata clears the whole ring with it.
/// </para>
/// </remarks>
[AIName("tahabata_cyclops_spot")]
public class TahabataSummonSpotAI : PatternAi
{
    private const int FaithfulSubordinate = 281258;
    private const int Called = 1;

    /// <summary>Retail's <c>live_time</c> on the subordinate: ten minutes, or until Tahabata is done.</summary>
    private const int SubordinateLife = 600;

    private const int Summon = 18222;

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnWakeUp = Of(
            Branch(1, "", When.Always,
                Do.SpawnNear(FaithfulSubordinate, Called, count: 1, range: 0f, liveSeconds: SubordinateLife),
                Do.SkillOnSelf(Summon))),
    };

    public TahabataSummonSpotAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The summon spot that calls up a drakan (281263). Retail pattern <c>Dragon_G1SlaveSuDr</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. The same shape as
/// <see cref="TahabataSummonSpotAI"/>, on the same four marks, casting the same 18222 — Tahabata
/// places these instead once he is below 45%, and what steps off them is a drakan (281259) rather than
/// a cyclops.
/// <para>
/// One difference worth keeping: retail gives the cyclops a ten-minute <c>live_time</c> and gives the
/// drakan none. The drakan stays until something removes it.
/// </para>
/// </remarks>
[AIName("tahabata_drakan_spot")]
public class TahabataDrakanSpotAI : PatternAi
{
    private const int Drakan = 281259;
    private const int Called = 1;
    private const int Summon = 18222;

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnWakeUp = Of(
            Branch(1, "", When.Always,
                Do.SpawnNear(Drakan, Called, count: 1, range: 0f),
                Do.SkillOnSelf(Summon))),
    };

    public TahabataDrakanSpotAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
