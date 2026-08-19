using System.Collections.Generic;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Calindi’s two ground hazards. Retail patterns <c>IDTiamat_Kalyndi_ShadowFire</c> and
/// <c>IDTiamat_Kalyndi_FireCrown</c>, with their <c>_Dmg</c> twins.
/// </summary>
/// <remarks>
/// Java parity: ai/instance/dragonLordsRefuge/CalindiSummonsAI (Cheatkiller, Luzien, Estrayl).
/// Retail-sourced corrections below; see docs/retail-ai-fidelity.md.
/// <para>
/// <b>This class holds retail’s relationship upside down, and that is fine.</b> Retail places a hazard
/// npc that stands still and spawns a short-lived <c>_Dmg</c> npc at its own feet, each of which casts
/// once and expires; this port places the <c>_Dmg</c> npc as the persistent one and spawns the hazard
/// beside it as a texture. The visible result is the same — a patch of ground that pulses — so the
/// inversion is kept. <b>What was wrong is how often it pulses and how long it stands.</b>
/// </para>
/// <para>
/// <b>The two hazards are not the same shape, and this class treated them as one.</b> Retail’s fire
/// crown re-arms its idle timer, so it drops a damage npc <b>every second for the ten seconds it
/// stands</b>. Retail’s shadow fire does not re-arm: it drops <b>exactly one</b>, a second after it
/// appears, and then simply stands for fifteen seconds. This class ran both as fixed-rate loops for
/// fifteen seconds — the crown every two seconds, and <b>the shadow fire every half second, which is
/// about thirty casts where retail has one</b>.
/// </para>
/// </remarks>
[AIName("calindi_summon")]
public class CalindiSummonsAI : NpcAI
{
    private ScheduledTask task;
    private VisibleObject textureObject = null;

    public CalindiSummonsAI(Npc owner)
        : base(owner)
    {
    }

    /// <summary>
    /// How one hazard behaves: when its first pulse lands, how often it repeats, and how long it stands.
    /// </summary>
    /// <param name="RepeatMillis">
    /// Retail re-arms the idle timer on the fire crown and not on the shadow fire, so zero here means
    /// one pulse and no more.
    /// </param>
    private readonly record struct Hazard(long FirstMillis, long RepeatMillis, long LifeMillis);

    /// <summary>Retail’s <c>set_idle_timer</c> on both hazards: the first pulse is a second in.</summary>
    private const long FirstPulse = 1000L;

    /// <summary>
    /// The fire crown, which re-arms, and the shadow fire, which does not.
    /// </summary>
    /// <remarks>
    /// The crown’s ten seconds is the <c>live_time</c> the burrow-dispel worm gives it; the shadow
    /// fire’s fifteen is the one Calindi herself gives it, in both the normal and hard patterns.
    /// </remarks>
    private static readonly Dictionary<int, Hazard> Hazards = new Dictionary<int, Hazard>
    {
        [283131] = new Hazard(FirstPulse, 1000L, 10_000L), // IDTiamat_Kalyndi_FireCrown
        [856299] = new Hazard(FirstPulse, 1000L, 10_000L),
        [283133] = new Hazard(FirstPulse, 0L, 15_000L),    // IDTiamat_Kalyndi_ShadowFire
        [856298] = new Hazard(FirstPulse, 0L, 15_000L),
    };

    protected override void HandleSpawned()
    {
        base.HandleSpawned();

        if (!Hazards.TryGetValue(GetNpcId(), out Hazard hazard))
            return;

        if (GetFakeTextureNpcId() != 0)
        {
            textureObject = Spawn(GetFakeTextureNpcId(), GetPosition().GetX(), GetPosition().GetY(), GetPosition().GetZ(), (sbyte)0);
        }

        if (hazard.RepeatMillis > 0)
        {
            task = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
            {
                AIActions.UseSkill(this, GetSkillId());
                return ValueTask.CompletedTask;
            }, System.TimeSpan.FromMilliseconds(hazard.FirstMillis), System.TimeSpan.FromMilliseconds(hazard.RepeatMillis));
        }
        else
        {
            // Retail’s shadow fire has no second set_idle_timer, so it burns once and then just stands.
            task = ThreadPoolManager.GetInstance().Schedule(_ =>
            {
                AIActions.UseSkill(this, GetSkillId());
                return ValueTask.CompletedTask;
            }, hazard.FirstMillis);
        }

        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            AIActions.DeleteOwner(this);
            if (textureObject != null)
            {
                textureObject.GetController().Delete();
            }

            return ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(hazard.LifeMillis));
    }

    private int GetSkillId()
    {
        switch (GetNpcId())
        {
            case 283131:
                return 20916;
            case 283133:
                return 20914;
            case 856298:
                return 21891;
            case 856299:
                return 21892;
            default:
                return 0;
        }
    }

    private int GetFakeTextureNpcId()
    {
        switch (GetNpcId())
        {
            case 283131:
            case 856299:
                return 283130;
            case 283133:
            case 856298:
                return 283132;
            default:
                return 0;
        }
    }

    protected override void HandleDespawned()
    {
        // An npc that is not in the hazard table never armed one.
        if (task != null && !task.IsDone())
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
