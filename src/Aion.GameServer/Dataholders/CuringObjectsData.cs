using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Curingzones;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/CuringObjectsData. @XmlRootElement(curing_objects); afterUnmarshal→AfterUnmarshal(object). NOTE: curingObject not nulled (matches Java).</summary>
[XmlRoot("curing_objects")]
public class CuringObjectsData
{
    [XmlElement("curing_object")] protected List<CuringTemplate> curingObject;

    [XmlIgnore] private readonly List<CuringTemplate> curingObjects = new();

    public void AfterUnmarshal(object parent)
    {
        foreach (CuringTemplate template in curingObject)
        {
            curingObjects.Add(template);
        }
    }

    public int Size()
    {
        return curingObjects.Count;
    }

    public List<CuringTemplate> GetCuringObject()
    {
        return curingObjects;
    }
}
