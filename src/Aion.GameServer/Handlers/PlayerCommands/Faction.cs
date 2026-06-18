using System;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Restrictions;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.PlayerCommands;

/// <summary>Java parity: data/handlers/playercommands/Faction (Shepper, bobobear, Neon). Faction-wide chat channel.</summary>
public class Faction : PlayerCommand
{
    public Faction()
        : base("faction", "Faction chat.")
    {
        string priceInfo = CustomConfig.FACTION_USE_PRICE > 0 ? " Price: " + CustomConfig.FACTION_USE_PRICE + " Kinah." : "";
        SetSyntaxInfo("<message> - Sends the message to all players of your faction." + priceInfo);
    }

    public override void Execute(Player player, params string[] paramsArr)
    {
        if (!CustomConfig.FACTION_CMD_CHANNEL)
        {
            SendInfo(player, "The faction channel is disabled.");
            return;
        }

        if (paramsArr == null || paramsArr.Length < 1)
        {
            SendInfo(player);
            return;
        }

        if (!PlayerRestrictions.CanChat(player))
            return;

        if (CustomConfig.FACTION_USE_PRICE > 0)
        {
            if (CustomConfig.FACTION_USE_PRICE > player.GetInventory().GetKinah())
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_NOT_ENOUGH_MONEY());
                return;
            }
            player.GetInventory().DecreaseKinah(CustomConfig.FACTION_USE_PRICE);
        }

        string senderName = player.GetName(player.IsStaff());
        string message = CustomConfig.FACTION_CHAT_CHANNEL ? string.Join(" ", paramsArr) : senderName + ": " + string.Join(" ", paramsArr);
        ChatType channel = CustomConfig.FACTION_CHAT_CHANNEL ? ChatType.CH1 : ChatType.BRIGHT_YELLOW;

        PlayerChatService.LogMessage(player, ChatType.NORMAL, "[Faction Msg] " + message);

        World.World.GetInstance().ForEachPlayer(listener =>
        {
            // GMs can read both factions (but only write to their own)
            if (listener.GetRace() == player.GetRace() || listener.IsStaff())
            {
                string name = listener.IsStaff() ? (player.GetRace() == Race.ASMODIANS ? "(A) " : "(E) ") + senderName : senderName;
                PacketSendUtility.SendPacket(listener, new SM_MESSAGE(player.GetObjectId(), name, message, channel));
            }
        });
    }
}
