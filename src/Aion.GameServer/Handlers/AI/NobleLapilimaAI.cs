using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The noble lapilima of the Abyssal Reliquary (216946, 216957, 281895, 281917, 283192). Retail
/// pattern <c>IDAbRe_Core_FlyingWorm</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. All five were on plain <c>aggressive</c> and the
/// three flash lapilimo they call up (281896, 281918, 281919) were spawned by nothing anywhere.
/// <para>
/// The whole pattern is one chain: ten seconds after something engages it, it splits off three
/// smaller worms at its own feet, and does it again every fifteen seconds for as long as the fight
/// lasts. Nothing caps them, so a fight that drags turns into a swarm — which is the mechanic, and the
/// reason the worm is meant to be killed quickly rather than tanked.
/// </para>
/// <para>
/// <b>Everything here is index-free</b> — the pattern casts nothing at all, so this is a complete
/// translation rather than a partial one. The three summons are distinct npc ids sharing one display
/// name, which is why they read as one add in the client and three in the data.
/// </para>
/// <para>
/// <b>The wave is not cleared on death or reset.</b> Retail files the spawns under
/// <c>SPAWN_ID_1</c> but never despawns that group — there is no <c>on_die</c>, no
/// <c>on_leave_attack_state</c>, no despawn anywhere in the pattern. The worms it has split off
/// outlive it, and that is deliberate rather than an omission: they carry
/// <c>despawn_at_attack_state</c>, so the engine retires them when the fight itself ends.
/// </para>
/// </remarks>
[AIName("noble_lapilima")]
public class NobleLapilimaAI : PatternAi
{
    private const int FlashLapilimo53 = 281918;
    private const int FlashLapilimo54 = 281919;
    private const int FlashLapilimo55 = 281896;

    /// <summary>Retail's <c>SPAWN_ID_1</c>. Nothing in the pattern ever despawns it — see the remarks.</summary>
    private const int Split = 1;

    private const int FirstSplitMillis = 10000;
    private const int SplitIntervalMillis = 15000;
    private const float AtItsFeet = 3f;

    private static PatternAction Splinter(int npcId) =>
        Do.SpawnNear(npcId, Split, count: 1, range: AtItsFeet);

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnEnterAttack = Of(
            Branch(2, "", When.Always,
                Do.ArmTimer(0, FirstSplitMillis))),

        OnBattleTimer = Of(
            Branch(1, "", [When.Timer(0)],
                Splinter(FlashLapilimo53),
                Splinter(FlashLapilimo54),
                Splinter(FlashLapilimo55),
                Do.ArmTimer(0, SplitIntervalMillis))),
    };

    public NobleLapilimaAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
