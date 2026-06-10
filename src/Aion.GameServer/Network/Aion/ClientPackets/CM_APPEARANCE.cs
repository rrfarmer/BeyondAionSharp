using System.Collections.Generic;
using Aion.GameServer.Dao;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Team.Legion;
using Aion.GameServer.Model.Templates.Item.Actions;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Player;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Audit;
using Aion.GameServer.World;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_APPEARANCE (xTz, Neon). Change character name (0), legion name (1), or use cosmetic item (2). PlayerService/LegionService/OldNamesDAO/PlayerDAO red-tolerated.</summary>
public class CM_APPEARANCE : AionClientPacket
{
    private byte type;
    private int itemObjId;
    private string newName;

    public CM_APPEARANCE(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        type = ReadC();
        ReadC();
        ReadH();
        itemObjId = ReadD();
        switch (type)
        {
            case 0:
            case 1:
                newName = ReadS();
                break;
        }
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();

        switch (type)
        {
            case 0: // Change Char Name
                TryChangeCharacterName(player, Util.ConvertName(newName), itemObjId);
                break;
            case 1: // Change Legion Name
                TryChangeLegionName(player, newName, itemObjId);
                break;
            case 2: // cosmetic items
                TryUseCosmeticItem(player, itemObjId);
                break;
        }
    }

    private void TryChangeCharacterName(Player player, string newName, int itemObjId)
    {
        string oldName = player.GetName();
        if (oldName.Equals(newName))
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_EDIT_CHAR_NAME_ERROR_SAME_YOUR_NAME());
        else if (!NameRestrictionService.IsValidName(newName) || NameRestrictionService.IsForbidden(newName))
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_EDIT_CHAR_NAME_ERROR_WRONG_INPUT());
        else if (PlayerService.IsNameUsedOrReserved(oldName, newName))
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_EDIT_CHAR_NAME_ALREADY_EXIST());
        else if ((player.GetInventory().GetItemByObjId(itemObjId).GetItemId() != 169670000 && player.GetInventory().GetItemByObjId(itemObjId).GetItemId() != 169670001)
            || !player.GetInventory().DecreaseByObjectId(itemObjId, 1))
            AuditLogger.Log(player, "tried to rename himself without coupon");
        else
        {
            OldNamesDAO.InsertNames(player.GetObjectId(), oldName, newName);

            player.GetCommonData().SetName(newName);
            PlayerDAO.StorePlayer(player);
            OnPlayerNameChanged(player, oldName);
        }
    }

    public static void OnPlayerNameChanged(Player player, string oldName)
    {
        World.GetInstance().UpdateCachedPlayerName(oldName, player);
        if (player.IsLegionMember())
        {
            LegionService.GetInstance().AddHistory(player.GetLegion(), oldName, LegionHistoryAction.CHARACTER_RENAME, player.GetName());
            player.GetLegionMember().SetPlayerData(player); // no need to broadcast SM_LEGION_UPDATE_MEMBER here, since SM_RENAME already handles it
        }
        PacketSendUtility.BroadcastToWorld(new SM_RENAME(player, oldName)); // broadcast to world to update all friendlists, housing npcs, etc.
    }

    private void TryChangeLegionName(Player player, string newName, int itemObjId)
    {
        Legion legion = player.GetLegion();
        if (legion == null || !player.GetLegionMember().IsBrigadeGeneral())
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_EDIT_GUILD_NAME_ERROR_ONLY_MASTER_CAN_CHANGE_NAME());
            return;
        }
        LegionService.GetInstance().TryRename(legion, newName, player, itemObjId);
    }

    private void TryUseCosmeticItem(Player player, int itemObjId)
    {
        Item item = player.GetInventory().GetItemByObjId(itemObjId);
        if (item != null)
        {
            foreach (AbstractItemAction action in item.GetItemTemplate().GetActions().GetItemActions())
            {
                if (action is CosmeticItemAction && action.CanAct(player, null, null))
                {
                    action.Act(player, null, item);
                    break;
                }
            }
        }
    }
}
