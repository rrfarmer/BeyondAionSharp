using System.Xml.Serialization;

namespace Aion.GameServer.SkillEngine.Model;

/// <summary>Alias teleport position. Java parity: skillengine/model/SkillAliasPosition (@XmlType("alias_pos")).</summary>
[XmlType("alias_pos")]
public class SkillAliasPosition
{
    [XmlAttribute("x")] public float X { get; set; }
    [XmlAttribute("y")] public float Y { get; set; }
    [XmlAttribute("z")] public float Z { get; set; }

    public float GetX() => X;
    public float GetY() => Y;
    public float GetZ() => Z;
}
