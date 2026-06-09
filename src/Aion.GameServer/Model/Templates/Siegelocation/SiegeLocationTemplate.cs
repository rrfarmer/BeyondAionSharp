using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Aion.GameServer.Model.Siege;
using Aion.GameServer.Model.Templates;

namespace Aion.GameServer.Model.Templates.Siegelocation;

/// <summary>Java parity: model/templates/siegelocation/SiegeLocationTemplate (Sarynth, antness, Source, Wakizashi).</summary>
[XmlType("siegelocation")]
public class SiegeLocationTemplate : IL10n
{
    [XmlAttribute("id")] protected int id;
    [XmlAttribute("type")] protected SiegeType type;
    [XmlAttribute("world")] protected int world;
    [XmlElement("artifact_activation")] protected ArtifactActivation artifactActivation;
    [XmlElement("door_repair_data")] protected DoorRepairData doorRepairData;
    [XmlElement("siege_reward")] protected List<SiegeReward> siegeRewards;
    [XmlElement("legion_reward")] protected List<SiegeLegionReward> siegeLegionRewards;
    [XmlElement("merc_zone")] protected List<SiegeMercenaryZone> siegeMercenaryZones;
    [XmlElement("assault_data")] protected AssaultData assaultData;
    [XmlElement("siege_related_bases")] protected SiegeRelatedBases siegeRelatedBases;

    [XmlAttribute("name_id")] protected int nameId = 0;
    [XmlAttribute("siege_duration")] protected int siegeDuration;
    [XmlAttribute("influence")] protected int influenceValue;
    [XmlAttribute("occupy_count")] protected int maxOccupyCount;
    [XmlAttribute("legion_gp")] protected int legionGp;

    // Java parity: @XmlAttribute(name="kinah_rewards") List<Integer> — space-separated.
    private List<int> kinahRewards;

    [XmlAttribute("kinah_rewards")]
    public string KinahRewardsRaw
    {
        get => kinahRewards == null ? null : string.Join(" ", kinahRewards);
        set => kinahRewards = value == null ? null : value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
    }

    // Java parity: @XmlList @XmlAttribute(name="fortress_dependency") List<Integer> — space-separated.
    private List<int> fortressDependency;

    [XmlAttribute("fortress_dependency")]
    public string FortressDependencyRaw
    {
        get => fortressDependency == null ? null : string.Join(" ", fortressDependency);
        set => fortressDependency = value == null ? null : value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
    }

    public int GetId()
    {
        return id;
    }

    public SiegeType GetType_()
    {
        return type;
    }

    public int GetWorldId()
    {
        return world;
    }

    public ArtifactActivation GetActivation()
    {
        return artifactActivation;
    }

    public DoorRepairData GetDoorRepairData()
    {
        return doorRepairData;
    }

    public List<SiegeReward> GetSiegeRewards()
    {
        return siegeRewards;
    }

    public List<SiegeLegionReward> GetSiegeLegionRewards()
    {
        return siegeLegionRewards;
    }

    public List<SiegeMercenaryZone> GetSiegeMercenaryZones()
    {
        return siegeMercenaryZones;
    }

    public AssaultData GetAssaultData()
    {
        return assaultData;
    }

    public SiegeRelatedBases GetSiegeRelatedBases()
    {
        return siegeRelatedBases;
    }

    public int GetL10nId()
    {
        return nameId;
    }

    public int GetRepeatCount()
    {
        return artifactActivation.GetRepeatCount();
    }

    public int GetRepeatInterval()
    {
        return artifactActivation.GetRepeatInterval();
    }

    public List<int> GetFortressDependency()
    {
        if (fortressDependency == null)
            return new List<int>();
        return fortressDependency;
    }

    /// <returns>the Duration in Seconds</returns>
    public int GetSiegeDuration()
    {
        return siegeDuration;
    }

    public int GetInfluenceValue()
    {
        return influenceValue;
    }

    public int GetMaxOccupyCount()
    {
        return maxOccupyCount;
    }

    public int GetLegionGp()
    {
        return legionGp;
    }

    public int GetKinahRewardByRewardLevel(int rewardLevel)
    {
        if (kinahRewards == null || rewardLevel > kinahRewards.Count - 1)
            return 0;
        return kinahRewards[rewardLevel];
    }
}
