using System.Collections.Generic;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Controllers.Attack;
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
public class KistenianAI : AbyssGuardSimpleAI, INpcMessageListener
{
    /// <summary>What he calls out every three seconds; his spirits answer it.</summary>
    public const int CallToPets = 10014;

    /// <summary>Sent by the despawn effect a dying spirit leaves: he lights another flame.</summary>
    public const int LightAnotherFlame = 10018;

    private const int FireSpirit = 295180;

    private const float CallRange = 75f;
    private const long CallIntervalMillis = 3000L;

    /// <summary>Retail puts them on the current target, two of them, three on a quarter roll.</summary>
    private const float SpiritRange = 2f;
    private const int SpiritLife = 6;

    private const int FlameOfKistenian = 295179;
    private const int DespawnEffect = 295181;

    /// <summary>Retail places the flame within three metres and gives it no lifetime.</summary>
    private const float NearHim = 3f;

    /// <summary>The death effect's <c>live_time</c>.</summary>
    private const long EffectLifeMillis = 6000L;

    private readonly List<Npc> flames = new List<Npc>();
    private ScheduledTask? callTask;
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
        LightFlame();
        callTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            if (!IsDead())
                NpcMessageBus.Broadcast(GetOwner(), CallToPets, GetOwner(), CallRange);
            return ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(CallIntervalMillis),
           System.TimeSpan.FromMilliseconds(CallIntervalMillis));
    }

    private void LightFlame()
    {
        WorldPosition at = GetPosition();
        if (Spawn(FlameOfKistenian, at.GetX() + NearHim, at.GetY(), at.GetZ(),
                (sbyte)at.GetHeading()) is Npc lit)
            flames.Add(lit);
    }

    /// <summary>
    /// The two halves of the loop. A spirit calling for more brings a fresh pair out on whoever he is
    /// facing; the effect a dying spirit leaves hands him another flame.
    /// </summary>
    public void OnNpcMessage(Npc sender, int messageType, VisibleObject? param)
    {
        if (IsDead() || !engaged)
            return;

        switch (messageType)
        {
            case KistenianPetAI.CallForMore:
                SendSpirits();
                break;
            case LightAnotherFlame:
                LightFlame();
                break;
        }
    }

    private void SendSpirits()
    {
        if (GetAggroList().GetTarget(AggroTarget.MOST_HATED) is not Creature target)
            return;

        int count = Rnd.Chance() < 25 ? 3 : 2;
        for (int i = 0; i < count; i++)
        {
            float angle = Rnd.NextFloat(360f) * (float)System.Math.PI / 180f;
            float x = target.GetX() + (float)(System.Math.Cos(angle) * SpiritRange);
            float y = target.GetY() + (float)(System.Math.Sin(angle) * SpiritRange);
            if (Spawn(FireSpirit, x, y, target.GetZ(), (sbyte)0) is not Npc spirit)
                continue;

            ThreadPoolManager.GetInstance().Schedule(_ =>
            {
                spirit.GetController().DeleteIfAliveOrCancelRespawn();
                return ValueTask.CompletedTask;
            }, SpiritLife * 1000L);
        }
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
        if (callTask != null && !callTask.IsDone())
            callTask.Cancel(true);
        callTask = null;

        foreach (Npc lit in flames)
            if (lit.IsSpawned())
                lit.GetController().DeleteIfAliveOrCancelRespawn();
        flames.Clear();
    }
}
