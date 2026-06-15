using System;
using System.Linq;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Java parity: ai/instance/tiamatStrongHold/InvincibleShabokanAI (@author Cheatkiller).
/// </summary>
[AIName("invincibleshabokan")]
public class InvincibleShabokanAI : AggressiveNpcAI
{
    private readonly AtomicBoolean isHome = new AtomicBoolean(true);
    private ScheduledTask? skillTask;
    private bool isFinalBuff;

    public InvincibleShabokanAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        if (isHome.CompareAndSet(true, false))
            StartSkillTask();
        if (!isFinalBuff && GetOwner().GetLifeStats().GetHpPercentage() <= 25)
        {
            isFinalBuff = true;
            AIActions.UseSkill(this, 20941);
        }
    }

    private void StartSkillTask()
    {
        skillTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            if (IsDead())
                CancelTask();
            else
                ChooseRandomEvent();
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(5000), TimeSpan.FromMilliseconds(30000));
    }

    private void CancelTask()
    {
        if (skillTask != null && !skillTask.IsCancelled)
        {
            skillTask.Cancel(true);
        }
    }

    private void EarthQuakeEvent()
    {
        Npc invisible = GetPosition().GetWorldMapInstance().GetNpc(283082); // 4.0
        SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 20717, 55, GetOwner()).UseNoAnimationSkill();
        if (invisible == null)
        {
            Spawn(283082, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(), (sbyte)0); // 4.0
        }
    }

    private void SinkEvent()
    {
        SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 20720, 55, GetOwner()).UseNoAnimationSkill();
        foreach (var player in GetKnownList().StreamVisiblePlayers().ToList())
        {
            if (IsInRange(player, 30))
            {
                Spawn(283083, player.GetX(), player.GetY(), player.GetZ(), (sbyte)0); // 4.0
                Spawn(283084, player.GetX(), player.GetY(), player.GetZ(), (sbyte)0); // 4.0
            }
        }
    }

    private void ChooseRandomEvent()
    {
        int rand = Rnd.Get(0, 1);
        if (rand == 0)
            EarthQuakeEvent();
        else
            SinkEvent();
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        CancelTask();
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
        GetOwner().GetEffectController().RemoveEffect(20941);
        isHome.Set(true);
    }
}
