using System.Threading;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Controllers.Movement;

/// <summary>
/// Base movement controller for a creature/visible object.
/// Java parity: controllers/movement/CreatureMoveController&lt;T extends VisibleObject&gt;.
/// </summary>
public abstract class CreatureMoveController
{
    public const float MOVE_CHECK_OFFSET = 0.1f;

    protected VisibleObject Owner;
    protected byte Heading;
    protected long LastMoveUpdate = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    protected bool IsInMoveField;
    protected readonly AtomicBoolean Started = new(false);

    public byte MovementMaskField;
    protected float TargetDestX;
    protected float TargetDestY;
    protected float TargetDestZ;
    private bool _isJumping;

    protected CreatureMoveController(VisibleObject owner)
    {
        Owner = owner;
    }

    // Java parity: moveToDestination()
    public virtual void MoveToDestination() { }

    public float GetTargetX2() => TargetDestX;
    public float GetTargetY2() => TargetDestY;
    public float GetTargetZ2() => TargetDestZ;

    // Java parity: setNewDirection(float, float, float, byte)
    public void SetNewDirection(float x, float y, float z, byte heading)
    {
        Heading = heading;
        SetNewDirection(x, y, z);
    }

    // Java parity: protected setNewDirection(float, float, float)
    protected virtual void SetNewDirection(float x, float y, float z)
    {
        TargetDestX = x;
        TargetDestY = y;
        TargetDestZ = z;
    }

    // Java parity: startMovingToDestination()
    public virtual void StartMovingToDestination() { }

    // Java parity: abortMove()
    public virtual void AbortMove() { }

    // Java parity: protected setAndSendStartMove(Creature)
    protected void SetAndSendStartMove(Creature owner)
    {
        SetInMove(true);
        MovementMaskField = MovementMask.NpcStartMove;
        PacketSendUtility.BroadcastToSightedPlayers(owner, new SmMove(owner));
    }

    // Java parity: protected setAndSendStopMove(Creature)
    protected void SetAndSendStopMove(Creature owner)
    {
        SetInMove(false);
        MovementMaskField = MovementMask.Immediate;
        PacketSendUtility.BroadcastToSightedPlayers(owner, new SmMove(owner));
    }

    // Java parity: final updateLastMove()
    public void UpdateLastMove() => LastMoveUpdate = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public long GetLastMoveUpdate() => LastMoveUpdate;
    public byte GetMovementMask() => MovementMaskField;
    public bool IsJumping() => _isJumping;
    public void SetIsJumping(bool isJumping) => _isJumping = isJumping;
    public bool IsInMove() => IsInMoveField;
    public void SetInMove(bool value) => IsInMoveField = value;
}

/// <summary>
/// Java parity: generic typing of <see cref="CreatureMoveController"/>
/// (Java <c>CreatureMoveController&lt;T extends VisibleObject&gt;</c>). Non-generic base + generic shim so
/// <c>Creature.GetMoveController()</c> returns the non-generic base (no C# wildcard generics).
/// </summary>
public abstract class CreatureMoveController<T> : CreatureMoveController where T : VisibleObject
{
    protected CreatureMoveController(T owner) : base(owner)
    {
    }

    protected new T Owner => (T)base.Owner;
}
