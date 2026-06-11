using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services.Items;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_ITEM_REMODEL (Sarynth). Remodels an item (keep appearance of one, extract from another). ItemRemodelService red-tolerated.</summary>
public class CM_ITEM_REMODEL : AionClientPacket
{
    private int keepItemId;
    private int extractItemId;

    public CM_ITEM_REMODEL(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        ReadD(); // npcId
        keepItemId = ReadD();
        extractItemId = ReadD();
        ReadD(); // unk 0
    }

    protected override void RunImpl()
    {
        Player activePlayer = GetConnection().GetActivePlayer();
        ItemRemodelService.RemodelItem(activePlayer, keepItemId, extractItemId);
    }
}
