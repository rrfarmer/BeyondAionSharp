using System.Drawing;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.PlayerCommands;

/// <summary>Java parity: data/handlers/playercommands/Nomorph. Toggles the player's current transformation appearance.</summary>
public class Nomorph : PlayerCommand
{
    public Nomorph()
        : base("nomorph", "Enables/disables your current transformation appearance.")
    {
    }

    public override void Execute(Player player, params string[] paramsArr)
    {
        if (player.GetTransformModel().GetEventModelId() == player.GetObjectTemplate().GetTemplateId())
        {
            player.GetTransformModel().SetEventModelId(0);
        }
        else
        {
            player.GetTransformModel().SetEventModelId(player.GetObjectTemplate().GetTemplateId());
        }
        player.GetTransformModel().UpdateVisually();
        SendInfo(player, "Transformation appearance is now " + (player.GetTransformModel().GetEventModelId() == player.GetObjectTemplate().GetTemplateId() ? ChatUtil.Color("inactive", Color.Red) : ChatUtil.Color("active", Color.Green)) + ".");
    }
}
