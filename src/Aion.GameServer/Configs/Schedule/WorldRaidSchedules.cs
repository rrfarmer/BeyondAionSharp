using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using Aion.GameServer.Utils.Xml;

namespace Aion.GameServer.Configs.Schedule;

/// <summary>Java parity: configs/schedule/WorldRaidSchedules (Whoop, Sykra).</summary>
[XmlRoot("world_raid_schedules")]
public class WorldRaidSchedules
{
    [XmlElement("world_raid_schedule")] private List<WorldRaidSchedule> worldRaidSchedules;

    public List<WorldRaidSchedule> GetWorldRaidSchedules()
    {
        return worldRaidSchedules;
    }

    [XmlRoot("world_raid_schedule")]
    public class WorldRaidSchedule
    {
        [XmlAttribute("id")] private string id = "";

        [XmlAttribute("min_count")] private int minCount = 0;

        [XmlAttribute("max_count")] private int maxCount = 0;

        [XmlAttribute("is_special_raid")] private bool isSpecialRaid;

        // Java parity: @XmlList @XmlAttribute(name="locations") List<Integer> — space-separated.
        private List<int> locations;

        [XmlAttribute("locations")]
        public string LocationsRaw
        {
            get => locations == null ? null : string.Join(" ", locations);
            set => locations = value == null
                ? null
                : value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
        }

        [XmlElement("start_time")] private List<string> raidTimes;

        public string GetId()
        {
            return id;
        }

        public int GetMinCount()
        {
            return minCount;
        }

        public int GetMaxCount()
        {
            return maxCount;
        }

        public bool IsSpecialRaid()
        {
            return isSpecialRaid;
        }

        public List<int> GetLocations()
        {
            return locations;
        }

        public List<string> GetRaidTimes()
        {
            return raidTimes;
        }
    }

    public static WorldRaidSchedules Load()
    {
        return JAXBUtil.Deserialize<WorldRaidSchedules>(new FileInfo("./config/schedule/world_raid_schedule.xml"));
    }
}
