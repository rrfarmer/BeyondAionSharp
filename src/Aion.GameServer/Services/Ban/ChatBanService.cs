using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Aion.GameServer.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Services.Ban;

/// <summary>Java parity: services/ban/ChatBanService (ViAl, Neon).</summary>
public class ChatBanService
{
    /// <summary>List for player chat bans (player → expiration time). Resets on server restart.</summary>
    private static readonly ConcurrentDictionary<int, long> chatBans = new ConcurrentDictionary<int, long>();

    /// <summary>Bans a player from all chats.</summary>
    public static void BanPlayer(Aion.GameServer.Model.GameObjects.Players.Player player, long durationMillis)
    {
        Aion.GameServer.Network.ChatServer.ChatServer.GetInstance().SendPlayerGagPacket(player.GetObjectId(), durationMillis);
        chatBans[player.GetObjectId()] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + durationMillis;
        RegisterUnban(player, durationMillis);
    }

    public static void UnbanPlayer(Aion.GameServer.Model.GameObjects.Players.Player player)
    {
        player.GetController().CancelTask(TaskId.GAG);
        Aion.GameServer.Network.ChatServer.ChatServer.GetInstance().SendPlayerGagPacket(player.GetObjectId(), 0);
        if (chatBans.TryRemove(player.GetObjectId(), out _) && player.IsOnline())
            PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_CAN_CHAT_NOW());
    }

    private static void RegisterUnban(Aion.GameServer.Model.GameObjects.Players.Player player, long delay)
    {
        player.GetController().AddTask(TaskId.GAG, ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            UnbanPlayer(player);
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(delay)));
    }

    public static bool IsBanned(Aion.GameServer.Model.GameObjects.Players.Player player)
    {
        return GetBanMinutes(player) > 0;
    }

    /// <summary>
    /// Checks time left for the player's ban. If ban is over, unbans the player automatically.
    /// If not and an unban task is missing (e.g. after logout), starts one.
    /// </summary>
    /// <returns>The remaining ban time in minutes. Only returns 0 if ban time is really over.</returns>
    public static int GetBanMinutes(Aion.GameServer.Model.GameObjects.Players.Player player)
    {
        if (!chatBans.TryGetValue(player.GetObjectId(), out long expireTime))
            return 0;

        long millisLeft = expireTime - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (millisLeft <= 0)
        {
            UnbanPlayer(player);
            return 0;
        }

        if (!player.GetController().HasTask(TaskId.GAG))
            RegisterUnban(player, millisLeft);

        return (int)Math.Max(0, Math.Ceiling(millisLeft / 60000f));
    }
}
