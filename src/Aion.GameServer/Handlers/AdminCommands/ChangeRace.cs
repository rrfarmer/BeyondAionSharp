using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/ChangeRace (ginho1).</summary>
public class ChangeRace : AdminCommand
{
    public ChangeRace()
        : base("changerace", "Switches your race to the opposite faction.")
    {
    }

    protected override void Execute(Player admin, params string[] paramsArr)
    {
        admin.GetCommonData().SetRace(admin.GetOppositeRace());
        admin.GetController().OnChangedPlayerAttributes();
        admin.GetController().UpdateNearbyQuests();
    }
}
