using System;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Players;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Stats;
using Aion.GameServer.World;
using Aion.GameServer.World.Geo;
using SM_ATTACK_STATUS = Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus;

namespace Aion.GameServer.Controllers.Movement;

/// <summary>Java parity: controllers/movement/PlayerMoveController (ATracer) : PlayableMoveController&lt;Player&gt;. Fall-damage + last-client-position tracking. Nested SmAttackStatus.TYPE/LOG enums qualified; currentTimeMillis→SystemClock.CurrentMillis; super→base. Base members (Owner/getMovementMask/updateLastMove)/WorldPosition/StatFunctions red-tolerated.</summary>
public class PlayerMoveController : PlayableMoveController<Player>
{
    /// <summary>
    /// Heights in meters: no damage up to the first, death from the second on. A fall in mid air with no ground near enough below is lethal
    /// past the third, or the fourth in fly zones.
    /// </summary>
    private const float MIN_FALL_DAMAGE_HEIGHT = 10, LETHAL_FALL_HEIGHT = 50, MAX_MIDAIR_FALL_HEIGHT = 50, MAX_MIDAIR_FALL_HEIGHT_IN_FLY_ZONE = 100;

    private float fallStartZ = float.NaN;
    private float lastZ = float.NaN;
    private byte lastMovementMask;
    private long lastPositionFromClientMillis;
    private WorldPosition lastPositionFromClient;
    private long lastRandomMoveLocEffectTimeMillis;

    public PlayerMoveController(Player owner) : base(owner)
    {
    }

    public byte GetLastMovementMask()
    {
        return lastMovementMask;
    }

    public long GetLastPositionFromClientMillis()
    {
        return lastPositionFromClientMillis;
    }

    public WorldPosition GetLastPositionFromClient()
    {
        return lastPositionFromClient;
    }

    public void ResetLastPositionFromClient()
    {
        lastPositionFromClient = null;
    }

    /// <summary>This method should only be called from player move packets, not any calculated intermediate position updates by the server.</summary>
    public void OnMoveFromClient()
    {
        UpdateLastMove();
        lastMovementMask = GetMovementMask();
        lastPositionFromClientMillis = SystemClock.CurrentMillis();
        if (lastPositionFromClient == null || lastPositionFromClient.GetMapId() != Owner.GetWorldId())
            lastPositionFromClient = new WorldPosition(Owner.GetWorldId(), Owner.GetX(), Owner.GetY(), Owner.GetZ(), Owner.GetHeading());
        else
            lastPositionFromClient.SetXYZH(Owner.GetX(), Owner.GetY(), Owner.GetZ(), Owner.GetHeading());
        UpdateMovementModifiers();
    }

    private void UpdateMovementModifiers()
    {
        switch (GetMoveRequest(GetMovementMask()))
        {
            case MoveRequest.DIRECTION:
                UpdateMovementModifierDirection();
                break;
            case MoveRequest.POINT:
                SetMovementModifierDirection(MovementModifierDirection.FORWARD);
                break;
            case MoveRequest.STOP:
                OnMovementStopped();
                break;
        }
    }

    internal static MoveRequest GetMoveRequest(byte movementMask)
    {
        if ((movementMask & MovementMask.POSITION) == MovementMask.POSITION && (movementMask & MovementMask.MANUAL) == MovementMask.MANUAL)
            return (movementMask & MovementMask.ABSOLUTE) == MovementMask.ABSOLUTE ? MoveRequest.POINT : MoveRequest.DIRECTION;
        if ((movementMask & MovementMask.POSITION) == 0 && (movementMask & MovementMask.FALL) == 0)
            return MoveRequest.STOP;
        return MoveRequest.CONTINUE;
    }

    internal enum MoveRequest
    {
        DIRECTION,
        POINT,
        CONTINUE,
        STOP
    }

