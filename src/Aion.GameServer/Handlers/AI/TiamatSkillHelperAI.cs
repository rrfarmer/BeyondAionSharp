using System.Collections.Generic;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The sand a burrowing thorn throws up (283135, 856041). Retail patterns
/// <c>IDTiamat_Tiamat_Uplift</c> and <c>IDTiamat_Hard_Earthquake_01</c>.
/// </summary>
/// <remarks>
/// Java parity: ai/instance/dragonLordsRefuge/TiamatSkillHelperAI (@author Estrayl March 8th, 2018).
/// Retail-sourced correction below; see docs/retail-ai-fidelity.md.
/// <para>
/// Both patterns say the same thing: on waking, place a damage npc at your own feet for three seconds.
/// Java found that npc by adding one to its own id, which is right for the normal-mode pair
/// (283135 → 283136) and <b>wrong for the hard-mode one</b>: 856041 pairs with <b>856124</b>, not
/// 856042, which is a different npc entirely. The pairing is a table now, for the same reason
/// <see cref="TiamatBurrowingThornAI"/> keeps one — the two generations do not run parallel id blocks
/// and arithmetic on ids is how the wrong npc gets into a room.
/// </para>
/// <para>
/// <b>Hard mode was not reaching this class at all.</b> 856041 was bound to <c>useSkillAndDie</c>, and
/// it has no row in our npc skill data — that AI deletes an npc with an empty skill list the instant it
/// spawns. So the hard-mode uplift vanished on arrival and its damage npc was never placed. Rebound to
/// this class, which is what its retail pattern describes.
/// </para>
/// </remarks>
[AIName("tiamat_skill_helper")]
public class TiamatSkillHelperAI : NpcAI
{
    public TiamatSkillHelperAI(Npc owner)
        : base(owner)
    {
    }

    public override float ModifyDamage(Creature attacker, float damage, Effect effect)
    {
        return 0;
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        HandleSkillTask();
    }

    /// <summary>
    /// The damage npc each uplift places, by the uplift that places it.
    /// </summary>
    /// <remarks>
    /// Normal mode is <c>IDTiamat_Tiamat_Uplift</c> → <c>_Uplift_Dmg</c>, which happens to be the next
    /// id up. Hard mode is <c>BIDTiamat_Tiamat_Uplift_Hard</c> → <c>_Uplift_Dmg_Hard</c>, which is not:
    /// 856041 pairs with 856124.
    /// </remarks>
    private static readonly Dictionary<int, int> DamageByUplift = new Dictionary<int, int>
    {
        [283135] = 283136,
        [856041] = 856124,
    };

    /// <summary>Retail gives the damage npc three seconds; this class ends itself on the same clock.</summary>
    private const int DamageLife = 3;

    private void HandleSkillTask()
    {
        ThreadPoolManager.GetInstance().Schedule(() =>
        {
            if (!DamageByUplift.TryGetValue(GetNpcId(), out int damageNpc))
                return;

            WorldPosition p = GetPosition();
            SpawnFor(damageNpc, p.GetX(), p.GetY(), p.GetZ(), (sbyte)p.GetHeading(), DamageLife);
            ThreadPoolManager.GetInstance().Schedule(() => AIActions.Die(this), 3000L);
        }, 1500L);
    }

    protected override void HandleDied()
    {
        RespawnService.ScheduleDecayTask(GetOwner(), 3000);
        base.HandleDied();
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.REWARD_AP_XP_DP_LOOT => false,
            _ => base.Ask(question),
        };
    }
}
