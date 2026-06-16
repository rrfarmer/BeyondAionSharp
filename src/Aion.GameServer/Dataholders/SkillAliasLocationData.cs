using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/SkillAliasLocationData. @XmlRootElement(alias_locations); afterUnmarshal→AfterUnmarshal(object parent).</summary>
[XmlRoot("alias_locations")]
public class SkillAliasLocationData
{
    [XmlElement("alias_location")] public List<SkillAliasLocation> skillAliasLocationData;

    [XmlIgnore] private Dictionary<string, SkillAliasLocation> skillAliasLocations = new();

    public void AfterUnmarshal(object parent)
    {
        foreach (SkillAliasLocation loc in skillAliasLocationData)
        {
            skillAliasLocations[loc.GetAliasName()] = loc;
        }
        skillAliasLocationData = null;
    }

    public SkillAliasLocation GetSkillAliasLocation(string alias)
    {
        return skillAliasLocations.TryGetValue(alias, out var v) ? v : null;
    }

    public int Size()
    {
        return skillAliasLocations.Count;
    }
}
