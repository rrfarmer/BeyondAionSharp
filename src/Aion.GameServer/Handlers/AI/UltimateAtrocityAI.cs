using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Tiamat's breath beacons — the strip of ground his dying breath sweeps.
/// </summary>
/// <remarks>
/// Java parity: ai/instance/dragonLordsRefuge/UltimateAtrocityAI (Luzien, Estrayl). Retail-sourced corrections below; see
/// docs/retail-ai-fidelity.md. Found by <c>audit_timer_drift.py</c>, whose "casts only" note was about
/// <i>which</i> skill and never about <i>when</i>.
/// <para>
/// <b>It opened half a second after landing and burned for eleven.</b> Retail's beacons are controllers:
/// <c>on_wake_up</c> sets an idle timer of <b>2000</b> and each firing lays a row of two-second damage
/// npcs. And <c>IDTiamat_Tiamat_Dragon_Dying_Named_60_Al</c> spawns every one of them with
/// <c>live_time=<b>7</b></c> — not eleven.
/// </para>
/// <para>
/// So this port ran <b>six pulses where retail runs three</b>: at 0.5, 2.5, 4.5, 6.5, 8.5 and 10.5
/// against 2, 4 and 6. Twice the damage from a hazard that stood over half again as long.
/// </para>
/// <para>
/// <b>The "4s" and "8s" in the beacon names are not lifetimes.</b> Every variant — L, M and R, in both
/// the four and eight forms — is spawned with the same seven seconds. It is worth saying because the
/// names invite exactly the wrong inference, and eleven seconds looks like somebody splitting the
/// difference between them.
/// </para>
/// <para>
/// <b>The FX/DMG collapse is kept:</b> retail's controller spawns rows of
/// <c>..._dmg</c> npcs at fixed coordinates which cast on waking and despawn; this port applies the
/// damage from the controller itself, to players in front of it within forty-five metres.
/// </para>
/// </remarks>
[AIName("ultimate_atrocity")]
public class UltimateAtrocityAI : GeneralNpcAI
{
    /// <summary>Retail's <c>set_idle_timer</c> on the beacon, on waking and on every firing.</summary>
    public const long OpeningMillis = 2000L;
    public const long RepeatMillis = 2000L;

    /// <summary>Retail's <c>live_time</c> on every beacon variant, four-second and eight alike.</summary>
    public const long BeaconLifeMillis = 7000L;

    private ScheduledTask task;

    public UltimateAtrocityAI(Npc owner)
        : base(owner)
    {
    }

    public override ItemAttackType ModifyAttackType(ItemAttackType type)
    {
        return ItemAttackType.MAGICAL_FIRE;
    }

    public override float ModifyOwnerDamage(float damage, Creature effected, Effect effect)
    {
        return damage / 4;
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        int skill = GetNpcId() switch
        {
            283244 => 21160,
            283240 => 21156,
            283237 or 283241 => 20923,
            _ => 0,
        };

        if (skill == 0)
            return;

        task = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            AIActions.UseSkill(this, skill);
            return ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(OpeningMillis), System.TimeSpan.FromMilliseconds(RepeatMillis));

        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            AIActions.DeleteOwner(this);
            return ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(BeaconLifeMillis));
    }

    protected override void HandleDespawned()
    {
        task.Cancel(true);
        base.HandleDespawned();
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
