using System.Collections.Generic;
using Aion.GameServer.Controllers.Movement;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Antihack;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_MOVE (-Nemesiss-). Core player-movement packet (position/glide/vehicle masks, jumping, falling, anti-hack, bogus-packet workaround). MovementMask/PlayerMoveController red-tolerated.</summary>
public class CM_MOVE : AionClientPacket
{
    private byte type;
    private byte heading;
    private float x, y, z, x2, y2, z2, vehicleX, vehicleY, vehicleZ, vectorX, vectorY, vectorZ;
    private byte glideFlag;
    private int unk1, unk2;
    private int geyserLocationId;

    public CM_MOVE(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        x = ReadF();
        y = ReadF();
        z = ReadF();

        heading = ReadC();
        type = ReadC();

        if ((type & MovementMask.POSITION) == MovementMask.POSITION && (type & MovementMask.MANUAL) == MovementMask.MANUAL)
        {
            if ((type & MovementMask.ABSOLUTE) == MovementMask.ABSOLUTE)
            {
                x2 = ReadF();
                y2 = ReadF();
                z2 = ReadF();
            }
            else
            {
                vectorX = ReadF();
                vectorY = ReadF();
                vectorZ = ReadF();
                x2 = vectorX + x;
                y2 = vectorY + y;
                z2 = vectorZ + z;
            }
        }
        if ((type & MovementMask.GLIDE) == MovementMask.GLIDE)
        {
            glideFlag = ReadC();
            if (glideFlag == GlideFlag.GEYSER)
                geyserLocationId = ReadUC(); // locationId from windstreams.xml
        }
        if ((type & MovementMask.VEHICLE) == MovementMask.VEHICLE)
        {
            unk1 = ReadD();
            unk2 = ReadD();
            vehicleX = ReadF();
            vehicleY = ReadF();
            vehicleZ = ReadF();
        }
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        if (player.IsDead() || player.GetEffectController().IsUnderFear() || player.GetEffectController().IsConfused()) // just in case of bad timing
            return;
        if (HandleBogusPacket(player))
            return;

        PlayerMoveController m = player.GetMoveController();
        bool jumping = false;
        byte oldMask = m.movementMask;
        m.movementMask = type;

        if (type == MovementMask.IMMEDIATE)
        { // stopping or turning
            m.SetNewDirection(x, y, z, heading);
        }
        else
        {
            jumping = !player.IsFlying() && (type & MovementMask.POSITION) == MovementMask.POSITION && (type & MovementMask.MANUAL) == MovementMask.MANUAL
                    && (type & MovementMask.ABSOLUTE) != MovementMask.ABSOLUTE && (type & MovementMask.GLIDE) != MovementMask.GLIDE
                    && (type & MovementMask.VEHICLE) != MovementMask.VEHICLE && z2 > z;
            if ((type & MovementMask.GLIDE) == MovementMask.GLIDE)
            {
                m.glideFlag = glideFlag;
                m.geyserLocationId = geyserLocationId;
                player.GetFlyController().SwitchToGliding();
            }

            if ((type & MovementMask.POSITION) == MovementMask.POSITION && (type & MovementMask.MANUAL) == MovementMask.MANUAL)
            { // start move or change direction
                m.SetNewDirection(x2, y2, z2, heading);
                if ((type & MovementMask.ABSOLUTE) == MovementMask.ABSOLUTE)
                {
                    if (player.IsInCustomState(CustomPlayerState.TELEPORTATION_MODE))
                    {
                        player.GetMoveController().SetIsJumping(false);
                        World.GetInstance().UpdatePosition(player, x2, y2, z2, heading);
                        m.OnMoveFromClient();
                        PacketSendUtility.BroadcastToSightedPlayers(player, new SM_MOVE(player), true);
                        return;
                    }
                }
                else
                {
                    m.vectorX = vectorX;
                    m.vectorY = vectorY;
                    m.vectorZ = vectorZ;
                }
            }
            else
            {
                if ((type & MovementMask.ABSOLUTE) == 0)
                {
                    float speed = player.GetGameStats().GetMovementSpeedFloat();
                    m.SetNewDirection(x + m.vectorX * speed, y + m.vectorY * speed, player.IsFlying() ? z + m.vectorZ * speed : z + m.vectorZ, heading);
                }
                else if (heading != player.GetHeading())
                    m.SetNewDirection(m.GetTargetX2(), m.GetTargetY2(), m.GetTargetZ2(), heading);
            }

            if ((type & MovementMask.VEHICLE) == MovementMask.VEHICLE)
            {
                m.unk1 = unk1;
                m.unk2 = unk2;
                m.vehicleX = vehicleX;
                m.vehicleY = vehicleY;
                m.vehicleZ = vehicleZ;
            }
        }

        if (!AntiHackService.CanMove(player, x, y, z, type))
        {
            player.GetMoveController().SetIsJumping(false);
            return;
        }

        if (!player.IsSpawned()) // should be checked as late as possible, to prevent false warnings from World.updatePosition
            return;
        if (player.IsProtectionActive() && (player.GetX() != x || player.GetY() != y || player.GetZ() > z + 0.5f))
            player.GetController().StopProtectionActiveTask();
        player.GetMoveController().SetIsJumping(jumping);
        World.GetInstance().UpdatePosition(player, x, y, z, heading);
        m.OnMoveFromClient();
        NotifyControllers(player, oldMask);

        if ((type & MovementMask.POSITION) == MovementMask.POSITION && (type & MovementMask.MANUAL) == MovementMask.MANUAL
            || type == MovementMask.IMMEDIATE)
            PacketSendUtility.BroadcastToSightedPlayers(player, new SM_MOVE(player));

        if ((type & MovementMask.FALL) == MovementMask.FALL)
        {
            player.GetFlyController().OnStopGliding();
            m.UpdateFalling(z);
        }
        else
        {
            m.StopFalling(z);
        }
    }

