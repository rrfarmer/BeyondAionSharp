using System.Collections.Generic;
using Aion.GameServer.Utils;
using System.Threading.Tasks;
using System.Linq;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Traitor Kumbanda (219355). Retail pattern <c>IDTiamat_Kumbanda</c>.
/// </summary>
/// <remarks>
/// Java parity: @author Cheatkiller. Retail-sourced corrections below; see docs/retail-ai-fidelity.md.
/// <para>
/// <b>Both of his mechanics hung off a five per cent roll on every blow he took.</b> Retail runs them
/// from two battle timers: the summoning circles at five seconds and then every fourteen, and the
/// avatar at six seconds and then every twenty-five. A roll per hit means a fast group triggered both
/// constantly and a slow one barely at all — the cadence was a function of how hard he was being hit.
/// </para>
/// <para>
/// <b>The circles stand on four fixed marks.</b> Retail names them: (871, 1332), (853, 1332),
/// (853, 1306) and (871, 1306), all at z 396, each for fifteen seconds. This class put one at his own
/// feet and scattered six more at random inside six metres, so the room never looked the same twice and
/// the marks a raid learns to avoid did not exist.
/// </para>
/// <para>
/// <b>And the avatar belongs on a player, not on him.</b> Retail spawns it with
/// <c>ATTACKERI_RANDOM_ONE_EXCEPT_CURRENT_TARGET</c> — someone other than the tank — inside a hundred
/// metres, with <c>attack_target_after_spawn</c> and <c>hatepoints_to_add=2147483647</c>, which is
/// retail saying this will not peel. This class spawned it at Kumbanda's own position with no hate at
/// all, so it walked to whoever the boss was already fighting.
/// </para>
/// <para>
/// The health windows are retail's too: the circles between fifteen and a hundred per cent, the avatar
/// between fifteen and <b>seventy</b> — this class used fifty — and the rage below <b>fifteen</b>, where
/// it was five.
/// </para>
/// <para>
/// <b>Not translated.</b> The power attack on <c>BTIMERI_INDEX_0</c> every seven seconds, and the three
/// casts that accompany the circles, the avatar and the rage: all name skill indices, and none of these
/// npcs has a row in our npc skill data.
/// </para>
/// </remarks>
[AIName("traitorkumbanda")]
public class TraitorKumbandaAI : AggressiveNpcAI
{
    /// <summary>Retail's <c>BTIMERI_INDEX_1</c>: five seconds to the first circles, fourteen between.</summary>
    private static readonly TimeSpan CircleFirst = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan CircleRepeat = TimeSpan.FromSeconds(14);

    /// <summary>Retail's <c>BTIMERI_INDEX_2</c>: six seconds to the first avatar, twenty-five between.</summary>
    private static readonly TimeSpan AvatarFirst = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan AvatarRepeat = TimeSpan.FromSeconds(25);

    /// <summary>The four marks retail puts the circles on, all at the same height.</summary>
    private static readonly (float X, float Y)[] CircleMarks =
        [(871f, 1332f), (853f, 1332f), (853f, 1306f), (871f, 1306f)];

    private const float CircleZ = 396f;

    /// <summary>Retail's health windows: circles 15-100, avatar 15-70, rage below 15.</summary>
    private const int CircleFloorPercent = 15;
    private const int AvatarFloorPercent = 15;
    private const int AvatarCeilingPercent = 70;
    private const int RagePercent = 15;

    /// <summary>Retail's <c>valid_distance</c> on the avatar, and the hate it arrives with.</summary>
    private const float AvatarReach = 100f;
    private const int AvatarHate = int.MaxValue;

    private ScheduledTask? circleTask;
    private ScheduledTask? avatarTask;
    private readonly AtomicBoolean isHome = new AtomicBoolean(true);
    private bool isFinalBuff;

