using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/Res (Sarynth).</summary>
public class Res : AdminCommand
{
    public Res()
        : base("res")
    {
    }

    protected override void Execute(Player admin, params string[] paramsArr)
    {
        VisibleObject target = admin.GetTarget();
        if (target == null)
        {
            PacketSendUtility.SendMessage(admin, "No target selected.");
            return;
        }

        if (target is not Player)
        {
            PacketSendUtility.SendMessage(admin, "You can only resurrect other players.");
            return;
        }

        Player player = (Player)target;
        if (!player.IsDead())
        {
            PacketSendUtility.SendMessage(admin, "That player is already alive.");
            return;
        }

        // Default action is to prompt for resurrect.
        if (paramsArr == null || paramsArr.Length == 0 || ("prompt").StartsWith(paramsArr[0]))
        {
            player.SetPlayerResActivate(true);
            PacketSendUtility.SendPacket(player, new SM_RESURRECT(admin));
            return;
        }

        if (("instant").StartsWith(paramsArr[0]))
        {
            PlayerReviveService.SkillRevive(player);
            return;
        }

        PacketSendUtility.SendMessage(admin, "[Resurrect] Usage: target player and use //res <instant|prompt>");
    }
}
