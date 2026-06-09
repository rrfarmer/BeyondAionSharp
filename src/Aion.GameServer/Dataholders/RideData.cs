using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Ride;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/RideData. @XmlRootElement(rides); afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("rides")]
public class RideData
{
    [XmlElement("ride_info")] private List<RideInfo> rides;

    [XmlIgnore] private readonly Dictionary<int, RideInfo> rideInfos = new();

    public void AfterUnmarshal(object parent)
    {
        foreach (RideInfo info in rides)
        {
            rideInfos[info.GetNpcId()] = info;
        }
        rides = null;
    }

    public RideInfo GetRideInfo(int npcId)
    {
        return rideInfos.TryGetValue(npcId, out var v) ? v : null;
    }

    public int Size()
    {
        return rideInfos.Count;
    }
}
