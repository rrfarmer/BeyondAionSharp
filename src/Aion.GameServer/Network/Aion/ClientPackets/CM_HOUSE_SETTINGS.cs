using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.House;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils.Audit;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_HOUSE_SETTINGS (Rolandas). Updates house door state / owner-name display / sign notice and notifies. AbstractHouseInfoPacket.SIGN_NOTICE_MAX_LENGTH; HouseDoorState red-tolerated.</summary>
public class CM_HOUSE_SETTINGS : AionClientPacket
{
    private byte doorState;
    private bool showOwnerName;
    private string signNotice;

    public CM_HOUSE_SETTINGS(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        doorState = ReadC();
        showOwnerName = ReadC() == 1;
        signNotice = ReadS();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        if (player == null)
            return;

        if (signNotice.Length > AbstractHouseInfoPacket.SIGN_NOTICE_MAX_LENGTH)
        { // client limits sign notices to 64 chars but technically it supports more
            AuditLogger.Log(player, "sent string with more than 64 chars for house notice: " + signNotice);
            signNotice = signNotice.Substring(0, AbstractHouseInfoPacket.SIGN_NOTICE_MAX_LENGTH);
        }
        HouseDoorState? doorState = HouseDoorStates.Get((sbyte)this.doorState);
        House house = player.GetActiveHouse();
        if (doorState == null)
            NullLoggerFactory.Instance.CreateLogger(GetType().Name).LogWarning("{Player} sent unknown door state {DoorState} for {House}", player, this.doorState, house);
        else
            house.SetDoorState(doorState.Value);
        house.SetShowOwnerName(showOwnerName);
        house.SetSignNotice(signNotice);

        SendPacket(new SM_HOUSE_ACQUIRE(player.GetObjectId(), house.GetAddress().GetId(), true));
        house.GetController().UpdateAppearance();

        if (doorState == HouseDoorState.OPEN)
            SendPacket(SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_ORDER_OPEN_DOOR());
        else if (doorState == HouseDoorState.CLOSED_EXCEPT_FRIENDS)
        {
            house.GetController().KickVisitors(player, false, false);
            SendPacket(SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_ORDER_CLOSE_DOOR_WITHOUT_FRIENDS());
        }
        else if (doorState == HouseDoorState.CLOSED)
        {
            house.GetController().KickVisitors(player, true, false);
            SendPacket(SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_ORDER_CLOSE_DOOR_ALL());
        }
    }
}
