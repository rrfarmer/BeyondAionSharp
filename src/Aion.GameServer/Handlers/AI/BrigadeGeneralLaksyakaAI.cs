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
        if (Rnd.Chance() < 3)
            SpawnSummon();
        if (isHome.CompareAndSet(true, false))
            StartSkillTask();
        if (!isFinalBuff && GetOwner().GetLifeStats().GetHpPercentage() <= 25)
        {
            isFinalBuff = true;
            AIActions.UseSkill(this, 20731);
        }
    }

    private void StartSkillTask()
    {
        skeletonTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            if (IsDead())
                CancelTask();
            else
                StartSkeletonEvent();
            return ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(5000), System.TimeSpan.FromMilliseconds(40000));
    }

    private void CancelTask()
    {
        if (skeletonTask != null && !skeletonTask.IsCancelled)
        {
            skeletonTask.Cancel(true);
        }
    }

    private void StartSkeletonEvent()
    {
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

    private void SpawnSummon()
    {
        if (GetPosition().GetWorldMapInstance().GetNpc(283115) == null) // 4.0
        {
            RndSpawn(283115, 4); // 4.0
        }
    }

    private void RndSpawn(int npcId, int count)
    {
        for (int i = 0; i < count; i++)
        {
            Expire(RndSpawnInRange(npcId, 10), SkeletonLife);
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
