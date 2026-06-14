using System.Drawing;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/Teleportation (cura, Neon).</summary>
public class Teleportation : AdminCommand
{
    public Teleportation()
        : base("teleportation", "Toggles teleportation mode when moving via mouse.")
    {
    }

    protected override void Execute(Player admin, params string[] paramsArr)
    {
        if (admin.IsInCustomState(CustomPlayerState.TELEPORTATION_MODE))
            admin.UnsetCustomState(CustomPlayerState.TELEPORTATION_MODE);
        else
            admin.SetCustomState(CustomPlayerState.TELEPORTATION_MODE);
        SendInfo(admin, "Teleportation mode is now " + (admin.IsInCustomState(CustomPlayerState.TELEPORTATION_MODE)
            ? ChatUtil.Color("active", Color.Green) : ChatUtil.Color("inactive", Color.Red)) + ".");
    }
}