    private bool HandleBogusPacket(Player player)
    {
        if (player.IsInCustomState(CustomPlayerState.WATCHING_CUTSCENE)) // client sends crap during cutscenes in transformed state
            return true;
        VisibleObject target = player.GetTarget();
        if (target != null && player.GetMoveController().HasMovedByRandomMoveLocEffect() && PositionUtil.IsInRange(target, x, y, z, 2)
            && !PositionUtil.IsInRange(player, x, y, z, 3))
        {
            /*
             * The game client often sends incorrect coordinates and tries to move you to your target's position when using any RandomMoveLocEffect
             * (Emergency Teleport I, Power: Emergency Teleport I, Blind Leap, Feint, etc.) while:
             * 1) running or jumping around the corner of an obstacle
             * 2) jumping on an obstacle
             * 3) jumping over an obstacle (harder to reproduce with skills that have no animation time)
             * 4) running up/down the upper end of stairs: only works for skills with animation time, animation must either start or end at the top flat level
             * 5) Additionally, teleporting across any type of crest blocking line of sight between the start and end position causes a similar condition.
             * It seems like this happens if the game thinks you have passed through an obstacle while using teleportation skills. Server side positions
             * are not considered for this, it is all evaluated by the client based on local coordinates.
             * Most often incorrect coordinates are contained in the first move packet after SM_CASTSPELL_RESULT, but sometimes it's the second one or,
             * in case of teleportation skills with animation time, sometimes even both. That's when we also see type == 0. Other times, type often has
             * MovementType.FALL but not always (especially if a directional teleport was involved).
             * Sending a move packet with the current server-side position works around this client bug and the client will not move you to your target's
             * position.
             */
            bool moveForcefully = type == 0 || (type & MovementMask.MANUAL) == MovementMask.MANUAL && (type & MovementMask.POSITION) == MovementMask.POSITION;
            SendPacket(moveForcefully ? new SM_FORCED_MOVE(player, player) : new SM_MOVE(player));
            return true;
        }
        return false;
    }

    private void NotifyControllers(Player player, byte oldMovementMask)
    {
        if (player.GetMoveController().GetMovementMask() == MovementMask.IMMEDIATE)
        { // stopping or turning
            if (oldMovementMask == MovementMask.IMMEDIATE) // turning
                player.GetController().OnMove();
            // notify arrived
            player.GetController().OnStopMove();
            player.GetFlyController().OnStopGliding();
        }
        else if ((type & MovementMask.POSITION) == MovementMask.POSITION && (type & MovementMask.MANUAL) == MovementMask.MANUAL
            && !player.GetMoveController().IsInMove())
        { // start move or change direction
            player.GetController().OnStartMove();
        }
        else
        {
            player.GetController().OnMove();
        }
    }

    public override string ToString()
    {
        return "CM_MOVE [type=" + (type & 0xFF) + ", heading=" + heading + ", x=" + x + ", y=" + y + ", z=" + z + ", x2=" + x2 + ", y2=" + y2 + ", z2="
            + z2 + ", vehicleX=" + vehicleX + ", vehicleY=" + vehicleY + ", vehicleZ=" + vehicleZ + ", vectorX=" + vectorX + ", vectorY=" + vectorY
            + ", vectorZ=" + vectorZ + ", glideFlag=" + glideFlag + ", unk1=" + unk1 + ", unk2=" + unk2 + "]";
    }
}
