using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Instance;
using Aion.GameServer.Model.Templates;
using Aion.GameServer.Services.Instance;
using Aion.GameServer.Utils.Time;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/InstanceCooltimeData (VladimirZ). @XmlRootElement(instance_cooltimes); ZonedDateTime→DateTimeOffset; afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("instance_cooltimes")]
public class InstanceCooltimeData
{
    private static readonly ILogger log = NullLogger.Instance;

    [XmlElement("instance_cooltime")] public List<InstanceCooltime> instanceCooltime;

    private Dictionary<int, InstanceCooltime> instanceCooltimes = new();
    private Dictionary<int, int> syncIdToMapId = new();

    public void AfterUnmarshal(object parent)
    {
        foreach (InstanceCooltime tmp in instanceCooltime)
        {
            instanceCooltimes[tmp.GetWorldId()] = tmp;
            syncIdToMapId[tmp.GetSyncId()] = tmp.GetWorldId();
        }
        instanceCooltime.Clear();
    }

    public Dictionary<int, InstanceCooltime> GetInstanceCooltimes()
    {
        return new Dictionary<int, InstanceCooltime>(instanceCooltimes);
    }

    public InstanceCooltime GetInstanceCooltimeByWorldId(int worldId)
    {
        return instanceCooltimes.TryGetValue(worldId, out var v) ? v : null;
    }

    public int GetInstanceMaxCountByWorldId(int worldId)
    {
        return instanceCooltimes[worldId].GetMaxCount();
    }

    public int GetMaxMemberCount(int worldId, Race race)
    {
        InstanceCooltime template = GetInstanceCooltimeByWorldId(worldId);
        return template == null ? 0 : race == Race.ELYOS ? template.GetMaxMemberLight() : template.GetMaxMemberDark();
    }

    public int GetWorldId(int syncId)
    {
        return syncIdToMapId.TryGetValue(syncId, out var v) ? v : 0;
    }

    public long CalculateInstanceEntranceCooltime(Player player, int worldId)
    {
        int instanceCooldownRate = InstanceService.GetInstanceRate(player, worldId);
        long instanceCoolTime = 0;
        InstanceCooltime clt = GetInstanceCooltimeByWorldId(worldId);
        if (clt == null || clt.GetMaxCount() == 0)
            return 0;
        switch (clt.GetCoolTimeType())
        {
            case InstanceCoolTimeType.DAILY:
            case InstanceCoolTimeType.WEEKLY:
            {
                int hour = clt.GetEntCoolTime() / 100;
                int minute = clt.GetEntCoolTime() % 100;
                DateTimeOffset now = ServerTime.Now();
                DateTimeOffset repeatDate = new DateTimeOffset(now.Year, now.Month, now.Day, hour, minute, 0, now.Offset);
                if (now > repeatDate)
                    repeatDate = repeatDate.AddDays(1);
                if (clt.GetCoolTimeType() == InstanceCoolTimeType.WEEKLY)
                    repeatDate = repeatDate.AddDays(CalculateDaysUntilReset(clt, repeatDate.DayOfWeek));
                instanceCoolTime = repeatDate.ToUnixTimeSeconds() * 1000;
                break;
            }
            case InstanceCoolTimeType.RELATIVE:
            {
                int minutes = clt.GetEntCoolTime();
                if (minutes == 0) // unlimited entrance, no need to store
                    return 0;
                instanceCoolTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + (minutes * 60 * 1000);
                break;
            }
            default:
                log.LogWarning("Unhandled InstanceCoolTimeType: " + clt.GetCoolTimeType());
                break;
        }
        if (instanceCooldownRate != 1)
            instanceCoolTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + ((instanceCoolTime - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) / instanceCooldownRate);
        return instanceCoolTime;
    }

    private int CalculateDaysUntilReset(InstanceCooltime clt, DayOfWeek day)
    {
        // Java DayOfWeek.getValue(): Mon=1..Sun=7
        int dayValue = day == DayOfWeek.Sunday ? 7 : (int)day;
        List<int> resetDaysSorted = clt.GetTypeValue().Split(',').Select(dayStr => GetDay(dayStr)).OrderBy(x => x).ToList();
        foreach (int resetDay in resetDaysSorted)
        {
            if (resetDay >= dayValue)
                return resetDay - dayValue;
        }
        return (7 - dayValue) + resetDaysSorted[0];
    }

    private int GetDay(string day)
    {
        if (day.Equals("Mon"))
        {
            return 1;
        }
        else if (day.Equals("Tue"))
        {
            return 2;
        }
        else if (day.Equals("Wed"))
        {
            return 3;
        }
        else if (day.Equals("Thu"))
        {
            return 4;
        }
        else if (day.Equals("Fri"))
        {
            return 5;
        }
        else if (day.Equals("Sat"))
        {
            return 6;
        }
        else if (day.Equals("Sun"))
        {
            return 7;
        }
        throw new ArgumentException("Invalid Day: " + day);
    }

    public int Size()
    {
        return instanceCooltimes.Count;
    }
}
