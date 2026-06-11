using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.House;
using Aion.GameServer.Model.Templates.Housing;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine;
using Aion.GameServer.QuestEngine.Model;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_HOUSE_DECORATE (Rolandas). Applies/clears a wall/floor/etc decoration part by line number. PartType/HouseDecoration/SM_HOUSE_EDIT red-tolerated.</summary>
public class CM_HOUSE_DECORATE : AionClientPacket
{
    private int objectId;
    private int lineNo, roomNo; // Line number (starts from 1 in 3.0 and from 2 in 3.5) of part in House render/update packet

    public CM_HOUSE_DECORATE(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        objectId = ReadD();
        ReadD(); // templateId (already known by objectId)
        lineNo = ReadUH();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        if (player == null)
            return;

        House house = player.GetActiveHouse();

        PartType? partType = PartTypeExtensions.GetForLineNr(lineNo);
        if (partType == null) // client may send lineNos which are not even implemented on client side (like 20-26)
            return;
        int roomNo = lineNo - partType.Value.GetStartLineNr();

        if (objectId == 0)
        { // change appearance to default and delete any applied custom decor
            house.GetRegistry().DiscardDecor(partType.Value, roomNo);
        }
        else
        { // apply decor and remove it from registry
            HouseDecoration decor = house.GetRegistry().GetDecorByObjId(objectId);
            house.GetRegistry().SetUsed(decor, roomNo);
            SendPacket(new SM_HOUSE_EDIT(4, 2, objectId)); // yes, in retail it's sent twice!
        }

        SendPacket(new SM_HOUSE_EDIT(4, 2, objectId));
        house.GetController().UpdateAppearance();
        QuestEngine.GetInstance().OnHouseItemUseEvent(new QuestEnv(null, player, 0));
    }
}
