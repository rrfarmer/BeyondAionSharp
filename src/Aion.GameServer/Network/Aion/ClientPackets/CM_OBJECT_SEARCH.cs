using System.Collections.Generic;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_OBJECT_SEARCH (Lyahim). Finds the nearest spawn of an npcId in the player's world and shows it on the map. DataManager.SPAWNS_DATA/SM_SHOW_NPC_ON_MAP red-tolerated.</summary>
public class CM_OBJECT_SEARCH : AionClientPacket
{
    private int npcId;

    public CM_OBJECT_SEARCH(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        this.npcId = ReadD();
    }

    protected override void RunImpl()
    {
        Player activePlayer = GetConnection().GetActivePlayer();
        if (activePlayer == null)
        {
            return;
        }
        SpawnSearchResult searchResult = DataManager.SPAWNS_DATA.GetNearestSpawnByNpcId(activePlayer, npcId, activePlayer.GetWorldId());
        if (searchResult != null)
            SendPacket(new SM_SHOW_NPC_ON_MAP(activePlayer, npcId, searchResult.GetWorldId(), searchResult.GetSpot().GetX(), searchResult.GetSpot().GetY(),
                searchResult.GetSpot().GetZ()));
        else
            SendPacket(SM_SYSTEM_MESSAGE.STR_FIND_POS_UNKNOWN_NAME());
    }
}
