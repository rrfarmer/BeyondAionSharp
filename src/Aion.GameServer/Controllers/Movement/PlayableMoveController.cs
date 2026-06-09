using System;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Container;

namespace Aion.GameServer.Controllers.Movement;

/// <summary>Java parity: controllers/movement/PlayableMoveController (ATracer) — base class for summon &amp; player move controller.</summary>
public abstract class PlayableMoveController<T> : CreatureMoveController<T> where T : Creature
{
    private bool sendMovePacket = true;
    private MovementModifierDirection movementModifierDirection = MovementModifierDirection.NONE;

    public float vehicleX;
    public float vehicleY;
    public float vehicleZ;

    public float vectorX;
    public float vectorY;
    public float vectorZ;
    public byte glideFlag;
    public int unk1;
    public int unk2;
    public int geyserLocationId; // locationId from windstreams.xml

    public PlayableMoveController(T owner)
        : base(owner)
    {
    }

    public override void StartMovingToDestination()
    {
        UpdateLastMove();
        if (Owner.CanPerformMove())
        {
            if (IsControlled() && Started.CompareAndSet(false, true))
            {
                this.MovementMaskField = MovementMask.NpcStartMove;
                SendForcedMovePacket();
                Aion.GameServer.Taskmanager.Tasks.PlayerMoveTaskManager.GetInstance().AddPlayer(Owner);
            }
        }
    }

    private bool IsControlled()
    {
        return Owner.GetEffectController().IsUnderFear() || Owner.GetEffectController().IsConfused();
    }

    private void SendForcedMovePacket()
    {
        Aion.GameServer.Utils.PacketSendUtility.BroadcastPacketAndReceive(Owner, new Aion.GameServer.Network.Aion.ServerPackets.SmMove(Owner));
        sendMovePacket = false;
    }

    public override void MoveToDestination()
    {
        if (!Owner.CanPerformMove())
        {
            if (Started.CompareAndSet(true, false))
            {
                SetAndSendStopMove(Owner);
                UpdateLastMove();
            }
            return;
        }

        if (sendMovePacket && IsControlled())
        {
            SendForcedMovePacket();
        }

        float x = Owner.GetX();
        float y = Owner.GetY();
        float z = Owner.GetZ();

        float dist = (float)Aion.GameServer.Utils.PositionUtil.GetDistance(x, y, z, TargetDestX, TargetDestY, TargetDestZ);
        if (dist < 0.01f)
            return;

        float currentSpeed = Aion.GameServer.Utils.Stats.StatFunctions.AdjustStatByMovementModifier(Owner, StatEnum.SPEED, Owner.GetGameStats().GetMovementSpeedFloat());
        long msElapsed = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - LastMoveUpdate;
        float futureXYDistPassed = Math.Min(currentSpeed * msElapsed / 1000f, dist);
        float futureZDistPassed = IsJumping() ? Math.Min(2 * msElapsed / 1000f, dist) : futureXYDistPassed;

        float distXYFraction = futureXYDistPassed / dist;
        float distZFraction = IsJumping() ? futureZDistPassed / dist : distXYFraction;
        float newX = (TargetDestX - x) * distXYFraction + x;
        float newY = (TargetDestY - y) * distXYFraction + y;
        float newZ = (TargetDestZ - z) * distZFraction + z;

        /*
         * if ((movementMask & MovementMask.MOUSE) == 0) { targetDestX = newX + vectorX; targetDestY = newY + vectorY; targetDestZ = newZ + vectorZ; }
         */

        Aion.GameServer.World.World.GetInstance().UpdatePosition(Owner, newX, newY, newZ, Heading, false);
        UpdateLastMove();
    }

    public override void AbortMove()
    {
        Started.Set(false);
        Aion.GameServer.Taskmanager.Tasks.PlayerMoveTaskManager.GetInstance().RemovePlayer(Owner);
        TargetDestX = 0;
        TargetDestY = 0;
        TargetDestZ = 0;
        SetAndSendStopMove(Owner);
    }

    protected override void SetNewDirection(float x, float y, float z)
    {
        if (TargetDestX != x || TargetDestY != y || TargetDestZ != z)
        {
            sendMovePacket = true;
        }
        base.SetNewDirection(x, y, z);

        float relativeMovementAngle = Aion.GameServer.Utils.PositionUtil.CalculateAngleTowards(Owner.GetX(), Owner.GetY(), Heading, TargetDestX, TargetDestY);
        if (relativeMovementAngle >= -67.5 && relativeMovementAngle <= 67.5)
            movementModifierDirection = MovementModifierDirection.FORWARD;
        else if (relativeMovementAngle <= -112.5 || relativeMovementAngle >= 112.5)
            movementModifierDirection = MovementModifierDirection.BACKWARD;
        else
            movementModifierDirection = MovementModifierDirection.SIDEWAYS;
    }

    public MovementModifierDirection GetMovementDirection()
    {
        if (!IsInMove() && DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - LastMoveUpdate > 1000)
            return MovementModifierDirection.NONE;
        return movementModifierDirection;
    }

    public enum MovementModifierDirection
    {
        NONE,
        FORWARD,
        SIDEWAYS,
        BACKWARD
    }
}
