using System.Collections.Generic;
using Aion.GameServer.Model.Team;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team.Alliance;
using Aion.GameServer.Model.Team.Common.Legacy;
using Aion.GameServer.Model.Team.League;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_ALLIANCE_INFO (Sarynth, xTz). Full alliance/league info: leader/vice-captains, loot rules, team type, league captain data. Private inner AllianceInfo POJO preserved as nested class. PlayerAlliance/League/LootGroupRules red-tolerated.</summary>
public class SM_ALLIANCE_INFO : AionServerPacket
{
    private LootGroupRules lootRules, lootLeagueRules;
    private PlayerAlliance alliance;
    private int leaderid;
    private int groupid;
    private int type;
    private int subType;
    private readonly int messageId;
    private readonly string message;
    private int leagueId;
    private readonly List<AllianceInfo> leagueData = new List<AllianceInfo>();
    public const int VICECAPTAIN_PROMOTE = 1300984;
    public const int VICECAPTAIN_DEMOTE = 1300985;
    public const int LEAGUE_ALLIANCE_ENTERED = 1400560; // Your alliance has joined %0's Alliance League.
    public const int LEAGUE_JOINED_ALLIANCE = 1400561; // %0's alliance has joined the Alliance League.
    public const int LEAGUE_LEFT_ME = 1400571;
    public const int LEAGUE_LEFT_HIM = 1400572;
    public const int LEAGUE_EXPEL = 1400574;
    public const int LEAGUE_EXPELLED = 1400576;
    public const int LEAGUE_DISPERSED = 1400579;

    private class AllianceInfo
    {
        private int alliancePosition;
        private int allianceObjectId;
        private int memberCount;
        private string captainName = "";
        private int captainWorldId = 0;

        public int GetAlliancePosition()
        {
            return alliancePosition;
        }

        public void SetAlliancePosition(int alliancePosition)
        {
            this.alliancePosition = alliancePosition;
        }

        public int GetAllianceObjectId()
        {
            return allianceObjectId;
        }

        public void SetAllianceObjectId(int allianceObjectId)
        {
            this.allianceObjectId = allianceObjectId;
        }

        public void SetMemberCount(int memberCount)
        {
            this.memberCount = memberCount;
        }

        public int GetMemberCount()
        {
            return memberCount;
        }

        public string GetCaptainName()
        {
            return captainName;
        }

        public void SetCaptainName(string captainName)
        {
            this.captainName = captainName;
        }

        public int GetCaptainWorldId()
        {
            return captainWorldId;
        }

        public void SetCaptainWorldId(int captainWorldId)
        {
            this.captainWorldId = captainWorldId;
        }
    }

    public SM_ALLIANCE_INFO(PlayerAlliance alliance)
        : this(alliance, 0, "", null)
    {
    }

    public SM_ALLIANCE_INFO(PlayerAlliance alliance, PlayerAlliance skipped)
        : this(alliance, 0, "", skipped)
    {
    }

    public SM_ALLIANCE_INFO(PlayerAlliance alliance, int messageId, string message)
        : this(alliance, messageId, message, null)
    {
    }

    public SM_ALLIANCE_INFO(PlayerAlliance alliance, int messageId, string message, PlayerAlliance skipped)
    {
        this.alliance = alliance;
        groupid = alliance.GetObjectId();
        leaderid = alliance.GetLeader().GetObjectId();
        lootRules = alliance.GetLootGroupRules();
        type = alliance.GetTeamType().GetType_();
        subType = alliance.GetTeamType().GetSubType();
        this.messageId = messageId;
        this.message = message;
        League league = alliance.GetLeague();
        if (league != null)
        {
            leagueId = league.GetTeamId();
            lootLeagueRules = league.GetLootGroupRules();
            foreach (Player captain in league.GetCaptains())
            {
                AllianceInfo info = new AllianceInfo();
                PlayerAlliance captainAlliance = captain.GetPlayerAlliance();
                if (captainAlliance != null)
                {
                    info.SetAlliancePosition(league.GetMember(captainAlliance.GetObjectId()).GetLeaguePosition());
                    info.SetAllianceObjectId(captainAlliance.GetObjectId());
                    info.SetMemberCount(captainAlliance.Size());
                    if (!captainAlliance.Equals(skipped))
                    {
                        info.SetCaptainName(captain.GetName());
                        info.SetCaptainWorldId(captain.GetWorldId());
                    }
                }
                leagueData.Add(info);
            }
        }
    }

    protected override void WriteImpl(AionConnection con)
    {
        Player player = con.GetActivePlayer();
        WriteH(alliance.GroupSize());
        WriteD(groupid);
        WriteD(leaderid);
        WriteD(player == null || player.GetPosition() == null ? 0 : player.GetWorldId());// mapId
        ICollection<int> ids = alliance.GetViceCaptainIds();
        foreach (int id in ids)
        {
            WriteD(id);
        }
        for (int i = 0; i < 4 - ids.Count; i++)
        {
            WriteD(0);
        }
        WriteD(lootRules.GetLootRule().GetId());
        WriteD(lootRules.GetMisc());
        WriteD(lootRules.GetCommonItemAbove());
        WriteD(lootRules.GetSuperiorItemAbove());
        WriteD(lootRules.GetHeroicItemAbove());
        WriteD(lootRules.GetFabledItemAbove());
        WriteD(lootRules.GetEternalItemAbove());
        WriteD(lootRules.GetMythicItemAbove());
        WriteD(0x02);
        WriteC(0x00);
        WriteD(type);
        WriteD(subType); // 3.5
        WriteD(leagueId);
        for (int a = 0; a < 4; a++)
        {
            WriteD(a); // group num
            WriteD(1000 + a); // group id
        }
        WriteD(messageId); // System message ID
        WriteS(messageId != 0 ? message : ""); // System message
        if (leagueData.Count != 0)
        {
            WriteH(leagueData.Count);
            WriteD(lootLeagueRules.GetLootRule().GetId());
            WriteD(lootLeagueRules.GetMisc());
            WriteD(lootLeagueRules.GetCommonItemAbove());
            WriteD(lootLeagueRules.GetSuperiorItemAbove());
            WriteD(lootLeagueRules.GetHeroicItemAbove());
            WriteD(lootLeagueRules.GetFabledItemAbove());
            WriteD(lootLeagueRules.GetEternalItemAbove());
            WriteD(lootLeagueRules.GetMythicItemAbove());
            WriteD(0x02);
            foreach (AllianceInfo info in leagueData)
            {
                WriteD(info.GetAlliancePosition());
                WriteD(info.GetAllianceObjectId());
                WriteD(info.GetMemberCount());
                WriteS(info.GetCaptainName());
                WriteD(info.GetCaptainWorldId());
            }
        }
    }
}
