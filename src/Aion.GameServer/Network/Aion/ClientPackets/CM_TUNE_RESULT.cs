using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Services.Item;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Audit;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_TUNE_RESULT (Estrayl). Accepts/rejects a pending re-identification result. ItemActionService/SM_INVENTORY_UPDATE_ITEM red-tolerated.</summary>
public class CM_TUNE_RESULT : AionClientPacket
{
    private int itemObjectId;
    private bool hasAccepted;

    public CM_TUNE_RESULT(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        itemObjectId = ReadD();
        hasAccepted = ReadC() == 1;
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        Item itemToTune = player.GetInventory().GetItemByObjId(itemObjectId);
        if (itemToTune != null)
        {
            bool auditInvalidEvent = !hasAccepted && itemToTune.GetPendingTuneResult() != null && itemToTune.GetPendingTuneResult().IsAttributeOnly();
            if (hasAccepted || auditInvalidEvent)
            {
                if (auditInvalidEvent)
                    AuditLogger.Log(player, "tried to cancel a attribute re-identification which is not possible by default");
                ItemActionService.ApplyTuneResult(player, itemToTune);
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_ITEM_REIDENTIFY_APPLY_YES(itemToTune.GetL10n()));
            }
            else
            {
                itemToTune.SetPendingTuneResult(null);
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_ITEM_REIDENTIFY_APPLY_NO());
            }
            PacketSendUtility.SendPacket(player, new SM_INVENTORY_UPDATE_ITEM(player, itemToTune));
        }
    }
}
