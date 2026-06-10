using System;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.House;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;
using Aion.GameServer.Utils.Time;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_HOUSE_OWNER_INFO (Rolandas, Neon). Active/inactive house + owner state + weeks-until-pay. java.time ZonedDateTime->DateTimeOffset, DayOfWeek->System.DayOfWeek, Duration.between(..).toDays()->(long)(b-a).TotalDays (truncates toward zero). House/HousingService/HouseOwnerState/ServerTime red-tolerated.</summary>
public class SM_HOUSE_OWNER_INFO : AionServerPacket
{
    private int playerHouseOwnerState;
    private House activeHouse;
    private House inactiveHouse;

    public SM_HOUSE_OWNER_INFO(Player player)
    {
        foreach (House house in player.GetHouses())
        {
            if (house.IsInactive())
                inactiveHouse = house;
            else
                activeHouse = house;
        }
        if (activeHouse == null)
        {
            playerHouseOwnerState = HouseOwnerState.SINGLE_HOUSE.GetId();
            if (HousingService.GetInstance().CanOwnHouse(player, false))
                playerHouseOwnerState |= HouseOwnerState.BIDDING_ALLOWED.GetId();
        }
        else
        {
            playerHouseOwnerState = HouseOwnerState.HAS_OWNER.GetId() | HouseOwnerState.BIDDING_ALLOWED.GetId();
        }
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(activeHouse == null ? 0 : activeHouse.GetAddress().GetId());
        WriteD(activeHouse == null ? 0 : activeHouse.GetBuilding().GetId());
        WriteC(playerHouseOwnerState); // (SINGLE_HOUSE | BIDDING_ALLOWED) enables studio buy button at Parrine and disables house icon in profile
        WriteC(activeHouse == null ? 0 : activeHouse.GetTownLevel());
        WriteD(CalculateWeeksUntilNextPay()); // controls date and color in player profile house icon tooltip, as well as overdue pay amount when paying
        WriteD(inactiveHouse == null ? 0 : inactiveHouse.GetAddress().GetId());
        WriteD(inactiveHouse == null ? 0 : inactiveHouse.GetBuilding().GetId());
        WriteD(inactiveHouse == null ? 0 : inactiveHouse.SecondsUntilGraceEnd()); // seconds until new house (inactiveHouse) will be activated / old one (activeHouse) gets removed
    }

    private int CalculateWeeksUntilNextPay()
    {
        int weeks = 0;
        if (activeHouse != null)
        {
            if (activeHouse.GetNextPay() == null)
            { // newly acquired houses have one week free, nextPay will be set on the next house maintenance
                weeks = 1;
            }
            else
            {
                DateTimeOffset now = ServerTime.Now();
                bool isSundayAfterAuction = now.DayOfWeek == DayOfWeek.Sunday && now.Hour >= 12;
                long days = (long)(activeHouse.GetNextPay().Value - now).TotalDays;
                weeks = (int)(days / 7);
                if (days < 0 && isSundayAfterAuction) // workaround for auction day, client counts sunday afternoon to new week
                    weeks--;
                else if (days >= 0 && !isSundayAfterAuction)
                    weeks++;
            }
        }
        return weeks;
    }
}
