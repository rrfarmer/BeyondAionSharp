using System;
using System.Threading.Tasks;
using Aion.GameServer.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Services;

/// <summary>Java parity: services/PunishmentService (lord_rex, Cura, nrg).</summary>
public class PunishmentService
{
    /// <summary>Handle unbanning a character.</summary>
    public static void UnbanChar(int playerId)
    {
        Aion.GameServer.Dao.PlayerPunishmentsDAO.UnpunishPlayer(playerId, PunishmentType.CHARBAN);
    }

    /// <summary>Handle banning a character.</summary>
    public static void BanChar(int playerId, int dayCount, string reason)
    {
        Aion.GameServer.Dao.PlayerPunishmentsDAO.PunishPlayer(playerId, PunishmentType.CHARBAN, CalculateDuration(dayCount), reason);

        // if player is online - kick him
        Aion.GameServer.Model.GameObjects.Players.Player player = Aion.GameServer.World.World.GetInstance().GetPlayer(playerId);
        if (player != null)
            player.GetClientConnection().Close(new Aion.GameServer.Network.Aion.ServerPackets.SmQuitResponse());
    }

    /// <summary>Calculates the timestamp when a given number of days is over.</summary>
    /// <returns>ban duration in seconds</returns>
    public static long CalculateDuration(int dayCount)
    {
        if (dayCount == 0)
            return int.MaxValue; // int because client handles this with seconds timestamp in int
        return dayCount * 86400L;
    }

    /// <summary>Handle moving or removing a player from prison.</summary>
    public static void SetIsInPrison(Aion.GameServer.Model.GameObjects.Players.Player player, bool state, long delayInMinutes, string reason)
    {
        if (state)
        {
            if (delayInMinutes > 0)
            {
                long duration = delayInMinutes * 60000L;
                SchedulePrisonTask(player, duration);
                Aion.GameServer.Services.Ban.ChatBanService.BanPlayer(player, delayInMinutes);
                player.SetPrisonEndTimeMillis(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + duration);
                Aion.GameServer.Services.Teleport.TeleportService.TeleportToPrison(player);
                Aion.GameServer.Dao.PlayerPunishmentsDAO.PunishPlayer(player, PunishmentType.PRISON, reason);
                PacketSendUtility.SendMessage(player, "You have been teleported to prison for a time of " + delayInMinutes
                    + " minutes.\n If you disconnect the time stops and the timer of the prison'll see at your next login.");
            }
        }
        else
        {
            player.GetController().CancelTask(TaskId.PRISON);
            player.SetPrisonEndTimeMillis(0);
            Aion.GameServer.Services.Ban.ChatBanService.UnbanPlayer(player);
            Aion.GameServer.Services.Teleport.TeleportService.MoveToBindLocation(player);
            Aion.GameServer.Dao.PlayerPunishmentsDAO.UnpunishPlayer(player.GetObjectId(), PunishmentType.PRISON);
            PacketSendUtility.SendMessage(player, "You come out of prison.");
        }
    }

    /// <summary>Update the prison status.</summary>
    public static void UpdatePrisonStatus(Aion.GameServer.Model.GameObjects.Players.Player player)
    {
        int prisonDurationSeconds = player.GetPrisonDurationSeconds();
        if (prisonDurationSeconds > 0)
        {
            SchedulePrisonTask(player, prisonDurationSeconds * 1000L);
            int remainingMinutes = prisonDurationSeconds / 60;
            if (remainingMinutes <= 0)
                remainingMinutes = 1;

            Aion.GameServer.Services.Ban.ChatBanService.BanPlayer(player, remainingMinutes);
            PacketSendUtility.SendMessage(player, "You are still in prison for " + remainingMinutes + " minute" + (remainingMinutes > 1 ? "s" : "") + ".");

            if (player.GetWorldId() != Aion.GameServer.World.WorldMapType.DF_PRISON.GetId() && player.GetWorldId() != Aion.GameServer.World.WorldMapType.LF_PRISON.GetId())
            {
                PacketSendUtility.SendMessage(player, "You will be teleported to prison in a moment!");
                ThreadPoolManager.GetInstance().Schedule(ct =>
                {
                    Aion.GameServer.Services.Teleport.TeleportService.TeleportToPrison(player);
                    return ValueTask.CompletedTask;
                }, TimeSpan.FromMilliseconds(10000));
            }
        }
    }

    /// <summary>Schedule a prison task.</summary>
    private static void SchedulePrisonTask(Aion.GameServer.Model.GameObjects.Players.Player player, long prisonTimer)
    {
        player.GetController().AddTask(TaskId.PRISON, ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            SetIsInPrison(player, false, 0, "");
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(prisonTimer)));
    }

    /// <summary>Handle can or can't gathering.</summary>
    public static void SetIsNotGatherable(Aion.GameServer.Model.GameObjects.Players.Player player, int captchaCount, bool state, long delay)
    {
        if (state)
        {
            if (captchaCount < 3)
            {
                PacketSendUtility.SendPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SmCaptcha(captchaCount + 1, player.GetCaptchaImage()));
            }
            else
            {
                player.SetCaptchaWord(null);
                player.SetCaptchaImage(null);
            }
            player.SetGatherRestrictionExpirationTime(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + delay);
            Aion.GameServer.Dao.PlayerPunishmentsDAO.PunishPlayer(player, PunishmentType.GATHER, "Possible gatherbot");
        }
        else
        {
            PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_MSG_CAPTCHA_RECOVERED());
            player.SetCaptchaWord(null);
            player.SetCaptchaImage(null);
            player.SetGatherRestrictionExpirationTime(0);
            Aion.GameServer.Dao.PlayerPunishmentsDAO.UnpunishPlayer(player.GetObjectId(), PunishmentType.GATHER);
        }
    }

    /// <summary>PunishmentType (Cura).</summary>
    public enum PunishmentType
    {
        PRISON,
        GATHER,
        CHARBAN
    }
}
