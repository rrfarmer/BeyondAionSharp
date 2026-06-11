using System.Xml.Serialization;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/ShapeChangeEffect (ATracer). @XmlType(name)→[XmlType]. TransformEffect red-tolerated.</summary>
[XmlType("ShapeChangeEffect")]
public class ShapeChangeEffect : TransformEffect
{
}
