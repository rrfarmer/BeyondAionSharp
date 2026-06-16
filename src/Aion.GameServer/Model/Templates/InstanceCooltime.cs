using System.Xml.Serialization;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Instance;

namespace Aion.GameServer.Model.Templates;

/// <summary>Java parity: model/templates/InstanceCooltime (VladimirZ).</summary>
[XmlType("InstanceCooltime")]
public class InstanceCooltime
{
    [XmlElement("type")] public InstanceCoolTimeType coolTimeType;
    [XmlElement("typevalue")] public string typevalue;
    [XmlElement("ent_cool_time")] public int entCoolTime;
    [XmlElement("maxcount")] public int maxCount;
    [XmlElement("max_member_light")] public int maxMemberLight;
    [XmlElement("max_member_dark")] public int maxMemberDark;
    [XmlElement("enter_min_level_light")] public int enterMinLevelLight;
    [XmlElement("enter_max_level_light")] public int enterMaxLevelLight;
    [XmlElement("enter_min_level_dark")] public int enterMinLevelDark;
    [XmlElement("enter_max_level_dark")] public int enterMaxLevelDark;
    [XmlElement("can_enter_mentor")] public bool can_enter_mentor;
    [XmlAttribute("id")] public int id;
    [XmlAttribute("worldId")] public int worldId;
    [XmlAttribute("race")] public Race race;
    [XmlAttribute("sync_id")] public int syncId;

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
