using System;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Players;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils.Stats;
using Aion.GameServer.World;

namespace Aion.GameServer.Controllers.Movement;

/// <summary>Java parity: controllers/movement/PlayerMoveController (ATracer) : PlayableMoveController&lt;Player&gt;. Fall-damage + last-client-position tracking. Nested SM_ATTACK_STATUS.TYPE/LOG enums qualified; currentTimeMillis→UtcNow.ToUnixTimeMilliseconds; super→base. Base members (owner/getMovementMask/updateLastMove)/WorldPosition/StatFunctions red-tolerated.</summary>
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
        StopFalling(owner.GetZ());
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
        if (lastPositionFromClient == null || lastPositionFromClient.GetMapId() != owner.GetWorldId())
            lastPositionFromClient = new WorldPosition(owner.GetWorldId(), owner.GetX(), owner.GetY(), owner.GetZ(), owner.GetHeading());
        else
            lastPositionFromClient.SetXYZH(owner.GetX(), owner.GetY(), owner.GetZ(), owner.GetHeading());
    }

    public void ResetToLastPositionFromClient()
    {
        AbortMove();
        if (lastPositionFromClient != null && owner.GetWorldId() == lastPositionFromClient.GetMapId())
            owner.GetPosition().SetXYZH(lastPositionFromClient.GetX(), lastPositionFromClient.GetY(), lastPositionFromClient.GetZ(),
                lastPositionFromClient.GetHeading());
    }

    public void UpdateFalling(float newZ)
    {
        if (lastFallZ != 0)
        {
            fallDistance += lastFallZ - newZ;
            if (fallDistance >= FallDamageConfig.MAXIMUM_DISTANCE_MIDAIR && owner.GetController().Die(SM_ATTACK_STATUS.TYPE.FALL_DAMAGE, SM_ATTACK_STATUS.LOG.REGULAR, owner))
            {
                PlayerReviveService.ScheduleReviveAtBase(owner, 1000, 0);
                return;
            }
        }
        lastFallZ = newZ;
        owner.GetObserveController().NotifyMoveObservers();
    }

    public void StopFalling(float newZ)
    {
        if (lastFallZ == 0)
            return;

        if (!owner.IsFlying() && !owner.IsDead())
        {
            fallDistance += lastFallZ - newZ;
            int damage = StatFunctions.CalculateFallDamage(owner, fallDistance);
            if (damage > 0)
            {
                owner.GetLifeStats().ReduceHp(SM_ATTACK_STATUS.TYPE.FALL_DAMAGE, damage, 0, SM_ATTACK_STATUS.LOG.REGULAR, owner);
                owner.GetObserveController().NotifyAttackedObservers(owner, 0);
            }
        }
        fallDistance = 0;
        lastFallZ = 0;
        owner.GetObserveController().NotifyMoveObservers();
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
