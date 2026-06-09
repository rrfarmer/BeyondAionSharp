using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Walker;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/WalkerVersionsData. @XmlRootElement(walker_versions); afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("walker_versions")]
public class WalkerVersionsData
{
    [XmlElement("walk_parent")] private List<RouteParent> routeGroups;

    [XmlIgnore] private Dictionary<string, string> walkParents = new();

    public void AfterUnmarshal(object parent)
    {
        foreach (RouteParent group in routeGroups)
        {
            foreach (RouteVersion version in group.GetRouteVersion())
                walkParents[version.GetId()] = group.GetId();
        }
        routeGroups.Clear();
        routeGroups = null;
    }

    public bool IsRouteVersioned(string routeId)
    {
        if (routeId == null)
            return false;
        return walkParents.ContainsKey(routeId);
    }

    public string GetRouteVersionId(string routeId)
    {
        if (routeId == null)
            return null;
        return walkParents.TryGetValue(routeId, out var v) ? v : null;
    }

    public int Size()
    {
        return walkParents.Count;
    }
}
