using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The klaw egg (280482). Retail pattern <c>ND2_NeutEgg2</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. It hatches: it puts a <b>faithful subordinate</b>
/// (280481) on its own mark for ten minutes and removes itself. It was on plain <c>aggressive</c>, so
/// it sat there as a fightable egg and the klaw inside never came out.
/// <para>
/// This is the other half of <see cref="FrostmaneLestinAI"/>'s story. The subordinate looked reachable
/// only because Lestin's summon table called it at every rung by mistake; correcting Lestin to the
/// three elementals retail gives him left 280481 with nothing to spawn it, which is what the audit
/// then reported. The egg is where it was always supposed to come from.
/// </para>
/// <para>
/// <b>Retail's <c>on_see_user</c> branch is not translated, and it is dead in retail too.</b> It
/// repeats the wake branch's actions behind the same test-and-set flag var — so waking consumes the
/// flag and the see-user copy can never pass. It is there for an egg that somehow reaches a player
/// without having woken, which our spawn path does not allow.
/// </para>
/// <para>
/// <b>Not translated:</b> one <c>SKILLI_INDEX</c> self-cast, which would not survive the
/// <c>despawn_self</c> in the same branch even if it resolved.
/// </para>
/// </remarks>
[AIName("klaw_egg")]
public class KlawEggAI : PatternAi
{
    /// <summary><c>BDF2_NM_NeutQeenSu_39_Ae</c> — the klaw that was inside.</summary>
    private const int FaithfulSubordinate = 280481;

    /// <summary>Retail's <c>SPAWN_ID_1</c>. Nothing ever despawns the group; the ten minutes do.</summary>
    private const int Hatched = 1;

    private const int Lifetime = 600;

    /// <summary>Retail's <c>FLAGVARI_ALPHA_1</c> — see the remarks on why it can only ever pass here.</summary>
    private const int Hatching = 1;

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnWakeUp = Of(
            Branch(1, "", [When.FirstTime(Hatching)],
                Do.SpawnNear(FaithfulSubordinate, Hatched, count: 1, range: 0f, liveSeconds: Lifetime),
                Do.DespawnSelf())),
    };

    public KlawEggAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
