using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Sheban mystical tyrhund (284455) — the hands Researcher Teselik summons. Retail pattern
/// <c>IDVritra_Base_Drakan_Wi_Nmd_Sum</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Nothing in our server spawned these at all: their
/// only source is Teselik's summoning ritual, and he had no AI. They carry the two halves of his
/// counter mechanic — see <see cref="ResearcherTeselikAI"/>.
/// <list type="bullet">
/// <item>dying broadcasts 22260, which is how the boss learns his wave is thinning</item>
/// <item>hearing 22261 — his self-destruct order — drops a Burn Zone where the hand stands</item>
/// </list>
/// Between orders it knocks its target around every 15 seconds on a coin flip, and drags a random
/// attacker to the top of its hate list either way.
/// <para>
/// <b>The explosion.</b> Retail's hand spawns the burn zone <i>and</i> casts its own skill index 2, a
/// suicide skill our <c>npc_skills</c> does not carry — the list has one entry, the knockback at index
/// 0. The zone (284687) delivers the damage with <c>21206 Burn Zone</c>, so what is missing is the
/// hand's own blast, not the hazard. It despawns itself instead of dying to that skill: the boss has
/// already set his count to zero when he gives the order, so a hand left standing would put the count
/// and the field permanently out of step and the next wave would stack on top of this one.
/// </para>
/// </remarks>
[AIName("sheban_mystical_tyrhund")]
public class ShebanMysticalTyrhundAI : PatternAi
{
    private const int KnockBack = 16791;   // index 0

    /// <summary>284687, <c>BIDVritra_Base_Suicide_Mon</c> — carries 21206 Burn Zone at 100%.</summary>
    private const int BurnZone = 284687;

    /// <summary>Retail's <c>SPAWN_ID_NONE</c>: the zone outlives the hand, so nothing tracks it.</summary>
    private const int Untracked = 0;

    /// <summary>The dying hand shouts to 50 metres, which reaches the boss anywhere in his room.</summary>
    private const float MessageRange = 50f;

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnEnterAttack = Of(
            Branch(2, "SetTimer", When.Always,
                Do.ArmTimer(0, 5000))),

        OnBattleTimer = Of(
            Branch(4, "KnockBack", [When.Chance(50), When.Timer(0)],
                Do.ArmTimer(0, 15000),
                Do.SkillOnTarget(KnockBack),
                Do.SwitchTarget(AggroTarget.RANDOM)),

            Branch(3, "KnockBack", [When.Timer(0)],
                Do.ArmTimer(0, 7000),
                Do.SwitchTarget(AggroTarget.RANDOM))),

        // The boss's order. The zone stays where the hand was standing; the hand goes.
        OnMessage = Of(
            // Retail is a spawn and then a suicide SKILL, which kills the hand -- so its on_die branch
            // runs and the boss is told. Our npc_skills does not carry that skill, and DespawnSelf
            // removes the hand without killing it, so the death notice was silently lost: a
            // self-destructing hand never reported in, and only a hand killed by players did. The
            // broadcast is repeated here to put the chain back. It is a substitute for the skill,
            // not a translation of an action retail writes on this branch -- see
            // docs/retail-ai-fidelity.md.
            Branch(5, "self-destruct", [When.Message(ResearcherTeselikAI.SelfDestructOrder)],
                Do.SpawnNear(BurnZone, Untracked, count: 1, range: 0f),
                Do.Broadcast(ResearcherTeselikAI.HandDied, MessageRange),
                Do.DespawnSelf())),

        OnDie = Of(
            Branch(6, "tell the boss", When.Always,
                Do.Broadcast(ResearcherTeselikAI.HandDied, MessageRange))),
    };

    public ShebanMysticalTyrhundAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
