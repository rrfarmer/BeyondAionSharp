using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Siege;
using Aion.GameServer.Model.Templates.Siegelocation;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/SiegeLocationData (Sarynth, antness). @XmlRootElement(siege_locations); LinkedHashMap→Dictionary; afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("siege_locations")]
public class SiegeLocationData
{
    [XmlElement("siege_location")] public List<SiegeLocationTemplate> siegeLocationTemplates;

    [XmlIgnore] private readonly Dictionary<int, ArtifactLocation> artifactLocations = new();
    [XmlIgnore] private readonly Dictionary<int, FortressLocation> fortressLocations = new();
    [XmlIgnore] private readonly Dictionary<int, OutpostLocation> outpostLocations = new();
    [XmlIgnore] private readonly Dictionary<int, SiegeLocation> siegeLocations = new();
    [XmlIgnore] private AgentLocation agentLoc;

    public void AfterUnmarshal(object parent)
    {
        artifactLocations.Clear();
        fortressLocations.Clear();
        outpostLocations.Clear();
        siegeLocations.Clear();
        foreach (SiegeLocationTemplate template in siegeLocationTemplates)
        {
            // JAXB invokes nested afterUnmarshal callbacks before the holder callback. XmlSerializer does not,
            // so finish the two nested indexes before wrapping the template in a live siege location.
            template.GetDoorRepairData()?.AfterUnmarshal(template);
            template.GetAssaultData()?.AfterUnmarshal(template);

            switch (template.GetType_())
            {
                case SiegeType.FORTRESS:
                {
                    FortressLocation fortress = new FortressLocation(template);
                    fortressLocations[template.GetId()] = fortress;
                    siegeLocations[template.GetId()] = fortress;
                    artifactLocations[template.GetId()] = new ArtifactLocation(template);
                    break;
                }
                case SiegeType.ARTIFACT:
                {
                    ArtifactLocation artifact = new ArtifactLocation(template);
                    artifactLocations[template.GetId()] = artifact;
                    siegeLocations[template.GetId()] = artifact;
                    break;
                }
                case SiegeType.OUTPOST:
                {
                    OutpostLocation outpost = new OutpostLocation(template);
                    if (outpost.GetLocationId() == 2111)
                        outpost.SetRace(SiegeRace.ELYOS);
                    else if (outpost.GetLocationId() == 3111)
                        outpost.SetRace(SiegeRace.ASMODIANS);
                    outpostLocations[template.GetId()] = outpost;
                    siegeLocations[template.GetId()] = outpost;
                    break;
                }
                case SiegeType.AGENT_FIGHT:
                {
                    agentLoc = new AgentLocation(template);
                    siegeLocations[template.GetId()] = agentLoc;
                    break;
                }
            }
        }
    }

    public int Size()
    {
        return siegeLocations.Count;
    }

    public Dictionary<int, ArtifactLocation> GetArtifacts()
    {
        return artifactLocations;
    }

    public Dictionary<int, FortressLocation> GetFortress()
    {
        return fortressLocations;
    }

    public Dictionary<int, OutpostLocation> GetOutpost()
    {
        return outpostLocations;
    }

    public Dictionary<int, SiegeLocation> GetSiegeLocations()
    {
        return siegeLocations;
    }

    public AgentLocation GetAgentLoc()
    {
        return agentLoc;
    }
}
