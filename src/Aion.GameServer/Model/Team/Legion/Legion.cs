using System;
using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.Team.Legion;

/// <summary>Java parity: model/team/legion/Legion extends AionObject.</summary>
public class Legion : AionObject
{
    private string legionName;
    private int legionLevel = 1;
    private long contributionPoints = 0;
    // Java parity: CopyOnWriteArrayList — concurrent read-heavy member id list (C# has no CoW list; foundational diff).
    private List<int> memberIds = new List<int>();
    private short deputyPermission = 0x1E0C;
    private short centurionPermission = 0x1C08;
    private short legionaryPermission = 0x1800;
    private short volunteerPermission = 0x800;
    private int disbandTime;
    private Announcement announcement;
    private LegionEmblem legionEmblem = new LegionEmblem();
    private readonly LegionWarehouse legionWarehouse;
    private readonly Dictionary<LegionHistoryAction.Type, List<LegionHistoryEntry>> legionHistoryByType = new Dictionary<LegionHistoryAction.Type, List<LegionHistoryEntry>>();
    private readonly Aion.GameServer.Utils.AtomicBoolean hasBonus = new Aion.GameServer.Utils.AtomicBoolean(false);
    private int occupiedLegionDominion = 0;
    private int currentLegionDominion = 0;
    private int lastLegionDominion = 0;

    public Legion(int legionId, string legionName) : base(legionId)
    {
        this.legionName = legionName;
        this.legionWarehouse = new LegionWarehouse(this);
        SetHistory(new Dictionary<LegionHistoryAction.Type, List<LegionHistoryEntry>>());
    }

    public int GetLegionId()
    {
        return GetObjectId();
    }

    public override string Name => legionName;

    public void SetName(string legionName)
    {
        this.legionName = legionName;
    }

    public void SetMemberIds(List<int> memberIds)
    {
        this.memberIds = memberIds;
    }

    public List<int> GetMemberIds()
    {
        return memberIds;
    }

    public List<LegionMember> GetMembers()
    {
        return StreamMembers().ToList();
    }

    private IEnumerable<LegionMember> StreamMembers()
    {
        return memberIds.Select(id => Aion.GameServer.Services.LegionService.GetInstance().GetLegionMember(id)).Where(m => m != null);
    }

    public List<Aion.GameServer.Model.GameObjects.Players.Player> GetOnlinePlayers()
    {
        return memberIds.Select(id => Aion.GameServer.World.World.GetInstance().GetPlayer(id)).Where(p => p != null).ToList();
    }

    public LegionMember GetBrigadeGeneral()
    {
        return StreamMembers().First(m => m.IsBrigadeGeneral());
    }

    public bool AddLegionMember(int playerObjId)
    {
        if (CanAddMember())
        {
            memberIds.Add(playerObjId);
            return true;
        }
        return false;
    }

    public void RemoveMember(int playerObjId)
    {
        memberIds.Remove(playerObjId);
    }

    public void SetLegionPermissions(short deputyPermission, short centurionPermission, short legionaryPermission, short volunteerPermission)
    {
        this.deputyPermission = deputyPermission;
        this.centurionPermission = centurionPermission;
        this.legionaryPermission = legionaryPermission;
        this.volunteerPermission = volunteerPermission;
    }

    public short GetDeputyPermission()
    {
        return deputyPermission;
    }

    public short GetCenturionPermission()
    {
        return centurionPermission;
    }

    public short GetLegionaryPermission()
    {
        return legionaryPermission;
    }

    public short GetVolunteerPermission()
    {
        return volunteerPermission;
    }

    public int GetLegionLevel()
    {
        return legionLevel;
    }

    public void SetLegionLevel(int legionLevel)
    {
        this.legionLevel = legionLevel;
        GetLegionWarehouse().UpdateLimit(GetWarehouseExpansions());
    }

    public void AddContributionPoints(long contributionPoints)
    {
        this.contributionPoints += contributionPoints;
    }

    public void SetContributionPoints(long contributionPoints)
    {
        this.contributionPoints = contributionPoints;
    }

    public long GetContributionPoints()
    {
        return contributionPoints;
    }

