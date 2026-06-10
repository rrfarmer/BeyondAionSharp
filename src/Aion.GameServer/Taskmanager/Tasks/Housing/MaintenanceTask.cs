using System;
using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.House;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Mail;
using Aion.GameServer.Services.Player;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Taskmanager.Tasks.Housing;

/// <summary>
/// Handles house maintenance as well as impoundment if a player didn't pay for two weeks (Java parity:
/// taskmanager/tasks/housing/MaintenanceTask, Rolandas, Neon) : AbstractCronTask. java.util.Date/Timestamp→DateTimeOffset
/// (after→&gt;, before→&lt;, getTime→ToUnixTimeMilliseconds, toInstant().plus(14,DAYS)→AddDays(14)); stream filter/collect→
/// Where/ToList; Collections.emptyList→new List. HousingService/HousingBidService/MailFormatter red-tolerated.
/// </summary>
public class MaintenanceTask : AbstractCronTask
{
    private static readonly MaintenanceTask instance = new();

    public static MaintenanceTask GetInstance()
    {
        return instance;
    }

    private MaintenanceTask() : base(HousingConfig.HOUSE_MAINTENANCE_TIME)
    {
    }

    protected override void ExecuteTask()
    {
        List<House> housesToMaintain = FindHousesToMaintain();
        log.LogInformation("Executing house maintenance for " + housesToMaintain.Count + " houses");

        DateTimeOffset now = DateTimeOffset.UtcNow;
        foreach (House house in housesToMaintain)
        {
            if (house.GetNextPay() == null) // the first week is free for newly acquired houses
            {
                house.SetNextPay(GetNextRun());
                house.Save();
                continue;
            }
            else if (house.GetNextPay().Value > now)
                continue;

            PlayerCommonData pcd = PlayerService.GetOrLoadPlayerCommonData(house.GetOwnerId());
            if (pcd == null) // player got deleted
            {
                PutHouseToAuction(house, null);
                continue;
            }

            string ownerName = house.GetOwnerName();
            long compensationKinah = 0;
            DateTimeOffset impoundDate = CalculateImpoundDate(house.GetNextPay().Value);
            if (impoundDate.ToUnixTimeMilliseconds() <= DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
            {
                PutHouseToAuction(house, pcd);
                // return 90% of the house cost (https://aion.fandom.com/wiki/Housing)
                compensationKinah = (long)(house.GetDefaultAuctionPrice() * 0.9f);
            }

            MailFormatter.SendHouseMaintenanceMail(house, ownerName, impoundDate.ToUnixTimeMilliseconds(), compensationKinah);
        }
    }

    private DateTimeOffset CalculateImpoundDate(DateTimeOffset housePaidUntil)
    {
        DateTimeOffset paymentDueDate = housePaidUntil.AddDays(14); // player must pay within two weeks
        DateTimeOffset impoundDate = DateTimeOffset.UtcNow;
        while (impoundDate < paymentDueDate)
        {
            impoundDate = GetNextRunAfter(impoundDate);
        }
        return impoundDate;
    }

    private List<House> FindHousesToMaintain()
    {
        if (!HousingConfig.ENABLE_HOUSE_PAY)
            return new List<House>();
        return HousingService.GetInstance().GetCustomHouses()
            .Where(house => !house.IsInactive() && house.GetOwnerId() != 0).ToList();
    }

    private void PutHouseToAuction(House house, PlayerCommonData owner)
    {
        HousingService.GetInstance().ChangeOwner(house, 0);
        HousingBidService.GetInstance().Auction(house, house.GetDefaultAuctionPrice());
        log.LogInformation("Auctioned house " + house.GetAddress().GetId() + " because " + (owner == null ? "owner got deleted." : "maintenance fee was overdue."));
        if (owner != null && owner.IsOnline())
            PacketSendUtility.SendPacket(owner.GetPlayer(), SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_SEQUESTRATE());
    }
}
