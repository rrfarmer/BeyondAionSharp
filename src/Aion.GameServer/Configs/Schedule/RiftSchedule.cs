using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Rift;

namespace Aion.GameServer.Configs.Schedule;

/// <summary>Java parity: configs/schedule/RiftSchedule.</summary>
[XmlRoot("rift_schedule")]
public class RiftSchedule
{
    [XmlElement("rift")] private List<Rift> riftsList;

    public List<Rift> GetRiftsList()
    {
        return riftsList;
    }

    public void SetRiftsList(List<Rift> fortressList)
    {
        this.riftsList = fortressList;
    }

    [XmlRoot("rift")]
    public class Rift
    {
        [XmlAttribute("id")] private int id;
        [XmlElement("open")] private List<OpenRift> openRift;

        public int GetWorldId()
        {
            return id;
        }

        public List<OpenRift> GetRift()
        {
            return openRift;
        }
    }

    // Java parity: JAXBUtil.deserialize (deferred JAXB→XmlSerializer bridge, red).
    public static RiftSchedule Load()
    {
        return (RiftSchedule) JAXBUtil.Deserialize(new FileInfo("./config/schedule/rift_schedule.xml"), typeof(RiftSchedule));
    }
}
