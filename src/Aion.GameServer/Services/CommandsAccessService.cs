using System.Collections.Generic;
using Aion.GameServer.Dao;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Chathandlers;

namespace Aion.GameServer.Services;

/// <summary>Java parity: services/CommandsAccessService (ViAl, Neon).</summary>
public class CommandsAccessService
{
    private static Dictionary<int, ISet<string>> commandAccesses = new Dictionary<int, ISet<string>>();

    private CommandsAccessService()
    {
    }

    public static void LoadAccesses()
    {
        commandAccesses = CommandsAccessDAO.LoadAccesses();
    }

    public static void GiveTemporaryAccess(Player admin, int playerId, string command)
    {
        GiveAccess(admin, playerId, command, true);
    }

    public static void GiveAccess(Player admin, int playerId, string command)
    {
        GiveAccess(admin, playerId, command, false);
    }

    private static void GiveAccess(Player admin, int playerId, string command, bool isTemporary)
    {
        if (HasAccess(playerId, command))
        {
            PacketSendUtility.SendMessage(admin, "This player already has access on command " + command);
            return;
        }
        if (!ChatProcessor.GetInstance().IsCommandExists(command))
        {
            PacketSendUtility.SendMessage(admin, "There is no such admin command as \"" + command + "\"");
            return;
        }
        commandAccesses.TryGetValue(playerId, out ISet<string> commands);
        if (commands == null)
            commands = new HashSet<string>();
        commands.Add(command);
        commandAccesses[playerId] = commands;
        if (!isTemporary)
            CommandsAccessDAO.AddAccess(playerId, command);
        PacketSendUtility.SendMessage(admin, "Command access was granted successfuly.");
    }

    public static void RemoveAccess(Player admin, int playerId, string command)
    {
        if (!HasAccess(playerId, command))
        {
            PacketSendUtility.SendMessage(admin, "This player has no access on command " + command);
            return;
        }
        ISet<string> commands = commandAccesses[playerId];
        commands.Remove(command);
        CommandsAccessDAO.RemoveAccess(playerId, command);
        PacketSendUtility.SendMessage(admin, "Command access was removed successfully");
    }

    public static bool RemoveAllAccesses(int playerId)
    {
        commandAccesses.TryGetValue(playerId, out ISet<string> commands);
        if (commands != null)
        {
            commands.Clear();
            CommandsAccessDAO.RemoveAllAccesses(playerId);
            return true;
        }
        return false;
    }

    public static bool HasAccess(int playerId, string command)
    {
        commandAccesses.TryGetValue(playerId, out ISet<string> commands);
        return commands != null && commands.Contains(command);
    }
}
