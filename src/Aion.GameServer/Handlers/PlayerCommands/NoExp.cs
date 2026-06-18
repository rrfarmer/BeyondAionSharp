using System.Drawing;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.PlayerCommands;

/// <summary>Java parity: data/handlers/playercommands/NoExp (Wakizashi, Neon). Toggles the player's ability to gain experience.</summary>
public class NoExp : PlayerCommand
{
    public NoExp()
        : base("noexp", "Enables/disables your ability to gain experience.")
    {
    }

    public override void Execute(Player player, params string[] paramsArr)
    {
        PlayerCommonData pcd = player.GetCommonData();

        pcd.SetNoExp(!pcd.GetNoExp());
        SendInfo(player, "Experience rewards are now " + (pcd.GetNoExp() ? ChatUtil.Color("inactive", Color.Red) : ChatUtil.Color("active", Color.Green)) + ".");
    }
}
