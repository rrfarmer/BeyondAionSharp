using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Item.Actions;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services.Item;
using Aion.GameServer.Utils.Audit;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_TUNE (xTz). Identifies an unidentified item or applies a tuning scroll. ItemActionService/TuningAction red-tolerated.</summary>
public class CM_TUNE : AionClientPacket
{
    private int itemObjectId, tuningScrollObjectId;

    public CM_TUNE(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        itemObjectId = ReadD();
        tuningScrollObjectId = ReadD();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        if (player == null)
            return;

        Item item = player.GetInventory().GetItemByObjId(itemObjectId);
        if (item == null)
            return;

        if (!item.IsIdentified())
        {
            ItemActionService.IdentifyItem(player, item);
        }
        else if (tuningScrollObjectId != 0)
        {
            Item tuningScroll = player.GetInventory().GetItemByObjId(tuningScrollObjectId);
            if (tuningScroll == null)
                return;

            TuningAction action = tuningScroll.GetItemTemplate().GetActions().GetTuningAction();
            if (action != null && action.CanAct(player, tuningScroll, item))
                action.Act(player, tuningScroll, item);
        }
        else
        {
            AuditLogger.Log(player, "attempted to tune an already identified item without tuning scroll.");
        }
    }
}
