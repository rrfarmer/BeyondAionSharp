using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.World;

/// <summary>Java parity: model/templates/world/AiInfo — NPC AI chase distances.</summary>
[XmlType("AiInfo")]
public class AiInfo
{
    public static readonly AiInfo Default = new();

    [XmlAttribute("chase_target")] public int ChaseTarget { get; set; } = 50;
    [XmlAttribute("chase_home")]   public int ChaseHome   { get; set; } = 200;

    public int GetChaseTarget() => ChaseTarget;
    public int GetChaseHome()   => ChaseHome;
}
