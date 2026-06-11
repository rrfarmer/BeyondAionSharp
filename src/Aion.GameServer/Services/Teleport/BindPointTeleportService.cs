using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Hotspot;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Services.Item;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Audit;

namespace Aion.GameServer.Services.Teleport;

/// <summary>Java parity: services/teleport/BindPointTeleportService (ViAl). Static hotspot/bind-point teleport w/ 10-min cooldown. HashMap cooldowns→Dictionary; anonymous Runnables (10s cast then 1s teleport)→async delegates; currentTimeMillis→UtcNow.ToUnixTimeMilliseconds; inline LoggerFactory.warn→inline; Math.max/abs→Math.Max/Abs; nested Cooldown class. HotspotTemplate/SM_BIND_POINT_TELEPORT red-tolerated.</summary>
public class BindPointTeleportService
{
    private const int COOLDOWN_IN_SECONDS = 600; // 10 mins
    /// <summary>player id - cooldown</summary>
    private static readonly Dictionary<int, Cooldown> cooldowns = new Dictionary<int, Cooldown>();

    public static void OnLogin(Player player)
    {
        Cooldown cooldown = GetCooldown(player);
        if (cooldown != null && cooldown.GetTimeLeft() > 0)
            PacketSendUtility.BroadcastPacketAndReceive(player,
                new SM_BIND_POINT_TELEPORT(3, player.GetObjectId(), cooldown.GetLocId(), cooldown.GetTimeLeft()));
    }

    public static void Teleport(Player player, int locId, long kinah)
    {
        HotspotTemplate hotspot = DataManager.HOTSPOT_DATA.GetHotspotTemplateById(locId);
        if (hotspot == null)
        {
            AuditLogger.Log(player, "Tried to use invalid hotspot teleport to locId " + locId);
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_CANNOT_MOVE_TO_AIRPORT_NO_ROUTE());
            return;
        }
        long price = CalculateTeleportationPrice(player, hotspot, kinah);

        if (!CheckRequirements(player, hotspot, price))
            return;

        PacketSendUtility.BroadcastPacket(player, new SM_BIND_POINT_TELEPORT(1, player.GetObjectId(), locId, 0), true);

        player.GetController().AddTask(TaskId.SKILL_USE, ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            if (!player.GetInventory().TryDecreaseKinah(price, ItemPacketService.ItemUpdateType.DEC_KINAH_FLY))
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_CANNOT_MOVE_TO_AIRPORT_NOT_ENOUGH_FEE());
                return ValueTask.CompletedTask;
            }
            AddCooldown(player, locId);
            PacketSendUtility.BroadcastPacket(player, new SM_BIND_POINT_TELEPORT(3, player.GetObjectId(), locId, COOLDOWN_IN_SECONDS), true);
            ThreadPoolManager.GetInstance().Schedule(ct2 =>
            {
                if (!player.GetLifeStats().IsAboutToDie() && !player.IsDead())
                    TeleportService.TeleportTo(player, hotspot.GetWorldId(), hotspot.GetX(), hotspot.GetY(), hotspot.GetZ());
                return ValueTask.CompletedTask;
            }, TimeSpan.FromMilliseconds(1000));
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(10000)));
    }

    public static void CancelTeleport(Player player, int locId)
    {
        if (player.GetController().HasTask(TaskId.SKILL_USE))
        {
            player.GetController().CancelTask(TaskId.SKILL_USE);
            PacketSendUtility.BroadcastPacket(player, new SM_BIND_POINT_TELEPORT(2, player.GetObjectId(), locId, 0), true);
        }
    }

    private static long CalculateTeleportationPrice(Player player, HotspotTemplate hotspot, long priceSentByGameClient)
    {
        double distance = PositionUtil.GetDistance(player, hotspot.GetX(), hotspot.GetY(), hotspot.GetZ());
        long basePrice = hotspot.GetPrice();
        long distanceCost = (long)(basePrice * distance / 1000d);
        long price = Math.Max(1, basePrice + distanceCost);
        long priceDifference = Math.Abs(price - priceSentByGameClient);
        if (priceDifference > 1) // only warn about unexpected differences (minimal discrepancies from floating-point calculations can be ignored)
            NullLoggerFactory.Instance.CreateLogger(nameof(BindPointTeleportService)).LogWarning("Hotspot teleport {Id} prices don't match: {Price} vs. {ClientPrice}", hotspot.GetId(), price, priceSentByGameClient);
        return Math.Max(price, priceSentByGameClient);
    }

    private static bool CheckRequirements(Player player, HotspotTemplate hotspot, long price)
    {
        if (player.GetWorldId() != hotspot.GetWorldId())
        {
            AuditLogger.Log(player, "tried to use hotspot teleport " + hotspot.GetId() + " from invalid start world " + player.GetWorldId() + ", expected "
                + hotspot.GetWorldId());
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_CANNOT_MOVE_TO_AIRPORT_NO_ROUTE());
            return false;
        }
        if (!(player.GetRace() == Race.PC_ALL) && player.GetRace() != hotspot.GetRace())
        {
            AuditLogger.Log(player, "tried to use hotspot teleport " + hotspot.GetId() + " for invalid race " + player.GetRace() + ", expected " + hotspot.GetRace());
            return false;
        }
        if (player.GetInventory().GetKinah() < price)
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_CANNOT_MOVE_TO_AIRPORT_NOT_ENOUGH_FEE());
            return false;
        }
        Cooldown cooldown = GetCooldown(player);
        if (cooldown != null && cooldown.GetTimeLeft() > 0)
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_FLYING_TIME_NOT_READY());
            return false;
        }

        return true;
    }

    private static void AddCooldown(Player player, int locId)
    {
        long cooldown = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + COOLDOWN_IN_SECONDS * 1000;
        cooldowns[player.GetObjectId()] = new Cooldown(locId, cooldown);
    }

    private static Cooldown GetCooldown(Player player)
    {
        return cooldowns.GetValueOrDefault(player.GetObjectId());
    }

    private class Cooldown
    {
        internal int locId;
        internal long cdEnd;

        public Cooldown(int locId, long cdEnd)
        {
            this.locId = locId;
            this.cdEnd = cdEnd;
        }

        public int GetLocId()
        {
            return locId;
        }

        public int GetTimeLeft()
        {
            int estimated = (int)((cdEnd - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) / 1000);
            if (estimated > 0)
                return estimated;
            else
                return 0;
        }
    }
}