    public void ResetToLastPositionFromClient()
    {
        AbortMove();
        if (lastPositionFromClient != null && Owner.GetWorldId() == lastPositionFromClient.GetMapId())
            Owner.GetPosition().SetXYZH(lastPositionFromClient.GetX(), lastPositionFromClient.GetY(), lastPositionFromClient.GetZ(),
                lastPositionFromClient.GetHeading());
    }

    /// <summary>
    /// Called for move packets in which the player falls. A fall packet that already touches the ground counts as landing.
    /// </summary>
    public void UpdateFalling(float newZ)
    {
        if (!float.IsNaN(GeoService.GetInstance().GetZ(Owner, newZ + 0.3f, newZ - 0.3f)))
        {
            StopFalling(newZ);
            return;
        }
        if (IsBelowWorld(newZ))
        {
            DieFromFall();
            return;
        }
        if (!float.IsNaN(fallStartZ) && newZ < lastZ)
        {
            float height = fallStartZ - newZ;
            float maxHeight = Owner.IsInsideFlyZone() ? MAX_MIDAIR_FALL_HEIGHT_IN_FLY_ZONE : MAX_MIDAIR_FALL_HEIGHT;
            if (height > maxHeight && float.IsNaN(GeoService.GetInstance().GetWalkableZ(Owner, newZ, newZ - 2 * maxHeight)))
            {
                DieFromFall();
                return;
            }
        }
        lastZ = newZ;
        Owner.GetObserveController().NotifyMoveObservers();
    }

    /// <summary>
    /// Called for all other move packets: the player stands on the ground, flies or glides.
    /// </summary>
    public void StopFalling(float newZ)
    {
        if (IsBelowWorld(newZ))
        {
            DieFromFall();
            return;
        }
        float height = fallStartZ - newZ;
        if (!Owner.IsFlying() && height > MIN_FALL_DAMAGE_HEIGHT)
        {
            if (height >= LETHAL_FALL_HEIGHT)
            {
                DieFromFall();
                return;
            }
            if (!Owner.IsInSprintMode())
            {
                Owner.GetLifeStats().ReduceHp(SmAttackStatus.TYPE.FALL_DAMAGE, StatFunctions.CalculateFallDamage(Owner, height), 0, SmAttackStatus.LOG.REGULAR, Owner);
                Owner.GetObserveController().NotifyAttackedObservers(Owner, 0);
            }
        }
        fallStartZ = newZ;
        lastZ = newZ;
        Owner.GetObserveController().NotifyMoveObservers();
    }

    /// <summary>
    /// Lets the next fall start at the given height, as after leaving a windstream or landing from free flight.
    /// </summary>
    public void RestartFalling(float z)
    {
        fallStartZ = z;
        lastZ = z;
    }

    /// <summary>
    /// Forgets where the current fall started, so a teleport or a landing from a flight path doesn't count as one.
    /// </summary>
    public void ResetFalling()
    {
        fallStartZ = float.NaN;
        lastZ = float.NaN;
    }

    /// <summary>True if the player is more than 2 m below z 0.</summary>
    private static bool IsBelowWorld(float z)
    {
        return z < -2;
    }

    private void DieFromFall()
    {
        ResetFalling();
        if (Owner.IsDead())
            return;
        if (Owner.GetController().Die(SmAttackStatus.TYPE.FALL_DAMAGE, SmAttackStatus.LOG.REGULAR, Owner))
            PlayerReviveService.BindRevive(Owner);
    }

    public void SetHasMovedByRandomMoveLocEffect(Skill skill)
    {
        // delayMillis is required because instant skills (like Power: Emergency Teleport I) are not scheduled with hitTime in endCast
        int delayMillis = skill.IsInstantSkill() ? skill.GetHitTime() : 0;
        this.lastRandomMoveLocEffectTimeMillis = SystemClock.CurrentMillis() + delayMillis;
    }

    public bool HasMovedByRandomMoveLocEffect()
    {
        return lastRandomMoveLocEffectTimeMillis != 0 && SystemClock.CurrentMillis() - lastRandomMoveLocEffectTimeMillis < 300;
    }
}
