using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The trap that goes off the moment it appears. Retail pattern <c>NTrap_A</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Two branches, both the same: on waking, and on
/// seeing a player, cast the one skill on itself and leave. Fifty-three NPCs across the game bind this
/// pattern — flame patches, spawn markers, the thing a dying boss drops — and every one of them was
/// sitting on plain <c>aggressive</c>, which is why a "flame center" would walk over and punch someone
/// for the ten seconds it existed.
/// <para>
/// <b>The cast reaches players even though it is aimed at itself.</b> These skills are all
/// <c>target_type="AREA"</c> with <c>target_relation="ENEMY"</c>, so aiming at itself puts the trap at
/// the centre of the area and everyone hostile within range takes it. That is how a stationary marker
/// with a self-cast becomes a patch of fire on the floor.
/// </para>
/// <para>
/// <b>Why <c>on_see_user</c> is not translated.</b> It is the same branch as <c>on_wake_up</c>, and
/// waking always happens first — an NPC cannot see anyone before it exists. Retail carries both so a
/// trap laid in advance still fires when someone walks into it; ours are all placed on top of the
/// people they are meant to hit, by a boss mid-fight. If a pre-laid trap is ever ported, this needs
/// the second branch and a way to keep the skill's use count.
/// </para>
/// <para>
/// <b>Safe to point at any <c>NTrap_A</c> NPC whose npc_skills holds exactly one entry</b>, and inert
/// on any that holds more — see <see cref="PatternAi.CastOnlySkillOnSelf"/>. That check is doing real
/// work: resolving a skill index by position in our list is unreliable, and refusing is better than
/// guessing.
/// </para>
/// </remarks>
[AIName("ntrap")]
public class NTrapAI : PatternAi
{
    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnWakeUp = Of(
            Branch(1, "", When.Always,
                Do.OnlySkillOnSelf())),
    };

    public NTrapAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;

    /// <summary>
    /// Retail's <c>despawn_self</c>, which follows the cast in the same branch. Both are PLANNED
    /// actions, so the despawn is queued behind the cast rather than racing it — the marker stands for
    /// as long as the skill takes and goes when it lands.
    /// </summary>
    /// <remarks>
    /// Written as a hook rather than a second action in the branch because putting it in the branch
    /// removes the NPC while the cast is still in flight. That is not a cosmetic difference: a marker
    /// that is gone before its skill resolves is a marker nobody ever sees, and its ten-second
    /// <c>live_time</c> — which every boss that places one supplies — would be meaningless.
    /// </remarks>
    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        AIActions.DeleteOwner(this);
    }
}
