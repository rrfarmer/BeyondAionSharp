using System;
using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/Access (ViAl, Neon). Chat command and access level management.</summary>
public class Access : AdminCommand
{
    private readonly Dictionary<int, sbyte> oldAccessLevels = new();

    public Access()
        : base("access", "Chat command and access level management.")
    {
        SetSyntaxInfo(
            "<add> <player name> <command name> - Grants the player access to the given chat command.",
            "<remove> <player name> <command name> - Removes the player's access to the given chat command.",
            "<removeall> <player name> - Removes all granted accesses.",
            "<level> <number> - Temporarily sets your accesslevel to a given lower value (for test purposes).",
            "<level> <reset> - Resets your accesslevel to the original value."
        );
    }

    protected override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length <= 1)
        {
            SendInfo(admin);
            return;
        }
        string cmd = paramsArr[0].ToLower();
        switch (cmd)
        {
            case "add":
            case "remove":
            {
                if (paramsArr.Length < 3)
                {
                    SendInfo(admin);
                    return;
                }
                string playerName = Util.ConvertName(paramsArr[1]);
                string commandName = paramsArr[2];
                PlayerCommonData pcd = PlayerService.GetOrLoadPlayerCommonData(playerName);
                if (pcd == null)
                {
                    SendInfo(admin, "Player with name " + playerName + " doesn't exists!");
                    return;
                }
                if (cmd.Equals("add"))
                    CommandsAccessService.GiveAccess(admin, pcd.GetPlayerObjId(), commandName);
                else
                    CommandsAccessService.RemoveAccess(admin, pcd.GetPlayerObjId(), commandName);
                break;
            }
            case "removeall":
            {
                string playerName = Util.ConvertName(paramsArr[1]);
                PlayerCommonData pcd = PlayerService.GetOrLoadPlayerCommonData(playerName);
                if (pcd == null)
                {
                    SendInfo(admin, "Player with name " + playerName + " doesn't exists!");
                    return;
                }
                if (CommandsAccessService.RemoveAllAccesses(pcd.GetPlayerObjId()))
                    SendInfo(admin, "Removed all custom chat command accesses from player " + pcd.GetName());
                else
                    SendInfo(admin, "Nothing to remove from player " + pcd.GetName());
                break;
            }
            case "level":
            {
                sbyte currentLevel = admin.GetAccount().GetAccessLevel();
                sbyte maxLevel = oldAccessLevels.TryGetValue(admin.GetObjectId(), out sbyte stored) ? stored : currentLevel;
                if ("reset".Equals(paramsArr[1], StringComparison.OrdinalIgnoreCase))
                {
                    if (maxLevel == currentLevel)
                    {
                        SendInfo(admin, "Nothing to reset.");
                        return;
                    }
                    admin.GetAccount().SetAccessLevel(maxLevel);
                    SendInfo(admin, "Your access level has been reset.");
                }
                else
                {
                    sbyte level = sbyte.TryParse(paramsArr[1], out sbyte parsed) ? parsed : (sbyte)-1;
                    if (level == -1 || level > maxLevel)
                    {
                        SendInfo(admin, "Invalid access level.");
                        return;
                    }
                    if (level == currentLevel)
                    {
                        SendInfo(admin, "You already have access level " + level + ".");
                        return;
                    }
                    admin.GetAccount().SetAccessLevel(level);
                    if (level < GetLevel() && !CommandsAccessService.HasAccess(admin.GetObjectId(), GetAlias()))
                    {
                        CommandsAccessService.GiveTemporaryAccess(admin, admin.GetObjectId(), GetAlias());
                    }
                    SendInfo(admin, "Your access level has been changed.");
                }
                currentLevel = admin.GetAccount().GetAccessLevel();
                if (currentLevel == maxLevel)
                {
                    oldAccessLevels.Remove(admin.GetObjectId());
                }
                else
                {
                    if (!oldAccessLevels.ContainsKey(admin.GetObjectId()))
                        oldAccessLevels[admin.GetObjectId()] = maxLevel;
                }
                if (currentLevel > GetLevel() && CommandsAccessService.HasAccess(admin.GetObjectId(), GetAlias()))
                {
                    CommandsAccessService.RemoveAccess(admin, admin.GetObjectId(), GetAlias());
                }
                admin.GetController().OnChangedPlayerAttributes();
                break;
            }
            default:
                SendInfo(admin);
                break;
        }
    }
}
