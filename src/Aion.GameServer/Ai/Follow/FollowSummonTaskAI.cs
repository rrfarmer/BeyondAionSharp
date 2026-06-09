using Aion.GameServer.Ai.Event;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Summons;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Ai.Follow;

/// <summary>Java parity: ai/follow/FollowSummonTaskAI (xTz). Java implements Runnable → Run().</summary>
public class FollowSummonTaskAI
{
    private Creature target;
    private Summon summon;
    private Aion.GameServer.Model.GameObjects.Player.Player master;
    private float targetX;
    private float targetY;
    private float targetZ;

    public FollowSummonTaskAI(Creature target, Summon summon)
    {
        this.target = target;
        this.summon = summon;
        this.master = summon.GetMaster();
        SetLeadingCoordinates();
    }

    private void SetLeadingCoordinates()
    {
        targetX = target.GetX();
        targetY = target.GetY();
        targetZ = target.GetZ();
    }

    public void Run()
    {
        if (!IsInMasterRange())
        {
            Aion.GameServer.Services.Summons.SummonsService.DoMode(SummonMode.RELEASE, summon, UnsummonType.DISTANCE);
            return;
        }
        if (!IsInTargetRange())
        {
            if (targetX != target.GetX() || targetY != target.GetY() || targetZ != target.GetZ())
            {
                SetLeadingCoordinates();
                OnOutOfTargetRange();
            }
        }
        else if (!master.Equals(target))
        {
            OnDestination();
        }
    }

    private bool IsInTargetRange()
    {
        return PositionUtil.IsInRange(target, summon, 2, false);
    }

    private bool IsInMasterRange()
    {
        return PositionUtil.IsInRange(master, summon, 50);
    }

    protected virtual void OnDestination()
    {
        summon.GetAi().OnCreatureEvent(AiEventType.Attack, target);
    }

    private void OnOutOfTargetRange()
    {
        summon.GetAi().OnGeneralEvent(AiEventType.MoveValidate);
    }
}
