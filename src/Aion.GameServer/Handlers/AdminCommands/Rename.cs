using System;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dao;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/Rename (xTz). Changes a player's name.</summary>
public class Rename : AdminCommand
{
    public Rename()
        : base("rename", "Changes a player's name.")
    {
        SetSyntaxInfo(
            "<new name> - Renames your target.",
            "<player name> <new name> [f] - Renames the given player (f = force rename, ignoring reserved names).");
    }

    protected override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length < 1)
        {
            SendInfo(admin);
            return;
        }

        string oldName = paramsArr.Length == 1 ? null : Util.ConvertName(paramsArr[0]);
        string newName = Util.ConvertName(paramsArr.Length == 1 ? paramsArr[0] : paramsArr[1]);
        Player renamed = paramsArr.Length == 1 && admin.GetTarget() is Player player ? player : Aion.GameServer.World.World.GetInstance().GetPlayer(oldName);
        PlayerCommonData renamedCommonData = renamed == null ? PlayerService.GetOrLoadPlayerCommonData(oldName) : renamed.GetCommonData();

        if (renamedCommonData == null)
        {
            PacketSendUtility.SendPacket(admin, oldName == null ? SM_SYSTEM_MESSAGE.STR_INVALID_TARGET() : SM_SYSTEM_MESSAGE.STR_NO_USER_NAMED(oldName));
            return;
        }
        else
        {
            oldName = renamedCommonData.GetName();
        }
        if (!NameRestrictionService.IsValidName(newName) || NameRestrictionService.IsForbidden(newName))
        {
            PacketSendUtility.SendPacket(admin, SM_SYSTEM_MESSAGE.STR_MSG_EDIT_CHAR_NAME_ERROR_WRONG_INPUT());
            return;
        }
        int nameReservationDurationDays = paramsArr.Length >= 3 && paramsArr[2].Equals("f", StringComparison.OrdinalIgnoreCase) ? 0 : NameConfig.RESERVE_OLD_NAME_DAYS;
        if (PlayerService.IsNameUsedOrReserved(oldName, newName, nameReservationDurationDays))
        {
            PacketSendUtility.SendPacket(admin, SM_SYSTEM_MESSAGE.STR_MSG_EDIT_CHAR_NAME_ALREADY_EXIST());
            return;
        }

        OldNamesDAO.InsertNames(renamedCommonData.GetPlayerObjId(), oldName, newName);
        renamedCommonData.SetName(newName);
        PlayerDAO.StorePlayerName(renamedCommonData);
        if (renamed != null)
            CM_APPEARANCE.OnPlayerNameChanged(renamed, oldName);
        SendInfo(admin, oldName + " has been renamed to " + newName);
    }
}
