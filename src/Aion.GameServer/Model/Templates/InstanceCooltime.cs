using System.Xml.Serialization;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Instance;

namespace Aion.GameServer.Model.Templates;

/// <summary>Java parity: model/templates/InstanceCooltime (VladimirZ).</summary>
[XmlType("InstanceCooltime")]
public class InstanceCooltime
{
    [XmlElement("type")] protected InstanceCoolTimeType coolTimeType;
    [XmlElement("typevalue")] protected string typevalue;
    [XmlElement("ent_cool_time")] protected int entCoolTime;
    [XmlElement("maxcount")] protected int maxCount;
    [XmlElement("max_member_light")] protected int maxMemberLight;
    [XmlElement("max_member_dark")] protected int maxMemberDark;
    [XmlElement("enter_min_level_light")] protected int enterMinLevelLight;
    [XmlElement("enter_max_level_light")] protected int enterMaxLevelLight;
    [XmlElement("enter_min_level_dark")] protected int enterMinLevelDark;
    [XmlElement("enter_max_level_dark")] protected int enterMaxLevelDark;
    [XmlElement("can_enter_mentor")] protected bool can_enter_mentor;
    [XmlAttribute("id")] protected int id;
    [XmlAttribute("worldId")] protected int worldId;
    [XmlAttribute("race")] protected Race race;
    [XmlAttribute("sync_id")] private int syncId;

    public InstanceCoolTimeType GetCoolTimeType()
    {
        return coolTimeType;
    }

    public string GetTypeValue()
    {
        return typevalue;
    }

    public int GetEntCoolTime()
    {
        return entCoolTime;
    }

    public int GetMaxCount()
    {
        return maxCount;
    }

    public int GetMaxMemberLight()
    {
        return maxMemberLight;
    }

    public int GetMaxMemberDark()
    {
        return maxMemberDark;
    }

    public int GetEnterMinLevelLight()
    {
        return enterMinLevelLight;
    }

    public int GetEnterMaxLevelLight()
    {
        return enterMaxLevelLight;
    }

    public int GetEnterMinLevelDark()
    {
        return enterMinLevelDark;
    }

    public int GetEnterMaxLevelDark()
    {
        return enterMaxLevelDark;
    }

    public bool GetCanEnterMentor()
    {
        return can_enter_mentor;
    }

    public int GetId()
    {
        return id;
    }

    public int GetWorldId()
    {
        return worldId;
    }

    public Race GetRace()
    {
        return race;
    }

    public int GetSyncId()
    {
        return syncId;
    }
}
