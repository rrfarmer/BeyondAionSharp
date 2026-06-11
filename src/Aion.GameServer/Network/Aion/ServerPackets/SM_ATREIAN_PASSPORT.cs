using System;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_ATREIAN_PASSPORT (ViAl, Neon). Atreian passport (daily login rewards): account creation date + per-passport id/stamps/rewardStatus/arriveDate. java.time.LocalDate -> DateOnly (Year/Month/Day); getArriveDate().getTime()/1000 -> ToUnixTimeMilliseconds()/1000. Passport/PassportsList red-tolerated.</summary>
public class SM_ATREIAN_PASSPORT : AionServerPacket
{
    private DateOnly accountCreationDate;
    private PassportsList passports;
    private int stamps;

    public SM_ATREIAN_PASSPORT(PassportsList passports, int stamps, DateOnly accountCreationDate)
    {
        this.accountCreationDate = accountCreationDate;
        this.passports = passports;
        this.stamps = stamps;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteH(accountCreationDate.Year);
        WriteH(accountCreationDate.Month);
        WriteH(accountCreationDate.Day);
        WriteH(passports.GetAllPassports().Count);
        foreach (Passport pp in passports.GetAllPassports())
        {
            WriteD(pp.GetId());
            WriteD(stamps); // wrong, this is the stamp count when each passport was received (current month sends current count for upcoming rewards)
            WriteD(pp.GetRewardStatus().GetId()); // 0 = not yet arrived (upcoming this months rewards), 1 = arrived and not taken, 2 = arrived and taken, 3 = not arrived (last months rewards)
            WriteD((int)(pp.GetArriveDate().ToUnixTimeMilliseconds() / 1000)); // for upcoming rewards it's the first login time each day
        }
    }
}
