using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Vortex;
using Aion.GameServer.Model.Vortex;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/VortexData (Source). @XmlRootElement(dimensional_vortex)→[XmlRoot]; @XmlElement→[XmlElement]; @XmlTransient LinkedHashMap→[XmlIgnore] Dictionary built in AfterUnmarshal; afterUnmarshal(Unmarshaller,parent)→AfterUnmarshal(object). Converges VortexLocation. VortexTemplate red-tolerated.</summary>
[XmlRoot("dimensional_vortex")]
public class VortexData
{
    [XmlElement("vortex_location")]
    public List<VortexTemplate> vortexTemplates;

    [XmlIgnore]
    private Dictionary<int, VortexLocation> vortex = new();

    public void AfterUnmarshal(object parent)
    {
        foreach (VortexTemplate template in vortexTemplates)
        {
            vortex[template.GetId()] = new VortexLocation(template);
        }
    }

    public int Size()
    {
        return vortex.Count;
    }

    public VortexLocation GetVortexLocation(int invasionWorldId)
    {
        foreach (VortexLocation loc in vortex.Values)
        {
            if (loc.GetInvasionWorldId() == invasionWorldId)
            {
                return loc;
            }
        }
        return null;
    }

    public Dictionary<int, VortexLocation> GetVortexLocations()
    {
        return vortex;
    }
}
