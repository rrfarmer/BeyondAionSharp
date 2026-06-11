using System;
using System.Xml.Serialization;
using Aion.GameServer.Dataholders.LoadingUtils.Adapters;
using Aion.GameServer.Model;

namespace Aion.GameServer.Model.Templates.Event;

/// <summary>Java parity: model/templates/event/AtreianPassport (Alcapwnd, SVDNESS).</summary>
[XmlRoot("login_event")]
public class AtreianPassport
{
    private static readonly LocalDateTimeAdapter DateAdapter = new LocalDateTimeAdapter();

    [XmlAttribute("id")] private int id;
    [XmlAttribute("active")] private bool active;

    // Java parity: @XmlJavaTypeAdapter(LocalDateTimeAdapter) on LocalDateTime — string attribute via adapter.
    [XmlIgnore] private DateTime pStart;
    [XmlIgnore] private DateTime pEnd;

    [XmlAttribute("period_start")]
    public string PStartRaw { get => DateAdapter.Marshal(pStart); set => pStart = DateAdapter.Unmarshal(value); }

    [XmlAttribute("period_end")]
    public string PEndRaw { get => DateAdapter.Marshal(pEnd); set => pEnd = DateAdapter.Unmarshal(value); }

    [XmlAttribute("attend_type")] private AttendType attendType;
    [XmlAttribute("attend_num")] private int attendNum;
    [XmlAttribute("reward_item")] private int rewardItemId;
    [XmlAttribute("reward_item_num")] private int rewardItemCount;
    [XmlAttribute("reward_item_expire_time")] private int rewardExpireMinutes;
    [XmlAttribute("reward_permit_level")] private int rewardPermitLevel;

    public int GetId()
    {
        return id;
    }

    public bool IsActive()
    {
        return active;
    }

    public DateTime GetPeriodStart()
    {
        return pStart;
    }

    public DateTime GetPeriodEnd()
    {
        return pEnd;
    }

    public AttendType GetAttendType()
    {
        return attendType;
    }

    public int GetAttendNum()
    {
        return attendNum;
    }

    public int GetRewardItemId()
    {
        return rewardItemId;
    }

    public int GetRewardItemCount()
    {
        return rewardItemCount;
    }

    public int GetRewardExpireMinutes()
    {
        return rewardExpireMinutes;
    }

    public int GetRewardPermitLevel()
    {
        return rewardPermitLevel;
    }
}
