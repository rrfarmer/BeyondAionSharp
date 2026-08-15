using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Omega, Inggison. Retail pattern <c>LF4_FieldRaid</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. His fight is four waves of clones, and each wave
/// <em>replaces</em> the one before it: crossing 85% brings three clones of power, 65% clears those and
/// brings three of explosion, 45% clears those for three of healing, and 25% clears those for a single
/// pair of barrier clones. They are summoned onto whoever he is fighting, not onto himself.
/// <para>
/// What this replaces came from <c>ai/spawn_helpers.xml</c> and differed in four ways: the thresholds
/// were 80/60/40/20, nothing was ever cleared so the waves accumulated, the last wave was three physical
/// barriers rather than one physical and one magical, and the clone of magical barrier (281949) was
/// therefore never spawned by anything at all.
/// </para>
/// <para>
/// His skill rotation is not translated. The pattern addresses thirteen indices and its branches carry
/// no comments, so nothing corroborates which of our fourteen skills each index names — see the
/// skill-index problem in the fidelity doc. The two casts that accompanied the old summons are kept on
/// the same branches, and the rest of his casting stays with his npc_skills probabilities.
/// </para>
/// </remarks>
[AIName("omega")]
public class OmegaAI : PatternAi
{
    private const int CloneOfPower = 281945;
    private const int CloneOfExplosion = 281946;
    private const int CloneOfHealing = 281947;
    private const int CloneOfPhysicalBarrier = 281948;
    private const int CloneOfMagicalBarrier = 281949;

    // Retail's spawn ids, kept as it numbers them: each wave is filed under its own id so the next
    // phase can clear exactly the previous one. They run backwards against the fight's order.
    private const int LastWave = 1;
    private const int HealingWave = 2;
    private const int ExplosionWave = 3;
    private const int PowerWave = 4;

    /// <summary>Waves live ten minutes unless a later phase clears them.</summary>
    private const int WaveLife = 600;

    // The two casts the old summon path made on every wave. Which pattern indices they correspond to is
    // unresolved, so they stay where they already were rather than being placed by guesswork.
    private const int SummonCastA = 19189;
    private const int SummonCastB = 19191;

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnEnterAttack = Of(
            Branch(1, "", When.Always,
                Do.ArmTimer(0, 5000))),

        OnBattleTimer = Of(
            Branch(20, "phase 25%", [When.Timer(0), When.HpBelow(25), When.FirstTime(1)],
                Do.Despawn(HealingWave),
                Do.ArmTimer(0, 5000),
                Do.SkillOnSelf(SummonCastA),
                Do.SkillOnSelf(SummonCastB),
                Do.SpawnOnTarget(CloneOfMagicalBarrier, LastWave, range: 3f, liveSeconds: WaveLife),
                Do.SpawnOnTarget(CloneOfPhysicalBarrier, LastWave, range: 3f, liveSeconds: WaveLife)),

            Branch(19, "phase 45%", [When.Timer(0), When.HpBelow(45), When.FirstTime(2)],
                Do.Despawn(ExplosionWave),
                Do.ArmTimer(0, 5000),
                Do.SkillOnSelf(SummonCastA),
                Do.SkillOnSelf(SummonCastB),
                Do.SpawnOnTarget(CloneOfHealing, HealingWave, count: 3, range: 3f, liveSeconds: WaveLife)),

            Branch(18, "phase 65%", [When.Timer(0), When.HpBelow(65), When.FirstTime(3)],
                Do.Despawn(PowerWave),
                Do.ArmTimer(0, 5000),
                Do.SkillOnSelf(SummonCastA),
                Do.SkillOnSelf(SummonCastB),
                Do.SpawnOnTarget(CloneOfExplosion, ExplosionWave, count: 3, range: 3f, liveSeconds: WaveLife)),

            Branch(17, "phase 85%", [When.Timer(0), When.HpBelow(85), When.FirstTime(4)],
                Do.ArmTimer(0, 5000),
                Do.SkillOnSelf(SummonCastA),
                Do.SkillOnSelf(SummonCastB),
                Do.SpawnOnTarget(CloneOfPower, PowerWave, count: 3, range: 3f, liveSeconds: WaveLife)),

            // The heartbeat every phase branch depends on: without it the chain ends on the first tick
            // that crosses no threshold, and no later phase ever runs.
            Branch(7, "", [When.Timer(0)],
                Do.ArmTimer(0, 5000))),

        OnEnterIdle = Of(
            Branch(1, "", When.Always,
                Do.Despawn(LastWave),
                Do.Despawn(HealingWave),
                Do.Despawn(ExplosionWave),
                Do.Despawn(PowerWave))),

        OnDie = Of(
            Branch(1, "", When.Always,
                Do.Despawn(LastWave),
                Do.Despawn(HealingWave),
                Do.Despawn(ExplosionWave),
                Do.Despawn(PowerWave))),
    };

    public OmegaAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
