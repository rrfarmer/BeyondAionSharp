using System;
using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dao;
using Aion.GameServer.Model.Team.Legion;
using Aion.GameServer.Model.Templates;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Model.LegionDominion;

/// <summary>Java parity: model/legionDominion/LegionDominionLocation (Yeats, Sykra). TreeMap→SortedDictionary; Collections.emptyList→new List; stream filter/sorted(comparingInt.reversed.thenComparingLong)/collect→Where/OrderByDescending(Points).ThenBy(Date)/ToList; groupingBy→GroupBy.ToDictionary; java.sql.Timestamp→DateTimeOffset?; synchronized reset→lock. LegionDominionDAO/LegionService/templates/SM_LEGION_DOMINION_RANK red-tolerated.</summary>
public class LegionDominionLocation
{
    private readonly LegionDominionLocationTemplate template;
    private readonly string zoneName;
    private int legionId;
    private DateTimeOffset? occupiedDate;
    private SortedDictionary<int, LegionDominionParticipantInfo> participantInfo = new();

    public LegionDominionLocation(LegionDominionLocationTemplate template)
    {
        this.template = template;
        this.zoneName = template.GetZone() + "_" + template.GetWorldId();
    }

    public int GetLocationId()
    {
        return template.GetId();
    }

    public int GetWorldId()
    {
        return template.GetWorldId();
    }

    public Race GetRace()
    {
        return template.GetRace();
    }

    public string GetL10n()
    {
        return template.GetL10n();
    }

    public string GetZoneNameAsString()
    {
        return zoneName;
    }

    public int GetLegionId()
    {
        return legionId;
    }

    public LegionDominionInvasionRift GetInvasionRift()
    {
        return template.GetInvasionRift();
    }

    public void SetLegionId(int legionId)
    {
        this.legionId = legionId;
    }

    public DateTimeOffset? GetOccupiedDate()
    {
        return occupiedDate;
    }

    public void SetOccupiedDate(DateTimeOffset? timestamp)
    {
        occupiedDate = timestamp;
    }

    public IDictionary<int, LegionDominionParticipantInfo> GetParticipantInfo()
    {
        return participantInfo;
    }

    public void SetParticipantInfo(IDictionary<int, LegionDominionParticipantInfo> info)
    {
        participantInfo = new SortedDictionary<int, LegionDominionParticipantInfo>(info);
    }

    public List<LegionDominionParticipantInfo> GetLegionRanking(bool removeNonEligibleLegions)
    {
        if (participantInfo.Count == 0)
            return new List<LegionDominionParticipantInfo>();
        IEnumerable<LegionDominionParticipantInfo> participantStream = participantInfo.Values;
        if (removeNonEligibleLegions)
            participantStream = participantStream.Where(info => info.GetPoints() > LegionConfig.STONESPEAR_REACH_MIN_POINTS_FOR_TERRITORY);
        return participantStream
            .OrderByDescending(info => info.GetPoints()).ThenBy(info => info.GetDate())
            .ToList();
    }

    public Dictionary<int, List<LegionDominionReward>> GetRewards()
    {
        return template.GetRewards().GroupBy(reward => reward.GetRank()).ToDictionary(g => g.Key, g => g.ToList());
    }

    public bool Join(int legionId)
    {
        if (participantInfo.ContainsKey(legionId))
        {
            return false;
        }
        LegionDominionParticipantInfo info = new();
        info.SetLegionId(legionId);
        participantInfo[legionId] = info;
        Store(info, true);
        return true;
    }

    private void Store(LegionDominionParticipantInfo info, bool isNew)
    {
        if (isNew)
        {
            LegionDominionDAO.StoreNewInfo(GetLocationId(), info);
        }
        else
        {
            LegionDominionDAO.UpdateInfo(info);
        }
    }

    public LegionDominionParticipantInfo GetParticipantInfo(int legionId)
    {
        return participantInfo.GetValueOrDefault(legionId);
    }

    public void UpdateRanking()
    {
        foreach (LegionDominionParticipantInfo info in participantInfo.Values)
        {
            Aion.GameServer.Model.Team.Legion.Legion legion = LegionService.GetInstance().GetLegion(info.GetLegionId());
            if (legion != null)
                PacketSendUtility.BroadcastToLegion(legion, new SM_LEGION_DOMINION_RANK(this, legion));
        }
    }

    public void Reset()
    {
        lock (this)
        {
            participantInfo = new SortedDictionary<int, LegionDominionParticipantInfo>();
        }
    }
}
