using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Player;

namespace Aion.GameServer.Model.Autogroup;

/// <summary>Java parity: model/autogroup/AGPlayer (xTz). Java record → C# positional record.</summary>
public record AGPlayer(int ObjectId, Race Race, PlayerClass PlayerClass, string Name)
{
    public AGPlayer(Player player)
        : this(player.GetObjectId(), player.GetRace(), player.GetPlayerClass(), player.GetName())
    {
    }
}
