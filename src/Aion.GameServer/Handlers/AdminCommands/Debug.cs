using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Aion.GameServer.Commons.Network;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>
/// Java parity: data/handlers/admincommands/Debug (Neon). Helps fixing runtime problems. Java enumerates the
/// live game-client connections by reflecting the static GameServer.nioServer field and walking its read-write
/// dispatcher selector keys; the C# reactor exposes the same observable connection set via the NioServer
/// singleton bridge + GetAllConnections() (gameplay-faithful-infra-idiomatic — the enumeration is the
/// connection-registry equivalent, the per-connection/per-player behavior is faithful).
/// </summary>
public class Debug : AdminCommand
{
    public Debug()
        : base("debug", "Helps fixing runtime problems.")
    {
        SetSyntaxInfo(
            "<connections> - Displays all connected game clients.",
            "<connectedPlayers> - Displays information about connected players.",
            "<dcBuggedPlayers> - Disconnects and attempts to save bugged players.");
    }

    protected override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length == 0)
        {
            SendInfo(admin);
            return;
        }

        if ("connections".Equals(paramsArr[0], System.StringComparison.OrdinalIgnoreCase))
        {
            List<AionConnection> connections = FindAionConnections(admin);
            if (connections != null)
            {
                SendInfo(admin, "Online clients:\n\t" + string.Join("\n\t", connections.Select(c => c.ToString())));
            }
        }
        else if ("connectedPlayers".Equals(paramsArr[0], System.StringComparison.OrdinalIgnoreCase))
        {
            List<Player> connectedPlayers = FindConnectedPlayers(admin);
            if (connectedPlayers != null)
            {
                string message = "Connected players (" + connectedPlayers.Count + "):";
                foreach (Player player in connectedPlayers)
                {
                    string details = "position: " + player.GetPosition().ToCoordString() + ", spawned: " + player.IsSpawned();
                    if (!player.IsInWorld())
                    {
                        details += ", " + ChatUtil.Color("not in world", Color.Red);
                    }
                    message += "\n\t" + player.GetName() + " [" + details + "]";
                }
                SendInfo(admin, message);
            }
        }
        else if ("dcBuggedPlayers".Equals(paramsArr[0], System.StringComparison.OrdinalIgnoreCase))
        {
            List<Player> buggedPlayers = FindConnectedPlayers(admin).Where(p => !p.IsInWorld()).ToList();
            if (buggedPlayers != null)
            {
                if (buggedPlayers.Count == 0)
                {
                    SendInfo(admin, "No bugged players found.");
                }
                else
                {
                    foreach (Player player in buggedPlayers)
                    {
                        player.GetController().CancelAllTasks(); // ensure to cancel item update task etc
                        player.GetCommonData().SetOnline(false);
                        PlayerService.StorePlayer(player);
                        player.GetClientConnection().SetActivePlayer(null);
                        player.GetClientConnection().Close();
                        player.SetClientConnection(null);
                    }
                    // Java parity: "...\n" + buggedPlayers (List<Player>.toString() = "[elem1, elem2]").
                    SendInfo(admin, "Saved most data and disconnected the following players:\n[" + string.Join(", ", buggedPlayers.Select(p => p.ToString())) + "]");
                }
            }
        }
    }

    private List<Player> FindConnectedPlayers(Player admin)
    {
        List<AionConnection> connections = FindAionConnections(admin);
        if (connections != null)
        {
            return connections.Select(c => c.GetActivePlayer()).Where(p => p != null)
                .OrderBy(p => p.GetName(), System.StringComparer.Ordinal).ToList();
        }
        return null;
    }

    private List<AionConnection> FindAionConnections(Player admin)
    {
        NioServer nioServer = NioServer.GetRegisteredInstance();
        if (nioServer == null)
        {
            SendInfo(admin, "NioServer is not running.");
            return null;
        }
        return nioServer.GetAllConnections().OfType<AionConnection>().ToList();
    }
}
