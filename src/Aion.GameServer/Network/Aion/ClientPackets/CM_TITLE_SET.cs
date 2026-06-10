using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Network.Aion;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_TITLE_SET (Nemiroff, cura). Sets the player's display title (0xFFFF clears). TitleList red-tolerated.</summary>
public class CM_TITLE_SET : AionClientPacket
{
    private int titleId;

    public CM_TITLE_SET(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        titleId = ReadUH();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        if (titleId != 0xFFFF)
            if (!player.GetTitleList().Contains(titleId))
                return;

        player.GetTitleList().SetDisplayTitle(titleId);
    }
}
