using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/Invul (Andy, Divinity).</summary>
public class Invul : AdminCommand
{
    public Invul()
        : base("invul", "Enables/disables invulnerability.")
    {
    }

    protected override void Execute(Player player, params string[] paramsArr)
    {
        if (player.IsInvulnerable())
        {
            player.UnsetCustomState(CustomPlayerState.INVULNERABLE);
            PacketSendUtility.SendMessage(player, "You are now mortal.");
        }
        else
        {
            player.SetCustomState(CustomPlayerState.INVULNERABLE);
            SendInfo(player, ChatUtil.L10n(293440)); // Immune to all damage.
        }
    }
}
