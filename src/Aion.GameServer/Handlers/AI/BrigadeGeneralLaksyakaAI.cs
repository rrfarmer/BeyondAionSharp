using System.Collections.Generic;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/tiamatStrongHold/BrigadeGeneralLaksyakaAI (@author Cheatkiller).</summary>
[AIName("brigadegenerallaksyaka")]
public class BrigadeGeneralLaksyakaAI : AggressiveNpcAI
{
    private readonly AtomicBoolean isHome = new AtomicBoolean(true);
    private ScheduledTask? skeletonTask;
    private bool isFinalBuff;

    public BrigadeGeneralLaksyakaAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        if (isHome.CompareAndSet(true, false))
            StartSkillTask();
        if (!isFinalBuff && GetOwner().GetLifeStats().GetHpPercentage() <= RagePercent)
        {
            isFinalBuff = true;
            AIActions.UseSkill(this, 20731);
        }
    }

    /// <summary>Retail's <c>BTIMERI_INDEX_0</c>: sixteen seconds, and sixteen again after every turn.</summary>
    private static readonly System.TimeSpan EyeFirst = System.TimeSpan.FromSeconds(16);
    private static readonly System.TimeSpan EyeRepeat = System.TimeSpan.FromSeconds(16);

    /// <summary>Retail's <c>BTIMERI_INDEX_1</c>: fifteen seconds to the first wave, twenty between.</summary>
    private static readonly System.TimeSpan SkeletonFirst = System.TimeSpan.FromSeconds(15);
    private static readonly System.TimeSpan SkeletonRepeat = System.TimeSpan.FromSeconds(20);

    /// <summary>Retail's <c>is_hp_in_boundary larger_than=15</c> on both rungs, and its rage percent.</summary>
    private const int FloorPercent = 15;
    private const int RagePercent = 15;

    /// <summary>Retail's <c>num_to_spawn</c> and <c>spawn_range</c> on the skeleton wave.</summary>
    private const int SkeletonCount = 4;
    private const float SkeletonSpread = 7f;

    private ScheduledTask? eyeTask;

    private void StartSkillTask()
    {
        eyeTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            if (IsDead())
                CancelTask();
            else
                StartSkeletonEvent();
            return ValueTask.CompletedTask;
        }, EyeFirst, EyeRepeat);

        skeletonTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            if (IsDead())
                CancelTask();
            else
                SpawnSummon();
            return ValueTask.CompletedTask;
        }, SkeletonFirst, SkeletonRepeat);
    }

    private void CancelTask()
    {
        if (skeletonTask != null && !skeletonTask.IsCancelled)
        {
            skeletonTask.Cancel(true);
        }

        if (eyeTask != null && !eyeTask.IsCancelled)
        {
            eyeTask.Cancel(true);
        }
    }

    /// <summary>
    /// Retail's <c>BTIMERI_INDEX_0</c> rung: broadcast 100, which is what makes Tiamat's Eye fire.
    /// </summary>
    /// <remarks>
    /// Misnamed since the port -- it has nothing to do with skeletons. The eye's own pattern answers
    /// message 100 with a cast; this port applies the effect directly instead, which is the same thing
    /// said in its own terms.
    /// </remarks>
    private void StartSkeletonEvent()
    {
        if (GetLifeStats().GetHpPercentage() <= FloorPercent)
            return;

        Npc tiamatEye = GetPosition().GetWorldMapInstance().GetNpc(283089); // 4.0
        List<Player> players = new List<Player>();
        GetKnownList().ForEachPlayer(player =>
        {
            if (!player.IsDead() && PositionUtil.IsInRange(player, tiamatEye, 40))
            {
                players.Add(player);
            }
        });
        if (players.Count != 0)
        {
            Player player = Rnd.Get(players);
            SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(20865, tiamatEye, player);
        }
    }

    /// <summary>
    /// Retail <c>IDTiamat_Rakshaka</c> gives <c>IDTiamat_Rakshaka_Skeleton</c> twenty seconds.
    /// </summary>
    /// <remarks>
    /// They had none, so every wave of four stayed for the whole fight.
    /// </remarks>
    private const int SkeletonLife = 20;

    /// <summary>
    /// Retail's <c>BTIMERI_INDEX_1</c> rung: four skeletons at his own feet, inside seven metres.
    /// </summary>
    /// <remarks>
    /// <b>This hung off a three per cent roll on every blow he took</b>, so the wave arrived as a
    /// function of how hard he was being hit rather than on a clock — the same defect Kumbanda had, in
    /// the same instance. There is no "only if none are standing" guard either: retail's rung has none,
    /// and at twenty seconds between waves of twenty-second skeletons it does not need one.
    /// </remarks>
    private void SpawnSummon()
    {
        if (GetLifeStats().GetHpPercentage() <= FloorPercent)
            return;

        RndSpawn(283115, SkeletonCount);
    }

    private void RndSpawn(int npcId, int count)
    {
        for (int i = 0; i < count; i++)
        {
            Expire(RndSpawnInRange(npcId, SkeletonSpread), SkeletonLife);
        }
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        CancelTask();
    }

    protected override void HandleBeforeSpawned()
    {
        base.HandleBeforeSpawned();
        GetOwner().OverrideNpcType(CreatureType.PEACE);
    }

    protected override void HandleDespawned()
    {
        base.HandleDespawned();
        CancelTask();
    }

    protected override void HandleBackHome()
    {
        base.HandleBackHome();
        CancelTask();
        isFinalBuff = false;
        isHome.Set(true);
    }
}
