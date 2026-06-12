using System.Collections.Generic;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_CLIENT_COMMAND_ROLL (Rhys2002). Received when a player types /roll; maxRoll is optional and defaults to 100. Rnd/SM_SYSTEM_MESSAGE red-tolerated.</summary>
public class CM_CLIENT_COMMAND_ROLL : AionClientPacket
{
    private int maxRoll;

    public CM_CLIENT_COMMAND_ROLL(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        maxRoll = ReadD();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        if (maxRoll <= 0) // client sends 100 on /roll 0 but negative numbers are passed through for whatever reason
            maxRoll = 100;
        int roll = Rnd.Get(1, maxRoll);
        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_DICE_CUSTOM_ME(roll, maxRoll));
        PacketSendUtility.BroadcastPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_DICE_CUSTOM_OTHER(player.GetName(), roll, maxRoll));
    }
}
