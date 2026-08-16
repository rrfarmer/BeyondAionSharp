using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The summon spot that calls up a worm (281271). Retail pattern <c>Dragon_G2SlaveSuWo</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Calindi places four of these at once while she is
/// between 31% and 60%, each for ten seconds, and each calls up one faithful subordinate (281267) on
/// the mark it stands on. The same arrangement as Tahabata's — see
/// <see cref="TahabataSummonSpotAI"/> for why the cast resolves.
/// </remarks>
[AIName("calindi_worm_spot")]
public class CalindiSummonSpotAI : PatternAi
{
    private const int Worm = 281267;
    private const int Called = 1;
    private const int Summon = 18222;

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnWakeUp = Of(
            Branch(1, "", When.Always,
                Do.SpawnNear(Worm, Called, count: 1, range: 0f),
                Do.SkillOnSelf(Summon))),
    };

    public CalindiSummonSpotAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The summon spot that calls up a drakan (281272). Retail pattern <c>Dragon_G2SlaveSuDr</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Calindi places <b>two</b> of these below 45%, on
/// the first and third marks only — where Tahabata's equivalent step places four. Same skill, same
/// shape, half the ring.
/// </remarks>
[AIName("calindi_drakan_spot")]
public class CalindiDrakanSpotAI : PatternAi
{
    private const int Drakan = 281268;
    private const int Called = 1;
    private const int Summon = 18222;

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnWakeUp = Of(
            Branch(1, "", When.Always,
                Do.SpawnNear(Drakan, Called, count: 1, range: 0f),
                Do.SkillOnSelf(Summon))),
    };

    public CalindiDrakanSpotAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// Calindi's worm (281267). Retail pattern <c>Dragon_G2SlaveWorm</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. The whole pattern is one branch: it leaves when
/// Calindi calls for a fresh ring of worm spots. Everything else about it is a plain aggressive
/// monster, which is what its template already gave it.
/// </remarks>
[AIName("calindi_worm")]
public class CalindiSlaveAI : PatternAi
{
    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnMessage = Of(
            Branch(7, "the next ring is coming", [When.Message(DarkPoetaCalindiFlamelordAI.ClearTheWorms)],
                Do.DespawnSelf())),
    };

    public CalindiSlaveAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// Calindi's drakan (281268). Retail pattern <c>Dragon_G2SlaveDrakan</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. It answers its own call — 3412 rather than the
/// worms' 3413 — so a fresh pair of drakan spots clears the standing drakan and leaves any worms
/// alone, and vice versa.
/// <para>
/// <b>Its own combat chain is not translated.</b> Retail gives it five battle-timer slots, and every
/// branch on them is a cast: four indices are addressed against three distinct skills, so nothing
/// maps. Its <c>on_despawn</c> effect spawn is left out with them — the NPC it names,
/// <c>Dragon_G2SlaveDrakanSu</c>, binds to nothing in our 4.8 client.
/// </para>
/// </remarks>
[AIName("calindi_drakan")]
public class CalindiDrakanAI : PatternAi
{
    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnMessage = Of(
            Branch(12, "the next pair is coming", [When.Message(DarkPoetaCalindiFlamelordAI.ClearTheDrakan)],
                Do.DespawnSelf())),
    };

    public CalindiDrakanAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
