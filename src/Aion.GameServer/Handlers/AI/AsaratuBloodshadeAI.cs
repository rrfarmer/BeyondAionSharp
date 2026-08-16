using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Asaratu Bloodshade (215283), Dark Poeta. Retail pattern <c>Dragon_G4</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. The other half of the Dark Poeta finding: he and
/// Vanuka Infernus both ran on plain <c>aggressive</c> while Tahabata and Calindi had classes.
/// <para>
/// Two chains run at once, both armed six seconds into the fight. Timer 0 is the banded one and gets
/// slower as he weakens — 16s at full health, 22s below 80 and again below 50, each of its slower
/// steps leaving a flame center at his feet. Timer 9 drives a faster loop that only does something
/// below 20%, where it summons a subordinate every 22 seconds.
/// </para>
/// <para>
/// The flame center (281246) was spawned by nothing in our server. His subordinate (281245) is
/// spawned elsewhere already, so only the flame is new content — but the chain that places it is the
/// fight, and none of it happened.
/// </para>
/// <para>
/// **Casts not translated**: ten skills, indices up to 9, no branch comments. The chain is
/// index-free, so the timings and the spawns are faithful and his npc_skills probabilities still
/// drive what he casts.
/// </para>
/// </remarks>
[AIName("asaratu_bloodshade")]
public class AsaratuBloodshadeAI : PatternAi
{
    private const int FlameCenter = 281246;
    private const int FaithfulSubordinate = 281245;

    // Retail files these under three different spawn ids. Nothing despawns an individual one, so
    // the split does no work here, but it is what the pattern says.
    private const int LizardGroup = 3;
    private const int LowFlameGroup = 5;
    private const int MidFlameGroup = 1;

    /// <summary>Everything he places lands on him, within a metre.</summary>
    private const float AtHisFeet = 1f;

    /// <summary>One link: arm the next slot, and optionally leave something behind.</summary>
    private static PatternBranch Step(int priority, PatternCondition[] guards, int next, int delay,
        PatternAction? extra = null)
    {
        PatternAction[] actions = extra is null
            ? [Do.ArmTimer(next, delay)]
            : [Do.ArmTimer(next, delay), extra];
        return Branch(priority, "", guards, actions);
    }

    private static PatternAction Flame(int group) =>
        Do.SpawnNear(FlameCenter, group, count: 1, range: AtHisFeet);

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnEnterAttack = Of(
            Branch(15, "", When.Always,
                Do.ArmTimer(0, 6000),
                Do.ArmTimer(9, 6000))),

        OnBattleTimer = Of(
            // Below 20% timer 9 hands over to a two-step loop that keeps summoning.
            Step(14, [When.Timer(7), When.HpBelow(20)], next: 6, delay: 20000),
            Step(13, [When.Timer(6), When.HpBelow(20)], next: 7, delay: 12000),
            Step(12, [When.Timer(9), When.HpBelow(20)], next: 6, delay: 22000,
                extra: Do.SpawnNear(FaithfulSubordinate, LizardGroup, count: 1, range: AtHisFeet)),

            Step(11, [When.Timer(5)], next: 9, delay: 10000),

            Step(10, [When.Timer(8), When.HpBetween(21, 50)], next: 5, delay: 22000,
                extra: Flame(LowFlameGroup)),
            Step(9, [When.Timer(0), When.HpBetween(21, 50)], next: 8, delay: 22000),

            Step(8, [When.Timer(0), When.HpBetween(51, 80)], next: 2, delay: 22000,
                extra: Flame(MidFlameGroup)),

            // The middle of the chain, which every band shares.
            Step(7, [When.Timer(4)], next: 2, delay: 12000),
            Step(6, [When.Timer(3)], next: 4, delay: 12000),
            Step(5, [When.Timer(2)], next: 3, delay: 12000),

            // The pattern guards these two at 80-100 and 81-100 respectively. The overlap at exactly
            // 80 is the designers', and is reproduced rather than tidied: at 80 the first still
            // matches and the second does not.
            Step(4, [When.Timer(1), When.HpBetween(80, 100)], next: 2, delay: 12000),
            Step(3, [When.Timer(0), When.HpBetween(81, 100)], next: 1, delay: 16000),

            // Both chains carry their own heartbeat, so a tick that lands between bands does not end
            // the fight's rotation.
            Step(2, [When.Timer(9)], next: 9, delay: 6000),
            Step(1, [When.Timer(0)], next: 0, delay: 6000)),

        OnEnterIdle = Of(
            Branch(16, "", When.Always,
                Do.Despawn(LizardGroup),
                Do.Despawn(LowFlameGroup),
                Do.Despawn(MidFlameGroup))),

        OnDie = Of(
            Branch(16, "", When.Always,
                Do.Despawn(LizardGroup),
                Do.Despawn(LowFlameGroup),
                Do.Despawn(MidFlameGroup))),
    };

    public AsaratuBloodshadeAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
