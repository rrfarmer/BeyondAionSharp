using System.Threading;

using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/nightmareCircus/YumeAI (@author Ritsu).</summary>
[AIName("yume")]
public class YumeAI : GeneralNpcAI
{
    private readonly AtomicBoolean isStart = new AtomicBoolean(false);
    private ScheduledTask skillTask;

    public YumeAI(Npc owner)
        : base(owner)
    {
    }

    public override bool CanThink()
    {
        return false;
    }

    protected override void HandleDialogStart(Player player)
    {
    }

    public override void OnEffectApplied(Effect effect)
    {
        switch (effect.GetSkillId())
        {
            case 21463:
            case 21465:
                AIActions.UseSkill(this, 21467);
                break;
        }
    }

    protected override void HandleCreatureMoved(Creature creature)
    {
        if (creature is Player p)
        {
            if (isStart.CompareAndSet(false, true))
            {
                skillTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(ct =>
                {
                    if (p.GetLifeStats().GetHpPercentage() < 100)
                    {
                        if (Rnd.NextBoolean())
                        {
                            PacketSendUtility.BroadcastMessage(GetOwner(), 1501126);
                        }
                        AIActions.UseSkill(this, 21466);
                    }
                    return System.Threading.Tasks.ValueTask.CompletedTask;
                }, System.TimeSpan.FromMilliseconds(15000), System.TimeSpan.FromMilliseconds(15000));
            }
        }
    }

    private void CancelTask()
    {
        if (skillTask != null && !skillTask.IsCancelled)
        {
            skillTask.Cancel(true);
        }
    }

    protected override void HandleDespawned()
    {
        CancelTask();
        base.HandleDespawned();
    }
}
