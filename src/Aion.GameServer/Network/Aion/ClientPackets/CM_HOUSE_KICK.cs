using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.House;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Utils.Audit;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_HOUSE_KICK (Rolandas). Kicks visitors from the player's house (option 1 normal, 2 incl. friends). House/AuditLogger red-tolerated.</summary>
public class CM_HOUSE_KICK : AionClientPacket
{
    private byte option;

    public CM_HOUSE_KICK(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        option = ReadC();
        ReadH();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        if (player == null)
            return;

        House house = player.GetActiveHouse();
        if (house == null)
        {
            AuditLogger.Log(player, "tried to kick players from house without owning one");
            return;
        }
        if (option == 1)
            house.GetController().KickVisitors(player, false, false);
        else if (option == 2)
            house.GetController().KickVisitors(player, true, false);
    }
}
