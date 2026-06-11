using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model;

namespace Aion.GameServer.Model.Templates.Items;

/// <summary>Java parity: model/templates/item/ItemUseLimits.</summary>
[XmlType("UseLimits")]
public class ItemUseLimits
{
    [XmlAttribute("usedelay")] private int useDelay;
    [XmlAttribute("usedelayid")] private int useDelayId;
    [XmlIgnore] private List<int> ownershipWorldIds;
    [XmlAttribute("usearea")] private string usearea;
    [XmlIgnore] private Gender? genderPermitted;
    [XmlIgnore] private bool? rideUsable;
    [XmlAttribute("rank_min")] private int minRank = Aion.GameServer.Utils.Stats.AbyssRankEnum.GRADE9_SOLDIER.GetId();
    [XmlAttribute("rank_max")] private int maxRank = Aion.GameServer.Utils.Stats.AbyssRankEnum.SUPREME_COMMANDER.GetId();
    [XmlAttribute("purchable_rank_min")] private int minRankPuchable;
    [XmlAttribute("recommend_rank")] private int recommendRank;
    [XmlAttribute("guild_level")] private int guildLevel;
    [XmlAttribute("pack_count")] private int packCount = -1;

    // Java parity: @XmlAttribute List<Integer> — space-separated world ids.
    [XmlAttribute("ownership_worlds")]
    public string OwnershipWorldsXml
    {
        get => ownershipWorldIds == null ? null : string.Join(" ", ownershipWorldIds);
        set
        {
            if (value == null) { ownershipWorldIds = null; return; }
            string[] parts = value.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            ownershipWorldIds = new List<int>(parts.Length);
            foreach (string p in parts)
                ownershipWorldIds.Add(int.Parse(p));
        }
    }

    [XmlAttribute("gender")]
    public string GenderXml
    {
        get => genderPermitted?.ToString();
        set => genderPermitted = value == null ? (Gender?)null : (Gender)Enum.Parse(typeof(Gender), value);
    }

    [XmlAttribute("ride_usable")]
    public string RideUsableXml
    {
        get => rideUsable?.ToString().ToLowerInvariant();
        set => rideUsable = value == null ? (bool?)null : bool.Parse(value);
    }

    public int GetDelayId()
    {
        return useDelayId;
    }

    public int GetDelayTime()
    {
        return useDelay;
    }

    public Aion.GameServer.World.Zone.ZoneName? GetUseArea()
    {
        if (usearea == null)
            return null;
        try
        {
            return Aion.GameServer.World.Zone.ZoneName.CreateOrGet(usearea);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public List<int> GetOwnershipWorldIds()
    {
        if (ownershipWorldIds == null)
            return new List<int>();
        return ownershipWorldIds;
    }

    public Gender? GetGenderPermitted()
    {
        return genderPermitted;
    }

    public bool IsRideUsable()
    {
        if (rideUsable == null)
            return false;
        return rideUsable.Value;
    }

    public int GetMinRank()
    {
        return minRank;
    }

    public int GetMaxRank()
    {
        return maxRank;
    }

    public int GetMinRankPurchable()
    {
        return minRankPuchable;
    }

    public int GetRecommendRank()
    {
        return recommendRank;
    }

    public bool VerifyRank(int rank)
    {
        return minRank <= rank && maxRank >= rank;
    }

    public int GetGuildLevelPermitted()
    {
        return guildLevel;
    }

    public int GetPackCount()
    {
        return packCount;
    }
}
