using System.Collections.Generic;
using Aion.GameServer.Dao;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Utils.Audit;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_CHARACTER_EDIT (IlBuono, Neon). Client requests plastic-surgery / gender-switch edit of an existing character (in edit mode), consuming the appropriate ticket. PlayerEnterWorldService/PlayerAppearanceDAO red-tolerated.</summary>
public class CM_CHARACTER_EDIT : AbstractCharacterEditPacket
{
    private int objectId;

    public CM_CHARACTER_EDIT(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        objectId = ReadD();
        ReadBasicInfo(false);
        ReadAppearance();
    }

    protected override void RunImpl()
    {
        AionConnection client = GetConnection();
        PlayerAccountData playerAccData = client.GetAccount().GetPlayerAccountData(objectId);
        if (playerAccData == null || !playerAccData.GetPlayerCommonData().IsInEditMode())
            return;
        PlayerEnterWorldService.EnterWorld(client, objectId);
        Player player = client.GetActivePlayer();

        bool isGenderSwitch = player.GetGender() != gender;
        if (CheckOrRemoveTicket(player, isGenderSwitch, true))
        {
            bool spawnedBeforeAttributesChanged = player.IsSpawned(); // just in case CM_LEVEL_READY was sent early
            if (isGenderSwitch)
                player.GetCommonData().SetGender(gender); // no need to save gender here, will be saved periodically and on logout
            player.SetPlayerAppearance(playerAppearance);
            PlayerAppearanceDAO.Store(player); // save new appearance
            if (spawnedBeforeAttributesChanged)
                player.GetController().OnChangedPlayerAttributes();
        }
        else
        { // can only happen if you illegally enter the character edit screen
            AuditLogger.Log(player, "tried to apply their plastic surgery without a ticket.");
        }
    }

    public static bool CheckOrRemoveTicket(Player player, bool isGenderSwitch, bool removeTicket)
    {
        int[] ticketIds = isGenderSwitch ? new int[] { 169660000, 169660001, 169660002, 169660003, 169660004 } : new int[] { 169650000, 169650001, 169650002, 169650003, 169650004, 169650005, 169650006, 169650007, 169650008 };
        foreach (int ticketId in ticketIds)
        {
            if (removeTicket && player.GetInventory().DecreaseByItemId(ticketId, 1) || !removeTicket && player.GetInventory().GetItemCountByItemId(ticketId) > 0)
            {
                return true;
            }
        }
        return false;
    }
}