    public TraitorKumbandaAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);

        if (isHome.CompareAndSet(true, false))
        {
            circleTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
            {
                SpawnTimeAccelerator();
                return ValueTask.CompletedTask;
            }, CircleFirst, CircleRepeat);

            avatarTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
            {
                SpawnKumbandaGhost();
                return ValueTask.CompletedTask;
            }, AvatarFirst, AvatarRepeat);
        }

        if (!isFinalBuff && GetOwner().GetLifeStats().GetHpPercentage() <= RagePercent)
        {
            isFinalBuff = true;
            AIActions.UseSkill(this, 20942);
        }
    }

    /// <summary>Retail's circle rung: four marks, fifteen seconds each, while he is above the floor.</summary>
    private void SpawnTimeAccelerator()
    {
        if (IsDead() || GetLifeStats().GetHpPercentage() <= CircleFloorPercent)
            return;
        // No "one at a time" guard: retail's rung has none, and with fourteen seconds between turns and
        // fifteen of life the sets deliberately overlap by a second. Java's guard suppressed every turn
        // whose predecessor was still standing, which is all of them.
        SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 20726, 55, GetOwner()).UseNoAnimationSkill();
        foreach ((float x, float y) in CircleMarks)
            SpawnFor(283086, x, y, CircleZ, (sbyte)0, CircleLife);
    }

    /// <summary>
    /// Retail's avatar rung: on somebody other than the tank, already fighting them.
    /// </summary>
    private void SpawnKumbandaGhost()
    {
        if (IsDead())
            return;

        int percent = GetLifeStats().GetHpPercentage();
        if (percent <= AvatarFloorPercent || percent > AvatarCeilingPercent)
            return;
        if (GetPosition().GetWorldMapInstance().GetNpc(283085) != null)
            return;

        // ATTACKERI_RANDOM_ONE_EXCEPT_CURRENT_TARGET: anyone in reach but the one he is facing. When he
        // has no target the most hated is the one he would be facing, so that is what is excluded --
        // without the fallback the exclusion silently does nothing and the avatar can land on the tank.
        Creature? facing = GetOwner().GetTarget() as Creature;
        Creature? mostHated = GetOwner().GetAggroList().GetTarget(AggroTarget.MOST_HATED);
        List<Creature> others = GetOwner().GetAggroList().StreamValidTargets(AvatarReach)
            .Where(c => c != facing && c != mostHated).ToList();
        if (others.Count == 0)
            return;

        Creature victim = others[Rnd.Get(0, others.Count - 1)];
        if (Spawn(283085, victim.GetX(), victim.GetY(), victim.GetZ(), (sbyte)0) is not Npc avatar)
            return;

        // hatepoints_to_add=2147483647 with attack_target_after_spawn -- retail saying this one will not
        // peel. Deferred a tick, because an aggressive npc picks its own target out of BringIntoWorld and
        // overwrites anything set inline: the first version of this set the hate directly and the avatar
        // simply re-acquired whoever the boss was already facing, which is the bug being fixed.
        AttackAfterSpawn.NextTick(avatar, victim, AvatarHate);
    }

    private void DeleteNpcs(List<Npc> npcs)
    {
        foreach (Npc npc in npcs)
        {
            if (npc != null)
            {
                npc.GetController().Delete();
            }
        }
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        WorldMapInstance instance = GetPosition().GetWorldMapInstance();
        DeleteNpcs(instance.GetNpcs(283086));
        DeleteNpcs(instance.GetNpcs(283088));
    }

    protected override void HandleBackHome()
    {
        base.HandleBackHome();
        isFinalBuff = false;
    }

    /// <summary>
    /// Retail <c>IDTiamat_Kumbanda</c> gives the circle effect <c>live_time</c> 15 on all twelve of its
    /// spawns. Ours had none, so the "only if none are standing" guard above never passed a second time
    /// and the accelerator ran once per fight.
    /// </summary>
    private const int CircleLife = 15;

    private void RndSpawn(int npcId, int count, int liveSeconds)
    {
        for (int i = 0; i < count; i++)
            Expire(RndSpawnInRange(npcId, 10, 20), liveSeconds);
    }
}
