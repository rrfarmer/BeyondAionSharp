using System.Collections.Generic;
using System.Globalization;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items.Storage;
using Aion.GameServer.Model.Team.Legion;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_LEGION_WH_KINAH (ATracer). Withdraws (0) / deposits (1) kinah to the legion warehouse with permission checks + history. LegionService/LegionPermissionsMask red-tolerated.</summary>
public class CM_LEGION_WH_KINAH : AionClientPacket
{
    private long amount;
    private byte actionType;

    public CM_LEGION_WH_KINAH(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        amount = ReadQ();
        actionType = ReadC();
    }

    protected override void RunImpl()
    {
        Player activePlayer = GetConnection().GetActivePlayer();
        LegionMember legionMember = activePlayer.GetLegionMember();
        if (legionMember == null)
            return;
        switch (actionType)
        {
            case 0:
                if (!legionMember.HasRights(LegionPermissionsMask.WH_WITHDRAWAL))
                {
                    // You do not have the authority to use the Legion warehouse.
                    PacketSendUtility.SendPacket(activePlayer, SM_SYSTEM_MESSAGE.STR_GUILD_WAREHOUSE_NO_RIGHT());
                    return;
                }
                if (activePlayer.GetStorage(StorageType.LEGION_WAREHOUSE.GetId()).TryDecreaseKinah(amount))
                {
                    activePlayer.GetInventory().IncreaseKinah(amount);
                    LegionService.GetInstance().AddHistory(legionMember.GetLegion(), activePlayer.GetName(), LegionHistoryAction.KINAH_WITHDRAW, amount.ToString(CultureInfo.InvariantCulture));
                }
                break;
            case 1:
                if (!legionMember.HasRights(LegionPermissionsMask.WH_DEPOSIT))
                {
                    // You do not have the authority to use the Legion warehouse.
                    PacketSendUtility.SendPacket(activePlayer, SM_SYSTEM_MESSAGE.STR_GUILD_WAREHOUSE_NO_RIGHT());
                    return;
                }
                if (activePlayer.GetInventory().TryDecreaseKinah(amount))
                {
                    activePlayer.GetStorage(StorageType.LEGION_WAREHOUSE.GetId()).IncreaseKinah(amount);
                    LegionService.GetInstance().AddHistory(legionMember.GetLegion(), activePlayer.GetName(), LegionHistoryAction.KINAH_DEPOSIT, amount.ToString(CultureInfo.InvariantCulture));
                }
                break;
        }
    }
}
