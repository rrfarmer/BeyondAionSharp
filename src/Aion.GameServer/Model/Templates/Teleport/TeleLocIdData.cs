using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Teleport;

/// <summary>Java parity: model/templates/teleport/TeleLocIdData (ATracer).</summary>
[XmlRoot("locations")]
public class TeleLocIdData
{
    [XmlElement("telelocation")] private List<TeleportLocation> locids;

    /// <returns>Teleport locations</returns>
    public List<TeleportLocation> GetTelelocations()
    {
        return locids;
    }

    public TeleportLocation GetTeleportLocation(int value)
    {
        foreach (TeleportLocation t in locids)
        {
            if (t != null && t.GetLocId() == value)
            {
                return t;
            }
        }
        return null;
    }
}
