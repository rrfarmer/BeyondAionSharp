using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_BONUS_TITLE (-Enomine-). Sets the player's bonus title (0xFFFF clears). Player red-tolerated.</summary>
public class CM_BONUS_TITLE : AionClientPacket
{
    private int bonusTitleId;

    public CM_BONUS_TITLE(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        bonusTitleId = ReadUH();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        if (bonusTitleId != 0xFFFF)
            if (!player.GetTitleList().Contains(bonusTitleId))
                return;

        player.GetTitleList().SetBonusTitle(bonusTitleId);
    }
}
