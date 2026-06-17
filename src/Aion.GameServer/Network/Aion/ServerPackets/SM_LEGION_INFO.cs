using System.Collections.Generic;
using Aion.GameServer.Model.Team.Legion;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services.Abyss;
using Announcement = global::Aion.GameServer.Model.Team.Legion.Legion.Announcement;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_LEGION_INFO (Simple). Legion summary: name/level/ranking/permissions/contribution/disband/dominion + announcements. Legion.Announcement record accessors -> PascalCase; Collections.singletonList -> new List; time().getTime()/1000 -> ToUnixTimeMilliseconds()/1000. Legion/AbyssRankingCache red-tolerated.</summary>
public class SM_LEGION_INFO : AionServerPacket
{
    private readonly Legion legion;

    public SM_LEGION_INFO(Legion legion)
    {
        this.legion = legion;
    }

    protected override void WriteImpl(AionConnection con)
    {
        // Java parity (writeImpl audited 1:1 vs game-server/src/com/aionemu/gameserver/network/aion/serverpackets/SM_LEGION_INFO.java): 2026-06-17. Live Legion graph + AbyssRankingCache singleton.
        WriteS(legion.GetName());
        WriteC(legion.GetLegionLevel());
        WriteD(AbyssRankingCache.GetInstance().GetRankingListPosition(legion));
        WriteH(legion.GetDeputyPermission());
        WriteH(legion.GetCenturionPermission());
        WriteH(legion.GetLegionaryPermission());
        WriteH(legion.GetVolunteerPermission());
        WriteQ(legion.GetContributionPoints());
        WriteD(0x00); // unk
        WriteD(0x00); // unk
        WriteD(legion.GetDisbandTime());
        WriteD(legion.GetOccupiedLegionDominion());
        WriteD(legion.GetLastLegionDominion());
        WriteD(legion.GetCurrentLegionDominion());
        WriteAnnouncements();
    }

    /// <summary>
    /// The game client expects up to 7 announcements, but it only shows the first one, so only one is sent. The code could be
    /// simplified with just one announcement, but this implementation is more accurate and future-proof.
    /// </summary>
    private void WriteAnnouncements()
    {
        List<Announcement> announcements = new List<Announcement> { legion.GetAnnouncement() };
        for (int i = 0; i < 7; i++)
        {
            Announcement announcement = i < announcements.Count ? announcements[i] : null;
            WriteS(announcement == null ? "" : announcement.Message);
            if (announcement == null || announcement.Message.Length == 0) // empty string is a stop marker
                break;
            WriteD((int)(announcement.Time.ToUnixTimeMilliseconds() / 1000));
        }
    }
}
