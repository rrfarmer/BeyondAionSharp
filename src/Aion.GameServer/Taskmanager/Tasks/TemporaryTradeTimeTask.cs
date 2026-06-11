using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Taskmanager.Tasks;

/// <summary>Java parity: taskmanager/tasks/TemporaryTradeTimeTask (Mr. Poke) : AbstractPeriodicTaskManager. ConcurrentHashMap→ConcurrentDictionary; SingletonHolder→static readonly; currentTimeMillis→UtcNow.ToUnixTimeMilliseconds; entrySet-iterator+remove→snapshot foreach + TryRemove. Item/World/SM_SYSTEM_MESSAGE red-tolerated.</summary>
public class TemporaryTradeTimeTask : AbstractPeriodicTaskManager
{
    private readonly ConcurrentDictionary<Item, ICollection<int>> items = new();

    public TemporaryTradeTimeTask() : base(1000)
    {
    }

    public static TemporaryTradeTimeTask GetInstance()
    {
        return SingletonHolder._instance;
    }

    public void AddTask(Item item, ICollection<int> players)
    {
        items[item] = players;
    }

    public bool CanTrade(Item item, int playerObjectId)
    {
        ICollection<int> players = items.GetValueOrDefault(item);
        if (players == null)
            return false;
        return players.Contains(playerObjectId);
    }

    protected override void Run()
    {
        foreach (KeyValuePair<Item, ICollection<int>> entry in items)
        {
            Item item = entry.Key;
            int time = item.GetTemporaryExchangeTime() - (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000);
            if (time <= 0)
            {
                foreach (int playerId in entry.Value)
                {
                    Player player = World.GetInstance().GetPlayer(playerId);
                    if (player != null)
                        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_EXCHANGE_TIME_OVER(item.GetL10n()));
                }
                item.SetTemporaryExchangeTime(0);
                items.TryRemove(item, out _);
            }
        }
    }

    private static class SingletonHolder
    {
        internal static readonly TemporaryTradeTimeTask _instance = new();
    }
}
