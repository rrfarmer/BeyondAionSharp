using System.Xml.Serialization;
using Aion.GameServer.Model;

namespace Aion.GameServer.Model.Templates.Vortex;

/// <summary>Java parity: model/templates/vortex/VortexTemplate (Source).</summary>
[XmlType("Vortex")]
public class VortexTemplate
{
    [XmlAttribute("id")] public int id;
    [XmlAttribute("defends_race")] public Race dRace;
    [XmlAttribute("offence_race")] public Race oRace;
    [XmlElement("home_point")] public HomePoint home;
    [XmlElement("resurrection_point")] public ResurrectionPoint resurrection;
    [XmlElement("start_point")] public StartPoint start;

    /// <returns>the location id</returns>
    public int GetId()
    {
        return this.id;
    }

    /// <returns>the defenders race</returns>
    public Race GetDefendersRace()
    {
        return this.dRace;
    }

    /// <returns>the invaders race</returns>
    public Race GetInvadersRace()
    {
        return this.oRace;
    }

    public HomePoint GetHomePoint()
    {
        return home;
    }

    public ResurrectionPoint GetResurrectionPoint()
    {
        return resurrection;
    }

    public StartPoint GetStartPoint()
    {
        return start;
    }
}
