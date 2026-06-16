using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Pet;

/// <summary>Java parity: model/templates/pet/PetStatsTemplate (M@xx).</summary>
[XmlRoot("petstats")]
public class PetStatsTemplate
{
    [XmlAttribute("reaction")] public string reaction;
    [XmlAttribute("run_speed")] public float runSpeed;
    [XmlAttribute("walk_speed")] public float walkSpeed;
    [XmlAttribute("height")] public float height;
    [XmlAttribute("altitude")] public float altitude;

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
