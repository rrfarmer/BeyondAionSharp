using System;
using System.Collections.Generic;
using Aion.GameServer.Dao;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

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
        command = ToAlias(command);
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
        command = ToAlias(command);
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

    /// <summary>
    /// The access of a command is stored and checked without the <c>//</c> prefix of admin commands, so the alias has to be
    /// normalized before it is compared with a grant.
    /// </summary>
    private static string ToAlias(string command)
    {
        string alias = command.StartsWith(AdminCommand.PREFIX, StringComparison.Ordinal) ? command.Substring(AdminCommand.PREFIX.Length) : command;
        return alias.ToLower();
    }

    public static bool HasAccess(int playerId, string command)
    {
        commandAccesses.TryGetValue(playerId, out ISet<string> commands);
        return commands != null && commands.Contains(command);
    }
}
