using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/SecurityToken (Artur).</summary>
public class SecurityToken : AdminCommand
{
    public SecurityToken()
        : base("stoken")
    {
    }

    protected override void Execute(Player player, params string[] paramsArr)
    {
        if (paramsArr.Length < 1)
        {
            PacketSendUtility.SendMessage(player, "Syntax: //stoken <playername> || //stoken show <playername>");
            return;
        }
        Player receiver = null;

        if (paramsArr[0].Equals("show"))
        {
            receiver = Aion.GameServer.World.World.GetInstance().GetPlayer(Util.ConvertName(paramsArr[1]));
            if (receiver == null)
            {
                PacketSendUtility.SendMessage(player, "Can't find this player, maybe he's not online");
                return;
            }

            if (!"".Equals(receiver.GetAccount().GetSecurityToken()))
            {
                PacketSendUtility.SendMessage(player, "The Security Token of this player is: " + receiver.GetAccount().GetSecurityToken());
            }
            else
            {
                PacketSendUtility.SendMessage(player, "This player haven't an Security Token!");
            }
        }
        else
        {
            receiver = Aion.GameServer.World.World.GetInstance().GetPlayer(Util.ConvertName(paramsArr[0]));

            if (receiver == null)
            {
                PacketSendUtility.SendMessage(player, "Can't find this player, maybe he's not online");
                return;
            }

            SecurityTokenService.GenerateToken(receiver.GetAccount());
        }
    }
}
