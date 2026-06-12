using System.Collections.Generic;
using Aion.GameServer.Configs.Administration;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_VIEW_PLAYER_DETAILS (Avol). Shows another player's equipment details unless they denied it (GM bypass). SM_VIEW_PLAYER_DETAILS red-tolerated.</summary>
public class CM_VIEW_PLAYER_DETAILS : AionClientPacket
{
    private int targetObjectId;

    public CM_VIEW_PLAYER_DETAILS(int opcode, ISet<State> validStates)
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
        Player target = player.GetKnownList().GetPlayer(targetObjectId);
        if (target == null)
            return;

        if (!target.GetPlayerSettings().IsInDeniedStatus(DeniedStatus.VIEW_DETAILS) || player.HasAccess(AdminConfig.VIEW_PLAYER_DETAILS))
            SendPacket(new SM_VIEW_PLAYER_DETAILS(target.GetEquipment().GetEquippedItemsWithoutStigma(), target));
        else
            SendPacket(SM_SYSTEM_MESSAGE.STR_MSG_REJECTED_WATCH(target.GetName()));
    }
}
