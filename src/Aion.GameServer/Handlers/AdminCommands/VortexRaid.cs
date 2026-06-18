using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Vortex;
using Aion.GameServer.Services;
using Aion.GameServer.Utils.ChatHandlers;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/VortexRaid.</summary>
public class VortexRaid : AdminCommand
{
    public VortexRaid()
        : base("vortexraid", "Starts/stops a raid in Theobomos or Brusthonin.")
    {
        SetSyntaxInfo(
            "start <Theobomos|Brusthonin> - Starts the raid at the given location.",
            "stop <Theobomos|Brusthonin> - Stops the raid at the given location.");
    }

    public override void Execute(Player player, params string[] paramsArr)
    {
        if (paramsArr.Length < 2)
        {
            SendInfo(player);
            return;
        }

        int mapId = WorldMapTypeExtensions.GetMapId(paramsArr[1]);
        VortexLocation loc = DataManager.VORTEX_DATA.GetVortexLocation(mapId);
        if (loc == null)
        {
            SendInfo(player, "Invalid location.");
            return;
        }
        string locationName = Aion.GameServer.World.World.GetInstance().GetWorldMap(mapId).GetName();

        if ("start".Equals(paramsArr[0], System.StringComparison.OrdinalIgnoreCase))
        {
            if (VortexService.GetInstance().IsInvasionInProgress(loc.GetId()))
            {
                SendInfo(player, locationName + " is already under siege.");
            }
            else
            {
                SendInfo(player, locationName + " raid started.");
                VortexService.GetInstance().StartInvasion(loc.GetId());
            }
        }
        else if ("stop".Equals(paramsArr[0], System.StringComparison.OrdinalIgnoreCase))
        {
            if (!VortexService.GetInstance().IsInvasionInProgress(loc.GetId()))
            {
                SendInfo(player, locationName + " is not under siege.");
            }
            else
            {
                SendInfo(player, locationName + " raid stopped.");
                VortexService.GetInstance().StopInvasion(loc.GetId());
            }
        }
        else
        {
            SendInfo(player);
        }
    }
}