    /// <summary>Checks whether a legion has enough members to level up.</summary>
    public bool HasRequiredMembers()
    {
        int memberSize = GetMemberIds().Count;
        switch (GetLegionLevel())
        {
            case 1:
                return memberSize >= Aion.GameServer.Configs.Main.LegionConfig.LEGION_LEVEL2_REQUIRED_MEMBERS;
            case 2:
                return memberSize >= Aion.GameServer.Configs.Main.LegionConfig.LEGION_LEVEL3_REQUIRED_MEMBERS;
            case 3:
                return memberSize >= Aion.GameServer.Configs.Main.LegionConfig.LEGION_LEVEL4_REQUIRED_MEMBERS;
            case 4:
                return memberSize >= Aion.GameServer.Configs.Main.LegionConfig.LEGION_LEVEL5_REQUIRED_MEMBERS;
            case 5:
                return memberSize >= Aion.GameServer.Configs.Main.LegionConfig.LEGION_LEVEL6_REQUIRED_MEMBERS;
            case 6:
                return memberSize >= Aion.GameServer.Configs.Main.LegionConfig.LEGION_LEVEL7_REQUIRED_MEMBERS;
            case 7:
                return memberSize >= Aion.GameServer.Configs.Main.LegionConfig.LEGION_LEVEL8_REQUIRED_MEMBERS;
        }
        return false;
    }

    /// <summary>The kinah price required to level up.</summary>
    public int GetKinahPrice()
    {
        switch (GetLegionLevel())
        {
            case 1:
                return Aion.GameServer.Configs.Main.LegionConfig.LEGION_LEVEL2_REQUIRED_KINAH;
            case 2:
                return Aion.GameServer.Configs.Main.LegionConfig.LEGION_LEVEL3_REQUIRED_KINAH;
            case 3:
                return Aion.GameServer.Configs.Main.LegionConfig.LEGION_LEVEL4_REQUIRED_KINAH;
            case 4:
                return Aion.GameServer.Configs.Main.LegionConfig.LEGION_LEVEL5_REQUIRED_KINAH;
            case 5:
                return Aion.GameServer.Configs.Main.LegionConfig.LEGION_LEVEL6_REQUIRED_KINAH;
            case 6:
                return Aion.GameServer.Configs.Main.LegionConfig.LEGION_LEVEL7_REQUIRED_KINAH;
            case 7:
                return Aion.GameServer.Configs.Main.LegionConfig.LEGION_LEVEL8_REQUIRED_KINAH;
        }
        return 0;
    }

    /// <summary>The contribution points required to level up.</summary>
    public int GetContributionPrice()
    {
        switch (GetLegionLevel())
        {
            case 1:
                return Aion.GameServer.Configs.Main.LegionConfig.LEGION_LEVEL2_REQUIRED_CONTRIBUTION;
            case 2:
                return Aion.GameServer.Configs.Main.LegionConfig.LEGION_LEVEL3_REQUIRED_CONTRIBUTION;
            case 3:
                return Aion.GameServer.Configs.Main.LegionConfig.LEGION_LEVEL4_REQUIRED_CONTRIBUTION;
            case 4:
                return Aion.GameServer.Configs.Main.LegionConfig.LEGION_LEVEL5_REQUIRED_CONTRIBUTION;
            case 5:
                return Aion.GameServer.Configs.Main.LegionConfig.LEGION_LEVEL6_REQUIRED_CONTRIBUTION;
            case 6:
                return Aion.GameServer.Configs.Main.LegionConfig.LEGION_LEVEL7_REQUIRED_CONTRIBUTION;
            case 7:
                return Aion.GameServer.Configs.Main.LegionConfig.LEGION_LEVEL8_REQUIRED_CONTRIBUTION;
        }
        return 0;
    }

    /// <summary>True if a legion is able to add a member.</summary>
    private bool CanAddMember()
    {
        int memberSize = GetMemberIds().Count;
        switch (GetLegionLevel())
        {
            case 1:
                return memberSize < Aion.GameServer.Configs.Main.LegionConfig.LEGION_LEVEL1_MAX_MEMBERS;
            case 2:
                return memberSize < Aion.GameServer.Configs.Main.LegionConfig.LEGION_LEVEL2_MAX_MEMBERS;
            case 3:
                return memberSize < Aion.GameServer.Configs.Main.LegionConfig.LEGION_LEVEL3_MAX_MEMBERS;
            case 4:
                return memberSize < Aion.GameServer.Configs.Main.LegionConfig.LEGION_LEVEL4_MAX_MEMBERS;
            case 5:
                return memberSize < Aion.GameServer.Configs.Main.LegionConfig.LEGION_LEVEL5_MAX_MEMBERS;
            case 6:
                return memberSize < Aion.GameServer.Configs.Main.LegionConfig.LEGION_LEVEL6_MAX_MEMBERS;
            case 7:
                return memberSize < Aion.GameServer.Configs.Main.LegionConfig.LEGION_LEVEL7_MAX_MEMBERS;
            case 8:
                return memberSize < Aion.GameServer.Configs.Main.LegionConfig.LEGION_LEVEL8_MAX_MEMBERS;
        }
        return false;
    }

    public Announcement GetAnnouncement()
    {
        return announcement;
    }

    public void SetAnnouncement(Announcement announcement)
    {
        this.announcement = announcement;
    }

