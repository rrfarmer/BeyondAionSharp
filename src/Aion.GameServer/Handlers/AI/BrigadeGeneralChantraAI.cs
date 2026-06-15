using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/tiamatStrongHold/BrigadeGeneralChantraAI (@author Cheatkiller).</summary>
[AIName("brigadegeneralchantra")]
public class BrigadeGeneralChantraAI : AggressiveNpcAI
{
    private readonly AtomicBoolean isHome = new AtomicBoolean(true);
    private ScheduledTask? trapTask;
    private bool isFinalBuff;

    public BrigadeGeneralChantraAI(Npc owner) : base(owner)
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
            AIActions.UseSkill(this, 20942);
        }
    }

    private void StartSkillTask()
    {
        trapTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            if (IsDead())
                CancelTask();
            else
                StartTrapEvent();
            return ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(5000), System.TimeSpan.FromMilliseconds(40000));
    }

    private void CancelTask()
    {
        if (trapTask != null && !trapTask.IsCancelled)
        {
            trapTask.Cancel(true);
        }
    }

    private void StartTrapEvent()
    {
        int[] trapNpcs = { 283092, 283094 }; // 4.0
        int trap = Rnd.Get(trapNpcs);
        if (GetPosition().GetWorldMapInstance().GetNpc(trap) == null)
        {
            Spawn(trap, 1031.1f, 466.38f, 445.45f, (sbyte)0);
            ThreadPoolManager.GetInstance().Schedule(_ =>
            {
                Npc ring = GetPosition().GetWorldMapInstance().GetNpc(trap);
                if (trap == 283092) // 4.0
                    Spawn(283171, 1031.1f, 466.38f, 445.45f, (sbyte)0); // 4.0
                else
                    Spawn(283172, 1031.1f, 466.38f, 445.45f, (sbyte)0); // 4.0
                ring.GetController().Delete();
                return ValueTask.CompletedTask;
            }, 5000L);
        }
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
        isFinalBuff = false;
        isHome.Set(true);
    }
}
