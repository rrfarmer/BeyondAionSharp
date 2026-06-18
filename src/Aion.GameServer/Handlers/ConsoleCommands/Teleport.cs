using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Services.Instance;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils.ChatHandlers;
using Aion.GameServer.World;
using Aion.GameServer.World.Geo;

namespace Aion.GameServer.Handlers.ConsoleCommands;

/// <summary>Java parity: data/handlers/consolecommands/Teleport (Yeats). Moves the player to map coordinates (GM dialog / world-map click).</summary>
public class Teleport : ConsoleCommand
{
    public Teleport()
        : base("teleport", "Moves you to any location.")
    {
        SetSyntaxInfo("[mapCName] <x> <y> <z> - Moves you to the specified coordinates on the given map (default: current map).");
    }

    public override void Execute(Player player, params string[] paramsArr)
    {
        if (paramsArr.Length < 3)
        {
            SendInfo(player);
            return;
        }
        int i = 0;
        int worldId = paramsArr.Length == 3 ? player.GetWorldId() : DataManager.WORLD_MAPS_DATA.GetWorldIdByCName(paramsArr[i++]);
        if (worldId != 0)
        {
            int x = int.Parse(paramsArr[i++]);
            int y = int.Parse(paramsArr[i++]);
            int inputZ = int.Parse(paramsArr[i++]);
            float z = inputZ;
            WorldMapInstance instance = GetOrCreateWorldMapInstance(player, worldId);
            if (inputZ == 10_000) // default value in client when selecting a point on the world map
                z = GeoService.GetInstance().GetZ(worldId, x, y, 4000f, 0f, instance.GetInstanceId());
            TeleportService.TeleportTo(player, instance, x, y, z);
        }
    }

    private WorldMapInstance GetOrCreateWorldMapInstance(Player player, int worldId)
    {
        if (player.GetWorldId() == worldId)
            return player.GetPosition().GetWorldMapInstance();
        else if (Aion.GameServer.World.World.GetInstance().GetWorldMap(worldId).IsInstanceType())
            return InstanceService.GetOrRegisterInstance(worldId, player);
        else
            return Aion.GameServer.World.World.GetInstance().GetWorldMap(worldId).GetMainWorldMapInstance();
    }
}