    /// <param name="disbandTime">the disbandTime to set</param>
    public void SetDisbandTime(int disbandTime)
    {
        this.disbandTime = disbandTime;
    }

    /// <summary>the disbandTime</summary>
    public int GetDisbandTime()
    {
        return disbandTime;
    }

    /// <summary>true if currently disbanding</summary>
    public bool IsDisbanding()
    {
        return disbandTime > 0;
    }

    /// <summary>Checks if object id is in the member list.</summary>
    public bool IsMember(int playerObjId)
    {
        return memberIds.Contains(playerObjId);
    }

    /// <param name="legionEmblem">the legionEmblem to set</param>
    public void SetLegionEmblem(LegionEmblem legionEmblem)
    {
        this.legionEmblem = legionEmblem;
    }

    /// <summary>the legionEmblem</summary>
    public LegionEmblem GetLegionEmblem()
    {
        return legionEmblem;
    }

    public LegionWarehouse GetLegionWarehouse()
    {
        return legionWarehouse;
    }

    public int GetWarehouseExpansions()
    {
        return GetLegionLevel() - 1;
    }

    public List<LegionHistoryEntry> GetHistory(LegionHistoryAction.Type type)
    {
        List<LegionHistoryEntry> history = legionHistoryByType[type];
        lock (history)
        {
            return new List<LegionHistoryEntry>(history);
        }
    }

    /// <summary>Adds the history entry at the top of the list and removes entries older than a year (except the first page).</summary>
    public List<LegionHistoryEntry> AddHistory(LegionHistoryEntry entry)
    {
        List<LegionHistoryEntry> removedEntries = new List<LegionHistoryEntry>();
        LegionHistoryAction.Type type = entry.Action.GetType_();
        List<LegionHistoryEntry> history = legionHistoryByType[type];
        lock (history)
        {
            history.Insert(0, entry);
            if (type == LegionHistoryAction.Type.REWARD || type == LegionHistoryAction.Type.WAREHOUSE)
            {
                long maxMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000 - (365L * 24 * 60 * 60);
                while (history[history.Count - 1].EpochSeconds < maxMillis)
                {
                    removedEntries.Add(history[history.Count - 1]);
                    history.RemoveAt(history.Count - 1);
                }
            }
        }
        return removedEntries;
    }

    public void SetHistory(Dictionary<LegionHistoryAction.Type, List<LegionHistoryEntry>> history)
    {
        foreach (LegionHistoryAction.Type type in Enum.GetValues<LegionHistoryAction.Type>())
        {
            history.TryGetValue(type, out List<LegionHistoryEntry> entries);
            legionHistoryByType[type] = entries == null ? new List<LegionHistoryEntry>(1) : entries;
        }
    }

    public void AddBonus()
    {
        List<Aion.GameServer.Model.GameObjects.Players.Player> members = GetOnlinePlayers();
        if (members.Count >= 10)
        {
            if (hasBonus.CompareAndSet(false, true))
            {
                foreach (Aion.GameServer.Model.GameObjects.Players.Player member in members)
                {
                    Aion.GameServer.Utils.PacketSendUtility.SendPacket(member, new Aion.GameServer.Network.Aion.ServerPackets.SmIconInfo(1, true));
                }
            }
        }
    }

    public void RemoveBonus()
    {
        List<Aion.GameServer.Model.GameObjects.Players.Player> members = GetOnlinePlayers();
        if (members.Count < 10)
        {
            if (hasBonus.CompareAndSet(true, false))
            {
                foreach (Aion.GameServer.Model.GameObjects.Players.Player member in members)
                {
                    Aion.GameServer.Utils.PacketSendUtility.SendPacket(member, new Aion.GameServer.Network.Aion.ServerPackets.SmIconInfo(1, false));
                }
            }
        }
    }

    public bool HasBonus()
    {
        return hasBonus.Get();
    }

    public int GetOccupiedLegionDominion()
    {
        return occupiedLegionDominion;
    }

    public int GetCurrentLegionDominion()
    {
        return currentLegionDominion;
    }

    public int GetLastLegionDominion()
    {
        return lastLegionDominion;
    }

    public void SetOccupiedLegionDominion(int occupiedLegionDominion)
    {
        this.occupiedLegionDominion = occupiedLegionDominion;
    }

    public void SetCurrentLegionDominion(int currentLegionDominion)
    {
        this.currentLegionDominion = currentLegionDominion;
    }

    public void SetLastLegionDominion(int lastLegionDominion)
    {
        this.lastLegionDominion = lastLegionDominion;
    }

    public override string ToString()
    {
        return "Legion [id=" + GetObjectId() + ", name=" + Name + "]";
    }

    public record Announcement(string Message, DateTime Time);
}
