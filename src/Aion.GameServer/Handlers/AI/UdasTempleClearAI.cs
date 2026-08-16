using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The invisible controller Lower Udas Temple's bosses drop as they die (281418). Retail pattern
/// <c>IDTP_Keeper3</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. One line: <b>broadcast 6956 to fifty metres, then
/// remove yourself</b>. It is the temple's clear-up — kill a boss and everything it called goes with
/// it — and it was recorded as a sender with no listener until the four patterns that answer 6956
/// were translated alongside it.
/// <para>
/// Each boss drops <b>five</b>: one within a metre and four scattered to twenty-five, which is how a
/// fifty-metre broadcast covers a room bigger than fifty metres.
/// </para>
/// </remarks>
[AIName("udas_temple_clear")]
public class UdasTempleClearAI : PatternAi
{
    /// <summary>Retail's message: everything this boss called, go away.</summary>
    public const int BossIsDown = 6956;

    private const float Reach = 50f;

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnWakeUp = Of(
            Branch(7, "", When.Always,
                Do.Broadcast(BossIsDown, Reach),
                Do.DespawnSelf())),
    };

    public UdasTempleClearAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// Everything Lower Udas Temple's bosses call up. Retail patterns <c>IDTP_Keeper2</c>,
/// <c>IDTP_NepBoss2</c>, <c>IDTP_NepBoss3</c> and <c>IDTP_NepEx2</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Ten NPCs across four patterns — the punishment
/// chakras, the protection of aion, the pyre souls and the shatters — and all four answer <b>6956</b>
/// the same way: <c>despawn_self</c>. That is the half of the temple's clear-up that lives on the
/// adds, and it is the reason <see cref="UdasTempleClearAI"/> is worth spawning at all.
/// <para>
/// <b>What each pattern does beyond that is not translated</b>, and differs per role: the chakras
/// walk a route we do not have, the nuclei and pyre souls answer message 6955 with a cast, the
/// shatters run a fourteen-second cast loop, and the pyre souls have a one-in-two chance of casting
/// and vanishing when hit. Every one of those is a <c>SKILLI_INDEX</c> or a waypoint. Sharing one
/// class for the despawn is not a claim that the four patterns are the same — only that this branch
/// of them is.
/// </para>
/// </remarks>
[AIName("udas_temple_add")]
public class UdasTempleAddAI : PatternAi
{
    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnMessage = Of(
            Branch(10, "", [When.Message(UdasTempleClearAI.BossIsDown)],
                Do.DespawnSelf())),
    };

    public UdasTempleAddAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
