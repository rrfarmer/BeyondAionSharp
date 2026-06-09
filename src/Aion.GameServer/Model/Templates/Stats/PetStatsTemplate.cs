using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Stats;

/// <summary>Java parity: model/templates/stats/PetStatsTemplate (IlBuono). Distinct from pet/PetStatsTemplate.</summary>
[XmlRoot("petstats")]
public class PetStatsTemplate
{
    [XmlAttribute("reaction")] private string reaction;
    [XmlAttribute("run_speed")] private float runSpeed;
    [XmlAttribute("walk_speed")] private float walkSpeed;
    [XmlAttribute("height")] private float height;
    [XmlAttribute("altitude")] private float altitude;

    public string GetReaction()
    {
        return reaction;
    }

    public float GetRunSpeed()
    {
        return runSpeed;
    }

    public float GetWalkSpeed()
    {
        return walkSpeed;
    }

    public float GetHeight()
    {
        return height;
    }

    public float GetAltitude()
    {
        return altitude;
    }
}
