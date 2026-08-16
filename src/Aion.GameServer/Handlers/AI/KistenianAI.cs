using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Kistenian (204753), the Beluslan abyss guard. Retail pattern <c>DGuard_Kistenian</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. LEGENDARY, and two of the three adds his pattern
/// calls up were spawned by nothing anywhere.
/// <list type="bullet">
/// <item><b>on entering combat</b> — a flame of kistenian (295179) at his feet, which retail gives no
/// lifetime, so it stands until the fight ends</item>
/// <item><b>on dying</b> — 295181, six seconds. Despite its "dredgion elite fighter" display name this
/// is the pattern's despawn effect, not a reinforcement; the audit reports display names and this one
/// is misleading.</item>
/// </list>
/// <para>
/// This extends <see cref="AbyssGuardSimpleAI"/> rather than using the pattern runtime. That handler
/// is shared by 859 NPCs and carries real behaviour — its own npc-versus-npc aggro checks, and a
/// <c>CanHandleEvent</c> that stops it reacting to movement while already fighting — and
/// <c>PatternAi</c> descends from <c>AggressiveNpcAI</c>, so adopting it would drop all of that.
/// Extending is the same shape <see cref="TahabataPyrelordAI"/> uses. The events these hooks need are
/// unaffected by that override, which only special-cases <c>CREATURE_MOVED</c>.
/// </para>
/// <para>
/// <b>The third add is not reachable yet.</b> Fire spirits (295180) arrive when he hears message
/// 10016, and 10016 is broadcast by <c>DGuard_KistenianPet</c> — the fire spirit's own pattern. He
/// calls out with 10014 every three seconds to seventy-five metres and they answer; until that
/// pattern is ported there is nothing to answer him, so neither the heartbeat nor the reply handler is
/// implemented. The same is true of message 10018, which would place a second flame and comes from
/// the death effect's own pattern.
/// </para>
/// </remarks>
[AIName("kistenian")]
public class KistenianAI : AbyssGuardSimpleAI
{
    private const int FlameOfKistenian = 295179;
    private const int DespawnEffect = 295181;

    /// <summary>Retail places the flame within three metres and gives it no lifetime.</summary>
    private const float NearHim = 3f;

    /// <summary>The death effect's <c>live_time</c>.</summary>
    private const long EffectLifeMillis = 6000L;

    private Npc? flame;
    private bool engaged;

    public KistenianAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        if (engaged)
            return;

        engaged = true;
        WorldPosition at = GetPosition();
        flame = Spawn(FlameOfKistenian, at.GetX() + NearHim, at.GetY(), at.GetZ(),
            (sbyte)at.GetHeading()) as Npc;
    }

    /// <summary>Retail's <c>on_leave_attack_state</c> clears what he called up.</summary>
    protected override void HandleBackHome()
    {
        ClearFlame();
        base.HandleBackHome();
    }

    protected override void HandleDied()
    {
        ClearFlame();

        WorldPosition at = GetPosition();
        if (Spawn(DespawnEffect, at.GetX(), at.GetY(), at.GetZ(), (sbyte)at.GetHeading()) is Npc effect)
        {
            ThreadPoolManager.GetInstance().Schedule(_ =>
            {
                effect.GetController().DeleteIfAliveOrCancelRespawn();
                return ValueTask.CompletedTask;
            }, EffectLifeMillis);
        }

        base.HandleDied();
    }

    protected override void HandleDespawned()
    {
        ClearFlame();
        base.HandleDespawned();
    }

    private void ClearFlame()
    {
        engaged = false;
        if (flame != null && flame.IsSpawned())
            flame.GetController().DeleteIfAliveOrCancelRespawn();
        flame = null;
    }
}
