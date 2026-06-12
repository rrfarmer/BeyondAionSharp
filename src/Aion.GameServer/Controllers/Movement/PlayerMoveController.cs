using System;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Players;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils.Stats;
using Aion.GameServer.World;
using SM_ATTACK_STATUS = Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus;

namespace Aion.GameServer.Controllers.Movement;

/// <summary>Java parity: controllers/movement/PlayerMoveController (ATracer) : PlayableMoveController&lt;Player&gt;. Fall-damage + last-client-position tracking. Nested SmAttackStatus.TYPE/LOG enums qualified; currentTimeMillis→UtcNow.ToUnixTimeMilliseconds; super→base. Base members (Owner/getMovementMask/updateLastMove)/WorldPosition/StatFunctions red-tolerated.</summary>
public class PlayerMoveController : PlayableMoveController<Player>
{
    private float fallDistance;
    private float lastFallZ;
    private byte lastMovementMask;
    private long lastPositionFromClientMillis;
    private WorldPosition lastPositionFromClient;
    private long lastRandomMoveLocEffectTimeMillis;

    public PlayerMoveController(Player owner) : base(owner)
    {
    }

    public override void AbortMove()
    {
        base.AbortMove();
        StopFalling(Owner.GetZ());
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
        lastPositionFromClientMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (lastPositionFromClient == null || lastPositionFromClient.GetMapId() != Owner.GetWorldId())
            lastPositionFromClient = new WorldPosition(Owner.GetWorldId(), Owner.GetX(), Owner.GetY(), Owner.GetZ(), Owner.GetHeading());
        else
            lastPositionFromClient.SetXYZH(Owner.GetX(), Owner.GetY(), Owner.GetZ(), Owner.GetHeading());
    }

    public void ResetToLastPositionFromClient()
    {
        AbortMove();
        if (lastPositionFromClient != null && Owner.GetWorldId() == lastPositionFromClient.GetMapId())
            Owner.GetPosition().SetXYZH(lastPositionFromClient.GetX(), lastPositionFromClient.GetY(), lastPositionFromClient.GetZ(),
                lastPositionFromClient.GetHeading());
    }

    public void UpdateFalling(float newZ)
    {
        if (lastFallZ != 0)
        {
            fallDistance += lastFallZ - newZ;
            if (fallDistance >= FallDamageConfig.MAXIMUM_DISTANCE_MIDAIR && Owner.GetController().Die(SmAttackStatus.TYPE.FALL_DAMAGE, SmAttackStatus.LOG.REGULAR, Owner))
            {
                PlayerReviveService.ScheduleReviveAtBase(Owner, 1000, 0);
                return;
            }
        }
        lastFallZ = newZ;
        Owner.GetObserveController().NotifyMoveObservers();
    }

    public void StopFalling(float newZ)
    {
        if (lastFallZ == 0)
            return;

        if (!Owner.IsFlying() && !Owner.IsDead())
        {
            fallDistance += lastFallZ - newZ;
            int damage = StatFunctions.CalculateFallDamage(Owner, fallDistance);
            if (damage > 0)
            {
                Owner.GetLifeStats().ReduceHp(SmAttackStatus.TYPE.FALL_DAMAGE, damage, 0, SmAttackStatus.LOG.REGULAR, Owner);
                Owner.GetObserveController().NotifyAttackedObservers(Owner, 0);
            }
        }
        fallDistance = 0;
        lastFallZ = 0;
        Owner.GetObserveController().NotifyMoveObservers();
    }

    public void SetHasMovedByRandomMoveLocEffect(Skill skill)
    {
        // delayMillis is required because instant skills (like Power: Emergency Teleport I) are not scheduled with hitTime in endCast
        int delayMillis = skill.IsInstantSkill() ? skill.GetHitTime() : 0;
        this.lastRandomMoveLocEffectTimeMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + delayMillis;
    }

    public bool HasMovedByRandomMoveLocEffect()
    {
        return lastRandomMoveLocEffectTimeMillis != 0 && DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - lastRandomMoveLocEffectTimeMillis < 300;
    }
}
