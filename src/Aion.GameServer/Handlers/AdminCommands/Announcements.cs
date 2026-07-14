using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Services;
using Aion.GameServer.Utils.ChatHandlers;
using Aion.GameServer.Utils.Xml;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/Announcements (Divinity). Manages automatic announcements.</summary>
public class Announcements : AdminCommand
{
    public Announcements()
        : base("announcements", "Manages automatic announcements.")
    {
        SetSyntaxInfo(
            "<list> - Shows all announcements including their ID.",
            "<reload> - Reloads all announcements from DB.",
            "<add> <elyos|asmodians|all> <chatType> <delay> <message> - Adds the specified message (delay is in seconds, chatType can be system, white, orange, shout or yellow).",
            "<delete> <id> - Deletes the announcement with the specified ID."
        );
    }

    public override void Execute(Player player, params string[] paramsArr)
    {
        if (paramsArr.Length == 0)
        {
            SendInfo(player);
            return;
        }

        if (paramsArr[0].Equals("list"))
        {
            ICollection<Announcement> announcements = AnnouncementService.GetInstance().GetAnnouncements();
            string msg;
            if (announcements.Count == 0)
            {
                msg = "There are no active announcements.";
            }
            else
            {
                msg = "Announcements:";
                foreach (Announcement announce in announcements)
                {
                    msg += "\nID: " + announce.GetId() + " (chat type: " + announce.GetType_() + ", delay: " + announce.GetDelay() + "s";
                    if (announce.GetFaction() != null)
                        msg += ", faction: " + announce.GetFaction();
                    msg += ")\n\t\"" + announce.GetAnnounce() + "\"";
                }
            }
            SendInfo(player, msg);
        }
        else if (paramsArr[0].Equals("reload"))
        {
            AnnouncementService.GetInstance().Reload();
            SendInfo(player, "Reloaded " + AnnouncementService.GetInstance().GetAnnouncements().Count + " announcements.");
        }
        else if (paramsArr[0].Equals("add"))
        {
            if (paramsArr.Length < 4)
            {
                SendInfo(player);
                return;
            }

            string faction = paramsArr[1].ToUpper();
            if (!new[] { "ELYOS", "ASMODIANS", "ALL" }.Contains(faction))
            {
                SendInfo(player, "Please specify a valid faction parameter.");
                return;
            }

            string chatType = paramsArr[2].ToUpper();
            if (!new[] { "SYSTEM", "WHITE", "ORANGE", "SHOUT", "YELLOW" }.Contains(chatType))
            {
                SendInfo(player, "Please specify a valid chat type parameter.");
                return;
            }

            int delay;
            // Java: NumberFormatException -> "Delay must be specified in seconds.";
            // IllegalArgumentException (delay < 300) -> "Delay must be at least 300s (5 minutes)."
            if (!TryParseInt(paramsArr[3], out delay))
            {
                SendInfo(player, "Delay must be specified in seconds.");
                return;
            }
            if (delay < 300)
            {
                SendInfo(player, "Delay must be at least 300s (5 minutes).");
                return;
            }

            string message = StringEscapeUtils.UnescapeJava(string.Join(' ', paramsArr.Skip(4)));
            if (message.Length == 0)
            {
                SendInfo(player, "The message cannot be empty.");
                return;
            }

            if (AnnouncementService.GetInstance().AddAnnouncement(message, faction, chatType, delay))
                SendInfo(player, "The announcement has been created successfully");
            else
                SendInfo(player, "The announcement could not be created");
        }
        else if (paramsArr[0].Equals("delete"))
        {
            if (paramsArr.Length < 2)
            {
                SendInfo(player, "Please specify the ID of the announcement to delete.");
                return;
            }

            int id;

            if (!TryParseInt(paramsArr[1], out id))
            {
                SendInfo(player, "Illegal announcement ID.");
                return;
            }

            // Delete the announcement from the database
            if (AnnouncementService.GetInstance().DelAnnouncement(id))
                SendInfo(player, "The announcement has been deleted successfully.");
            else
                SendInfo(player, "The announcement could not be deleted.");
        }
        else
        {
            SendInfo(player);
        }
    }
}
