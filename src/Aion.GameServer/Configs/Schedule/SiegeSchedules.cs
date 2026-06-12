using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using Aion.GameServer.Utils.Xml;

namespace Aion.GameServer.Configs.Schedule;

/// <summary>Java parity: configs/schedule/SiegeSchedules.</summary>
[XmlRoot("siege_schedule")]
public class SiegeSchedules
{
    [XmlElement("fortress")] private List<Fortress> fortressesList;
    [XmlElement("agent_fight")] private List<AgentFight> agentFights;

    public List<Fortress> GetFortresses()
    {
        return fortressesList;
    }

    public List<AgentFight> GetAgentFights()
    {
        return agentFights;
    }

    // Java parity: private static abstract; C# CS0060 forbids a private base of public derived → widened to public.
    public abstract class SiegeSchedule
    {
        [XmlAttribute("id")] private int id;
        [XmlElement("siegeTime")] private List<string> siegeTimes;

        public int GetId()
        {
            return id;
        }

        public List<string> GetSiegeTimes()
        {
            return siegeTimes;
        }
    }

    [XmlRoot("fortress")]
    public class Fortress : SiegeSchedule
    {
    }

    [XmlRoot("agent_fight")]
    public class AgentFight : SiegeSchedule
    {
    }

    // Java parity: JAXBUtil.deserialize (deferred JAXB→XmlSerializer bridge, red).
    public static SiegeSchedules Load()
    {
        return (SiegeSchedules) JAXBUtil.Deserialize(new FileInfo("./config/schedule/siege_schedule.xml"), typeof(SiegeSchedules));
    }
}
