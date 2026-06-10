using System;
using System.Collections.Concurrent;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Services.Cron;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Services.Player;

/// <summary>Java parity: services/player/PlayerLimitService (Source, Neon). Per-account daily NPC sell limit: updateSellLimit computes affordable count vs remaining limit (dynamic cap option), decrements, returns sellable count; scheduleUpdate clears the map on a cron. ConcurrentHashMap->ConcurrentDictionary; get/putIfAbsent->TryGetValue/TryAdd; Long limit->long via TryGetValue. CronService now ported. SellLimit/CustomConfig red-tolerated.</summary>
public class PlayerLimitService
{
    private static readonly ConcurrentDictionary<int, long> sellLimit = new ConcurrentDictionary<int, long>();

    /// <summary>
    /// Returns the number of items subtracted from the sell limit or 0 if limit is reached and did not change.
    /// </summary>
    public static long UpdateSellLimit(Player player, long itemPrice, long itemCount)
    {
        if (!CustomConfig.LIMITS_ENABLED || itemPrice == 0)
            return itemCount;

        int accountId = player.GetAccount().GetId();
        if (!sellLimit.TryGetValue(accountId, out long limit))
        {
            limit = SellLimit.GetSellLimit(player);
            sellLimit.TryAdd(accountId, limit);
        }

        if (itemPrice < 0 || itemCount <= 0)
            return 0;

        long possibleCount = Math.Max(0, limit / itemPrice);
        if (CustomConfig.LIMITS_ENABLE_DYNAMIC_CAP && possibleCount < itemCount)
            possibleCount += 1;

        if (possibleCount == 0 || limit == 0)
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_DAY_CANNOT_SELL_NPC(limit));
            return 0;
        }
        else
        {
            long useCount = Math.Min(possibleCount, itemCount);
            limit -= Math.Min(limit, itemPrice * useCount);
            sellLimit[accountId] = limit;
            return useCount;
        }
    }

    public void ScheduleUpdate()
    {
        CronService.GetInstance().Schedule(() => sellLimit.Clear(), CustomConfig.LIMITS_UPDATE, true);
    }

    public static PlayerLimitService GetInstance()
    {
        return SingletonHolder.instance;
    }

    private static class SingletonHolder
    {
        internal static readonly PlayerLimitService instance = new PlayerLimitService();
    }
}
