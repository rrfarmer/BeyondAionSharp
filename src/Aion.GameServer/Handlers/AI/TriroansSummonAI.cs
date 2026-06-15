using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/theobomosLab/TriroansSummonAI (Ritsu).</summary>
[AIName("triroan_summon")]
public class TriroansSummonAI : AggressiveNpcAI
{
    private readonly AtomicBoolean isDestroyed = new AtomicBoolean(false);
    private int walkPosition;
    private int helperSkill;
    private readonly object syncLock = new object();

    public TriroansSummonAI(Npc owner)
        : base(owner)
    {
    }

    public override bool CanThink()
    {
        return false;
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        switch (GetNpcId())
        {
            case 280975:
                walkPosition = 3;
                helperSkill = 18493;
                break;
            case 280976:
                walkPosition = 4;
                helperSkill = 18492;
                break;
            case 280977:
                walkPosition = 2;
                helperSkill = 18485;
                break;
            case 280978:
                walkPosition = 5;
                helperSkill = 18491;
                break;
        }
    }

    protected override void HandleMoveArrived()
    {
        base.HandleMoveArrived();
        int point = GetOwner().GetMoveController().GetCurrentStep().GetStepIndex();
        if (walkPosition == point)
        {
            if (isDestroyed.CompareAndSet(false, true))
            {
                GetSpawnTemplate().SetWalkerId(null);
                WalkManager.StopWalking(this);
                UseSkill();
                StartDespawnTask();
            }
        }
    }

    private void UseSkill()
    {
        lock (syncLock)
        {
            Npc boss = GetPosition().GetWorldMapInstance().GetNpc(214669);
            if (boss != null && CheckLocation(GetOwner()) && !boss.IsDead())
            {
                SkillEngine.SkillEngine.GetInstance().GetSkill(boss, helperSkill, 50, boss).UseSkill();
            }
            else
            {
                CheckSkillUse(boss);
            }
        }
    }

    private void CheckSkillUse(Npc boss)
    {
        if (boss != null && CheckDistance() == 0 && !boss.IsDead())
        {
            if (!boss.IsCasting())
            {
                SkillEngine.SkillEngine.GetInstance().GetSkill(boss, helperSkill, 50, boss).UseSkill();
            }
            else
            {
                ThreadPoolManager.GetInstance().Schedule(_ =>
                {
                    if (CheckDistance() == 0 && !boss.IsDead())
                        CheckSkillUse(boss);
                    return ValueTask.CompletedTask;
                }, System.TimeSpan.FromMilliseconds(5000));
            }
        }
    }

    private bool CheckLocation(Npc npc)
    {
        if (CheckDistance() == 1 && npc.GetNpcId() == 280975)
            return true;
        else if (CheckDistance() == 2 && npc.GetNpcId() == 280976)
            return true;
        else if (CheckDistance() == 3 && npc.GetNpcId() == 280977)
            return true;
        else if (CheckDistance() == 4 && npc.GetNpcId() == 280978)
            return true;
        else
            return false;
    }

    public int CheckDistance()
    {
        Npc boss = GetPosition().GetWorldMapInstance().GetNpc(214669);
        if (PositionUtil.GetDistance(boss, 624.002f, 474.241f, 196.160f) <= 5)
            return 1;
        else if (PositionUtil.GetDistance(boss, 623.23f, 502.715f, 196.087f) <= 5)
            return 2;
        else if (PositionUtil.GetDistance(boss, 579.943f, 500.999f, 196.604f) <= 5)
            return 3;
        else if (PositionUtil.GetDistance(boss, 578.323f, 475.784f, 196.463f) <= 5)
            return 4;
        else
            return 0;
    }

    private void StartDespawnTask()
    {
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            AIActions.DeleteOwner(this);
            return ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(3000));
    }
}
