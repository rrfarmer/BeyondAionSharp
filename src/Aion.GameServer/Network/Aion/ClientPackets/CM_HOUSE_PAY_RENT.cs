using System;
using System.Collections.Generic;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.House;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.TaskManager.Tasks.Housing;
using Aion.GameServer.Utils.Time;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_HOUSE_PAY_RENT (Rolandas). Pays house maintenance for N weeks (client caps at 4 weeks ahead). Java java.time arithmetic mapped to DateTimeOffset; ChronoUnit.WEEKS.between -> TotalDays/7. MaintenanceTask/ServerTime red-tolerated.</summary>
public class CM_HOUSE_PAY_RENT : AionClientPacket
{
    private int weekCount;

    public CM_HOUSE_PAY_RENT(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        weekCount = ReadUC();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        House house = player.GetActiveHouse();
        long cost = HousingConfig.ENABLE_HOUSE_PAY ? house.GetLand().GetMaintenanceFee() * weekCount : 0;

        if (cost <= 0)
        {
            SendPacket(SM_SYSTEM_MESSAGE.STR_MSG_F2P_CASH_HOUSE_FEE_FREE());
            return;
        }

        if (player.GetInventory().GetKinah() < cost)
        {
            SendPacket(SM_SYSTEM_MESSAGE.STR_NOT_ENOUGH_MONEY());
            return;
        }

        DateTimeOffset nextPay = house.GetNextPay() != null ? house.GetNextPay().Value : MaintenanceTask.GetInstance().GetNextRun();
        for (int counter = 0; counter < weekCount; counter++)
            nextPay = MaintenanceTask.GetInstance().GetNextRunAfter(nextPay);

        DateTimeOffset nowMidnight = ServerTime.Now();
        nowMidnight = new DateTimeOffset(nowMidnight.Year, nowMidnight.Month, nowMidnight.Day, 0, 0, 0, nowMidnight.Offset); // .with(LocalTime.MIDNIGHT)
        long totalWeeksPaid = (long)((ServerTime.AtDate(nextPay) - nowMidnight).TotalDays / 7); // ChronoUnit.WEEKS.between
        if (totalWeeksPaid > 4) // client cap
            return;

        player.GetInventory().DecreaseKinah(cost);
        house.SetNextPay(nextPay);
        house.Save();
        SendPacket(new SM_HOUSE_PAY_RENT(weekCount));
    }
}
