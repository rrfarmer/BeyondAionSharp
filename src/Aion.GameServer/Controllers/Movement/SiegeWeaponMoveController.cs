using System;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Controllers.Movement;

/// <summary>Java parity: controllers/movement/SiegeWeaponMoveController (xTz).</summary>
public class SiegeWeaponMoveController : SummonMoveController
{
    private float pointX;
    private float pointY;
    private float pointZ;

    public SiegeWeaponMoveController(Summon owner)
        : base(owner)
    {
    }

    /// <returns>if destination reached</returns>
    public override void MoveToDestination()
    {
        if (!Owner.CanPerformMove() || (Owner.GetAi().GetSubState() == Aion.GameServer.Ai.AISubState.CAST))
        {
            if (Started.CompareAndSet(true, false))
            {
                SetAndSendStopMove(Owner);
                UpdateLastMove();
            }
            return;
        }
        else if (Started.CompareAndSet(false, true))
        {
            UpdateLastMove();
            SetAndSendStartMove(Owner);
        }

        VisibleObject target = Owner.GetTarget();
        if (target != null) // update target position, in case target moved
        {
            pointX = target.GetX();
            pointY = target.GetY();
            pointZ = target.GetZ();
        }
        MoveToLocation(pointX, pointY, pointZ);
        UpdateLastMove();
    }

    public override void MoveToTargetObject()
    {
        UpdateLastMove();
        Aion.GameServer.Taskmanager.Tasks.MoveTaskManager.GetInstance().AddCreature(Owner);
    }

    public override void AbortMove()
    {
        base.AbortMove();
        Aion.GameServer.Taskmanager.Tasks.MoveTaskManager.GetInstance().RemoveCreature(Owner);
    }

    protected void MoveToLocation(float targetX, float targetY, float targetZ)
    {
        bool destinationChanged = targetX != TargetDestX || targetY != TargetDestY || targetZ != TargetDestZ;
        float ownerX = Owner.GetX();
        float ownerY = Owner.GetY();
        float ownerZ = Owner.GetZ();

        if (targetX != TargetDestX || targetY != TargetDestY)
        {
            Heading = Aion.GameServer.Utils.PositionUtil.GetHeadingTowards(ownerX, ownerY, targetX, targetY);
        }

        TargetDestX = targetX;
        TargetDestY = targetY;
        TargetDestZ = targetZ;

        float currentSpeed = Owner.GetGameStats().GetMovementSpeedFloat();
        float futureDistPassed = currentSpeed * (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - LastMoveUpdate) / 1000f;

        float dist = (float)Aion.GameServer.Utils.PositionUtil.GetDistance(ownerX, ownerY, ownerZ, targetX, targetY, targetZ);

        if (dist == 0)
        {
            return;
        }

        if (futureDistPassed > dist)
        {
            futureDistPassed = dist;
        }

        float distFraction = futureDistPassed / dist;
        float newX = (TargetDestX - ownerX) * distFraction + ownerX;
        float newY = (TargetDestY - ownerY) * distFraction + ownerY;
        float newZ = (TargetDestZ - ownerZ) * distFraction + ownerZ;
        Aion.GameServer.World.World.GetInstance().UpdatePosition(Owner, newX, newY, newZ, Heading, true);
        if (destinationChanged)
        {
            MovementMaskField = MovementMask.NPC_STARTMOVE;
            Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(Owner, new Aion.GameServer.Network.Aion.ServerPackets.SmMove(Owner));
        }
    }
}
