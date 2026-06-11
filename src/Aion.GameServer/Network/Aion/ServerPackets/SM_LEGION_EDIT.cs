using Aion.GameServer.Model.Team.Legion;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services.Abyss;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_LEGION_EDIT (Simple). Legion field update by type (level/ranking-pos/permissions/contributions/warehouse-kinah/announcement/disband/recover). Converges AbyssPointsService/AbyssRankingCache. switch-on-type; Legion.Announcement record accessors; Timestamp.getTime()/1000->ToUnixTimeMilliseconds()/1000. Legion/AbyssRankingCache red-tolerated.</summary>
public class SM_LEGION_EDIT : AionServerPacket
{
    private int type;
    private Legion legion;
    private int unixTime;
    private string announcement;

    public SM_LEGION_EDIT(int type)
    {
        this.type = type;
    }

    public SM_LEGION_EDIT(int type, Legion legion)
    {
        this.type = type;
        this.legion = legion;
    }

    public SM_LEGION_EDIT(int type, int unixTime)
    {
        this.type = type;
        this.unixTime = unixTime;
    }

    public SM_LEGION_EDIT(Legion.Announcement announcement)
    {
        this.type = 0x05;
        this.announcement = announcement.Message();
        this.unixTime = (int)(announcement.Time().ToUnixTimeMilliseconds() / 1000);
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteC(type);
        switch (type)
        {
            case 0x00: // Change Legion Level
                WriteC(legion.GetLegionLevel());
                break;
            case 0x01: // Change Abyss Ranking List Position
                WriteD(AbyssRankingCache.GetInstance().GetRankingListPosition(legion));
                break;
            case 0x02: // Change Legion Permissions
                WriteH(legion.GetDeputyPermission());
                WriteH(legion.GetCenturionPermission());
                WriteH(legion.GetLegionaryPermission());
                WriteH(legion.GetVolunteerPermission());
                break;
            case 0x03: // Change Legion Contributions
                WriteQ(legion.GetContributionPoints());
                break;
            case 0x04:
                WriteQ(legion.GetLegionWarehouse().GetKinah());
                break;
            case 0x05: // Change Legion Announcement
                WriteS(announcement);
                WriteD(unixTime);
                break;
            case 0x06: // Disband Legion
                WriteD(unixTime);
                break;
            case 0x07: // Recover Legion
                break;
            case 0x08: // Refresh Legion Announcement?
                break;
        }
    }
}
