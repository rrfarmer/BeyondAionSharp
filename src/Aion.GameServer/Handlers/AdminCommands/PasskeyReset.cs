using Aion.GameServer.Dao;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.LoginServer;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/PasskeyReset (cura).</summary>
public class PasskeyReset : AdminCommand
{
    public PasskeyReset()
        : base("passkeyreset")
    {
    }

    protected override void Execute(Player player, params string[] paramsArr)
    {
        if (paramsArr == null || paramsArr.Length < 1)
        {
            PacketSendUtility.SendMessage(player, "syntax: //passkeyreset <player> <passkey>");
            return;
        }

        string name = Util.ConvertName(paramsArr[0]);
        int accountId = PlayerDAO.GetAccountIdByName(name);
        if (accountId == 0)
        {
            PacketSendUtility.SendMessage(player, "player " + name + " can't find!");
            PacketSendUtility.SendMessage(player, "syntax: //passkeyreset <player> <passkey>");
            return;
        }

        if (!int.TryParse(paramsArr[1], out _))
        {
            PacketSendUtility.SendMessage(player, "parameters should be number!");
            return;
        }

        string newPasskey = paramsArr[1];
        if (!(newPasskey.Length > 5 && newPasskey.Length < 9))
        {
            PacketSendUtility.SendMessage(player, "passkey is 6~8 digits!");
            return;
        }

        PlayerPasskeyDAO.UpdateForcePlayerPasskey(accountId, newPasskey);
        LoginServer.GetInstance().SendBanPacket((byte)2, accountId, "", -1, player.GetObjectId());
    }

    private void Info(Player player, string message)
    {
        PacketSendUtility.SendMessage(player, "syntax: //passkeyreset <player> <passkey>");
    }
}
