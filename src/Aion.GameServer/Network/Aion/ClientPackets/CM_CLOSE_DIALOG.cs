using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_CLOSE_DIALOG. Client closes (or unselects) a dialog with a target object. DialogService red-tolerated.</summary>
public class CM_CLOSE_DIALOG : AionClientPacket
{
    /// <summary>Target object id that client wants to TALK WITH or 0 if wants to unselect</summary>
    private int targetObjectId;

    public CM_CLOSE_DIALOG(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        targetObjectId = ReadD();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        VisibleObject target = player.GetKnownList().GetObject(targetObjectId);
        DialogService.OnCloseDialog(player, target);
    }
}
