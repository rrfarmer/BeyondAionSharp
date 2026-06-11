using System;
using System.Collections.Generic;
using System.IO;
using Aion.GameServer.Configs.Administration;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Services;

/// <summary>Java parity: services/AdminService (KID).</summary>
public class AdminService
{
    private static readonly ILogger itemLog = NullLogger.Instance; // Java logger "GMITEMRESTRICTION"
    private static readonly ILogger log = NullLogger.Instance;
    private List<int> list;
    private static AdminService instance = new AdminService();

    public static AdminService GetInstance()
    {
        return instance;
    }

    public AdminService()
    {
        list = new List<int>();
        Reload();
    }

    public void Reload()
    {
        list.Clear();

        try
        {
            using (StreamReader br = new StreamReader("./config/administration/item.restriction.txt"))
            {
                string line = null;
                while ((line = br.ReadLine()) != null)
                {
                    if (line.StartsWith("#") || line.Trim().Length == 0)
                        continue;

                    string pt = line.Split('#')[0].Replace(" ", "");
                    list.Add(int.Parse(pt));
                }
            }
        }
        catch (IOException e)
        {
            Console.Error.WriteLine(e);
        }
        log.LogInformation("AdminService loaded " + list.Count + " operational items.");
    }

    public bool CanOperate(Player player, Player target, Item item, string type)
    {
        return CanOperate(player, target, item.GetItemId(), type);
    }

    public bool CanOperate(Player player, Player target, int itemId, string type)
    {
        if (!player.IsStaff())
            return true;

        if (player.HasAccess(AdminConfig.UNRESTRICTED_ITEMTRADE)) // staff member is allowed to trade with who ever he wants to
            return true;

        if (target != null && target.IsStaff()) // allow between server staff
            return true;

        if (list.Contains(itemId)) // item goes from staff member to normal player, so log it
        {
            itemLog.LogInformation(player + " traded item " + itemId + " via " + type + (target != null ? " to player " + target : ""));
            return true;
        }

        PacketSendUtility.SendMessage(player, "You cannot use " + type + " with this item.");
        return false;
    }
}
