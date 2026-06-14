using Aion.GameServer.Cache;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/Html (lord_rex).</summary>
public class Html : AdminCommand
{
    public Html()
        : base("html")
    {
    }

    protected override void Execute(Player player, params string[] paramsArr)
    {
        if (paramsArr == null || paramsArr.Length < 1)
        {
            PacketSendUtility.SendMessage(player, "Usage: //html <reload|show>");
            return;
        }

        if (paramsArr[0].Equals("reload"))
        {
            HTMLCache.GetInstance().Reload(true);
            PacketSendUtility.SendMessage(player, HTMLCache.GetInstance().ToString());
        }
        else if (paramsArr[0].Equals("show"))
            if (paramsArr.Length >= 2)
                HTMLService.ShowHTML(player, HTMLCache.GetInstance().GetHTML(paramsArr[1] + ".xhtml"));
            else
                PacketSendUtility.SendMessage(player, "Usage: //html show <filename>");
    }

    private void Info(Player player, string message)
    {
        PacketSendUtility.SendMessage(player, "Usage: //html <reload|show>");
    }
}
