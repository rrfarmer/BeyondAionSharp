using System.Collections.Generic;
using Aion.GameServer.Model.Town;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_TOWNS_LIST (ViAl). Sends town id/level/levelUpDate. getLevelUpDate().getTime()/1000 -> ToUnixTimeMilliseconds()/1000; Map.values iteration. Town red-tolerated.</summary>
public class SM_TOWNS_LIST : AionServerPacket
{
    private IDictionary<int, Town> towns;

    public SM_TOWNS_LIST(IDictionary<int, Town> towns)
    {
        this.towns = towns;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteH(towns.Count);
        foreach (Town town in towns.Values)
        {
            WriteD(town.GetId());
            WriteD(town.GetLevel());
            WriteD((int)(town.GetLevelUpDate().ToUnixTimeMilliseconds() / 1000));
        }
    }
}
