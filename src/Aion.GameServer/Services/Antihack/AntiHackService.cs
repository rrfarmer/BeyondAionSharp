using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Controllers.Movement;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Skillengine.Effect;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Audit;
using Aion.GameServer.World;

namespace Aion.GameServer.Services.Antihack;

/// <summary>Java parity: services/antihack/AntiHackService (Source). canMove anti-cheat checks: abnormal-state move, speed hack (position/absolute/no-mask vectors vs movement speed + counters), teleport hack; punish (audit log + move-back / disconnect per SecurityConfig.PUNISH), moveBack, checkAionBin (aion.bin size validation). Math.rint->Math.Round (round-half-to-even); currentTimeMillis->UtcNow.ToUnixTimeMilliseconds; public hack-counter fields keep Java names. MovementMask/AbnormalState/SM_ packets red-tolerated.</summary>
public class AntiHackService
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(AntiHackService));

    public static bool CanMove(Player player, float x, float y, float z, byte type)
    {
        PlayerMoveController m = player.GetMoveController();
        WorldPosition lastPositionFromClient = m.GetLastPositionFromClient();
        if (lastPositionFromClient == null || lastPositionFromClient.GetMapId() != player.GetWorldId())
            return true;

        if (SecurityConfig.ABNORMAL)
        {
            if (!player.CanPerformMove() && !player.GetEffectController().IsAbnormalSet(AbnormalState.PULLED)
                && (type & MovementMask.GLIDE) != MovementMask.GLIDE)
            {
                if (player.abnormalHackCounter > SecurityConfig.ABNORMAL_COUNTER)
                {
                    return Punish(player, false, "possibly performed illegal move action (Anti-Abnormal Hack)");
                }
                else
                    player.abnormalHackCounter++;
            }
            else
                player.abnormalHackCounter = 0;
        }

        float speed = player.GetGameStats().GetMovementSpeedFloat();
        if (SecurityConfig.SPEEDHACK)
        {
            if (type != 0)
            {
                if ((type & MovementMask.POSITION) == MovementMask.POSITION)
                {
                    double vector2D = PositionUtil.GetDistance(x, y, m.GetTargetX2(), m.GetTargetY2());

                    if (vector2D != 0)
                    {
                        if ((type & MovementMask.MANUAL) == MovementMask.MANUAL && vector2D > 5 && vector2D > speed + 0.001)
                            player.speedHackCounter++;
                        else if (vector2D > 37.5 && vector2D > 1.5 * speed * speed + 0.001)
                            player.speedHackCounter++;
                        else if (player.speedHackCounter > 0)
                            player.speedHackCounter--;

                        if (player.speedHackCounter > SecurityConfig.SPEEDHACK_COUNTER)
                        {
                            return Punish(player, false, "possibly used speed hack - SHC:" + player.speedHackCounter + " S:" + speed + " V:"
                                + Math.Round(1000.0 * vector2D) / 1000.0 + " type:" + type);
                        }
                    }
                }
                else if ((type & MovementMask.ABSOLUTE) == MovementMask.ABSOLUTE && (type & MovementMask.GLIDE) != MovementMask.GLIDE)
                {
                    double vector = PositionUtil.GetDistance(x, y, lastPositionFromClient.GetX(), lastPositionFromClient.GetY());
                    long timeDiff = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - m.GetLastPositionFromClientMillis();

                    if ((type & MovementMask.POSITION) == MovementMask.POSITION)
                    {
                        bool isMoveToTarget = false;
                        if (player.GetTarget() != null && player.GetTarget() != player)
                        {
                            double distDiff = PositionUtil.GetDistance(player.GetTarget().GetX(), player.GetTarget().GetY(), m.GetTargetX2(), m.GetTargetY2());
                            isMoveToTarget = distDiff <= 5;
                        }

                        if (timeDiff > 1000 && player.speedHackCounter > 0)
                            player.speedHackCounter--;

                        if (vector > timeDiff * (speed + 0.85) * 0.001)
                            player.speedHackCounter++;
                        else if (isMoveToTarget && player.speedHackCounter > 0)
                            player.speedHackCounter--;
                    }
                    else if (vector > timeDiff * (speed + 0.25) * 0.001)
                        player.speedHackCounter++;
                    else if (player.speedHackCounter > 0)
                        player.speedHackCounter--;

                    if (SecurityConfig.PUNISH > 0 && player.speedHackCounter > SecurityConfig.SPEEDHACK_COUNTER + 5)
                    {
                        return Punish(player, false,
                            "possibly used speed hack - SHC:" + player.speedHackCounter + " SMS:" + Math.Round(100.0 * (timeDiff * (speed + 0.25) * 0.001)) / 100.0
                                + " TDF:" + timeDiff + " VTD:" + Math.Round(1000.0 * (timeDiff * (speed + 0.85) * 0.001)) / 1000.0 + " VS:"
                                + Math.Round(100.0 * vector) / 100.0 + " type:" + type);
                    }
                    else if (player.speedHackCounter > SecurityConfig.SPEEDHACK_COUNTER)
                    {
                        MoveBack(player, false);
                        return false;
                    }
                }
            }
            else
            {
                double vector = PositionUtil.GetDistance(x, y, lastPositionFromClient.GetX(), lastPositionFromClient.GetY());
                long timeDiff = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - m.GetLastPositionFromClientMillis();

                if (m.GetLastMovementMask() == 0 && vector > timeDiff * speed * 0.00075)
                    player.speedHackCounter++;

                if (SecurityConfig.PUNISH > 0 && player.speedHackCounter > SecurityConfig.SPEEDHACK_COUNTER + 5)
                {
                    return Punish(player, false, "possibly used speed hack - SHC:" + player.speedHackCounter + " TD:" + Math.Round(1000.0 * timeDiff) / 1000.0
                        + " VTD:" + Math.Round(1000.0 * (timeDiff * speed * 0.00075)) / 1000.0 + " VS:" + Math.Round(100.0 * vector) / 100.0 + " type:" + type);
                }
                else if (player.speedHackCounter > SecurityConfig.SPEEDHACK_COUNTER + 2)
                {
                    MoveBack(player, false);
                    return false;
                }
            }
        }

        if (SecurityConfig.TELEPORTATION)
        {
            double delta = PositionUtil.GetDistance(x, y, player.GetX(), player.GetY()) / speed;
            if (speed > 5.0 && delta > 5.0 && (type & MovementMask.GLIDE) != MovementMask.GLIDE)
            {
                return Punish(player, true, "possibly used teleport hack - S:" + speed + " D:" + Math.Round(1000.0 * delta) / 1000.0 + " type:" + type);
            }
        }

        return true;
    }

    private static bool Punish(Player player, bool normalMovePacket, string message)
    {
        AuditLogger.Log(player, message);
        switch (SecurityConfig.PUNISH)
        {
            case 1:
                MoveBack(player, normalMovePacket);
                return false;
            case 2:
                MoveBack(player, normalMovePacket);
                if (player.speedHackCounter > SecurityConfig.SPEEDHACK_COUNTER * 3 || player.abnormalHackCounter > SecurityConfig.ABNORMAL_COUNTER * 3)
                    player.GetClientConnection().Close(new SM_QUIT_RESPONSE());
                return false;
            case 3:
                player.GetClientConnection().Close(new SM_QUIT_RESPONSE());
                return false;
            default:
                return true;
        }
    }

    private static void MoveBack(Player player, bool normalMovePacket)
    {
        if (normalMovePacket)
            PacketSendUtility.BroadcastPacketAndReceive(player, new SM_MOVE(player));
        else
        {
            WorldPosition lastPos = player.GetMoveController().GetLastPositionFromClient();
            PacketSendUtility.BroadcastPacketAndReceive(player,
                new SM_FORCED_MOVE(player, player.GetObjectId(), lastPos.GetX(), lastPos.GetY(), lastPos.GetZ()));
        }
        player.GetMoveController().UpdateLastMove();
        player.speedHackCounter = 0;
    }

    public static void CheckAionBin(int size, AionConnection con)
    {
        int legitSize = 212; // 212 after login, exactly 30 minutes later: 224, right after that: 1128 o.O
        if (SecurityConfig.AION_BIN_CHECK)
        {
            if (size != legitSize)
            {
                log.LogWarning("Detected modified aion.bin for account ID " + con.GetAccount().GetId());
                con.Close(new SM_QUIT_RESPONSE());
            }
        }
        // con.sendPacket(new SM_GAMEGUARD(size)); // not sent on GF servers currently
    }
}
