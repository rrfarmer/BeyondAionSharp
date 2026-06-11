using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Utils;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_CUSTOM_SETTINGS (Sweetkr). Updates the player's display/deny settings and broadcasts SM_CUSTOM_SETTINGS. PlayerSettings/SM_CUSTOM_SETTINGS red-tolerated.</summary>
public class CM_CUSTOM_SETTINGS : AionClientPacket
{
    private int display;
    private int deny;

    public CM_CUSTOM_SETTINGS(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        display = ReadUH(); // see SM_CUSTOM_SETTINGS.HIDE_* variables
        /**
         * 1 : view detail player 2 : trade 4 : party/force 8 : legion 16 : friend 32 : dual(pvp)
         */
        deny = ReadUH();
    }

    protected override void RunImpl()
    {
        Player activePlayer = GetConnection().GetActivePlayer();
        activePlayer.GetPlayerSettings().SetDisplay(display);
        activePlayer.GetPlayerSettings().SetDeny(deny);

        PacketSendUtility.BroadcastPacket(activePlayer, new SM_CUSTOM_SETTINGS(activePlayer), true);
    }
}
