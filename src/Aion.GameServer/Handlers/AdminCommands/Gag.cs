using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Ban;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/Gag (Watson, Neon).</summary>
public class Gag : AdminCommand
{
    public Gag()
        : base("gag", "Bans a player from all chats.")
    {
        SetSyntaxInfo(
            "<player> <duration> <reason> - Chat bans the player for the specified time in minutes.",
            "<player> <remove> - Removes the chat ban of this player.");
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length == 0)
        {
            SendInfo(admin);
            return;
        }

        Player player = Aion.GameServer.World.World.GetInstance().GetPlayer(Util.ConvertName(paramsArr[0]));
        if (player == null || !player.IsOnline())
        {
            PacketSendUtility.SendPacket(admin, SM_SYSTEM_MESSAGE.STR_MSG_ASK_PCINFO_LOGOFF());
            return;
        }

        if (paramsArr.Length < 2)
        {
            SendInfo(admin);
            return;
        }

        if (paramsArr[1].Equals("remove", System.StringComparison.OrdinalIgnoreCase))
        {
            if (!ChatBanService.IsBanned(player))
            {
                SendInfo(admin, "Player " + player.GetName() + " can already chat.");
                return;
            }

            ChatBanService.UnbanPlayer(player);
            SendInfo(admin, "Removed gag from player " + player.GetName() + ".");
            return;
        }

        int time;
        if (!int.TryParse(paramsArr[1], out time))
        {
            SendInfo(admin, "<duration> must be an int value (time in minutes).");
            return;
        }

        if (time < 1)
        {
            SendInfo(admin, "<duration> must be at least 1 minute.");
            return;
        }

        if (paramsArr.Length < 3 || paramsArr[2].Trim().Length <= 1)
        {
            SendInfo(admin, "<reason> must be specified.");
            return;
        }

        ChatBanService.BanPlayer(player, time * 60000);
        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_INGAME_BLOCK_ENABLE_NO_CHAT(time));

        string reason = string.Join(" ", paramsArr[2..paramsArr.Length]);
        string capitalized = ChatUtil.Capitalize(reason);
        SendInfo(player, capitalized.EndsWith(".") || capitalized.EndsWith("!") ? capitalized : capitalized + ".");
        SendInfo(admin, "Player " + player.GetName() + " is now gagged for " + time + " minutes.");
    }
}
