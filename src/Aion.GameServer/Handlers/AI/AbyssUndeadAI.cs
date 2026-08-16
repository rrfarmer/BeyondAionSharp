using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The eternal and immortal dead of the Abyss — twenty-one spawned npcs across retail patterns
/// <c>AD2_UnDeadFi_Da</c>, <c>_Fi_Li</c>, <c>_Pr_Da</c>, <c>_Pr_Li</c>, <c>_Ra_Da</c>, <c>_Ra_Li</c>,
/// <c>_Wi_Da</c> and <c>_Wi_Li</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Eight patterns, one for each class and side, and
/// every one of them identical in the part that can be translated. All twenty-one npcs were on plain
/// <c>aggressive</c>.
/// <para>
/// <b>Killing one is a coin flip.</b> Half the time it leaves a <b>fear</b> (290137) standing on the
/// player who brought it down, for six minutes. Not near the corpse and not on the tank — on the
/// killer, which is what makes clearing a field of them a decision rather than a chore.
/// </para>
/// <para>
/// <b>This needed new vocabulary.</b> <c>spawn_on_target target_obj=OBJI_KILLER</c> had never come up,
/// and the pattern runtime had no way to say who had killed it. <see cref="PatternAi.Killer"/> reads
/// most-damage rather than most-hated, because that is the lookup the rest of the server already
/// treats as the killer — the same one loot ownership uses.
/// </para>
/// <para>
/// <b>Retail declares no spawn group for it</b> (<c>SPAWN_ID_NONE</c>), so nothing ever despawns these
/// as a set; the six minutes are the only thing that removes them. Carried as written.
/// </para>
/// <para>
/// <b>Not translated.</b> Two skill indices: the cast on engaging, and a self-cast on being hit or
/// spelled below thirty-five percent — a fifty-percent, once-a-fight reaction whose whole content is
/// that cast. The <c>say_to_all</c> on the death branch, which has no <c>npc_shouts.xml</c> row. And
/// the branch's <c>is_race</c> guard, which appears in the dump with no argument at all — the same
/// unreadable element recorded against the sealed akaimum. Here it cannot even be inferred: both the
/// Elyos-side and Asmodian-side patterns spawn the same npc, so whatever it distinguishes is not the
/// summon. Dropped rather than guessed at, which makes our version fire for any killer.
/// </para>
/// <para>
/// <c>on_killed_by_user</c> and <c>on_killed_by_npc</c> are one branch here for the reason already
/// recorded against Bollvig's bloodwings: our runtime raises a single death event.
/// </para>
/// </remarks>
[AIName("abyss_undead")]
public class AbyssUndeadAI : PatternAi
{
    /// <summary><c>BAb1_1130_UndeadSummon_A</c> — a fear.</summary>
    private const int Fear = 290137;

    /// <summary>Retail's <c>SPAWN_ID_NONE</c>: this one belongs to no group.</summary>
    private const int Ungrouped = 0;

    private const int FearLife = 360;

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnDie = Of(
            Branch(3, "half the time, on whoever did it", [When.Chance(50)],
                Do.SpawnOnKiller(Fear, Ungrouped, count: 1, liveSeconds: FearLife))),
    };

    public AbyssUndeadAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
