using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The mark an incarnation leaves when it dies (283063-283066). Retail patterns
/// <c>IDTiamat_BurrowingWorm_SumAscention_OnDie</c>, <c>IDTiamat_NagaQueen_BlazingInfernoFX</c>,
/// <c>_BlazingInfernoDmg</c> and <c>IDTiamat_NagaQueen_Meteor</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md.
/// <para>
/// All four patterns are three lines and identical in shape: cast a dispel on themselves, <b>broadcast
/// a message at ninety-nine metres</b>, and despawn. Each message is answered by exactly one <b>balaur
/// spiritualist</b> — the drakan mage standing at that incarnation's corner of the room — which
/// despawns on hearing it. <b>Killing an incarnation is what removes its mage.</b>
/// </para>
/// <para>
/// <c>TiamatsIncarnationAI</c> already places these four npcs on the right deaths, for retail's six
/// seconds. But they were bound to <c>general</c>, so they were pure scenery: <b>every mage stayed in
/// the room no matter how many incarnations the raid killed</b>. Found by
/// <c>audit_silent_hazards.py</c>, which lists npcs whose retail pattern casts while this port gives
/// them no way to.
/// </para>
/// <para>
/// <b>The dispel is not translated.</b> Each pattern's <c>use_skill</c> names <c>SKILLI_INDEX_0</c>,
/// and none of these npcs has a row in our npc skill data — the same blocker logged against Tiamat's
/// hard-mode uplift. The mage removal needs no skill and is what is ported here.
/// </para>
/// </remarks>
[AIName("tiamat_incarnation_death_effect")]
public class TiamatIncarnationDeathEffectAI : NpcAI
{
    /// <summary>
    /// The mage each death effect dismisses, normal and hard, by the message retail sends.
    /// </summary>
    /// <remarks>
    /// <c>IDTiamat_Temp16</c> through <c>_Temp19</c> answer 55 through 58 respectively, and each has a
    /// normal and a hard-mode id. The pairs are listed rather than computed: the normal ids run
    /// 283163-283166 and the hard ones 856483-856486, so no arithmetic relates them.
    /// </remarks>
    private static readonly Dictionary<int, int[]> MageByDeathEffect = new Dictionary<int, int[]>
    {
        [283063] = [283163, 856483], // broadcast 55, Fissurefang
        [283064] = [283165, 856485], // broadcast 57, Petriscale
        [283065] = [283166, 856486], // broadcast 58, Graviwing
        [283066] = [283164, 856484], // broadcast 56, Wrathclaw
    };

    /// <summary>Retail's <c>range_as_meter</c> on all four broadcasts.</summary>
    private const float Earshot = 99f;

    public TiamatIncarnationDeathEffectAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();

        if (!MageByDeathEffect.TryGetValue(GetNpcId(), out int[]? mages))
            return;

        foreach (Npc npc in GetPosition().GetWorldMapInstance().GetNpcs().ToList())
        {
            if (npc == null || npc.IsDead() || !mages.Contains(npc.GetNpcId()))
                continue;
            if (!PositionUtil.IsInRange(GetOwner(), npc, Earshot))
                continue;

            npc.GetController().Delete();
        }
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.ALLOW_DECAY or AIQuestion.ALLOW_RESPAWN or AIQuestion.REWARD_AP_XP_DP_LOOT => false,
            _ => base.Ask(question),
        };
    }
}
