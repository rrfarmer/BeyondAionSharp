using System.Xml.Serialization;

namespace Aion.GameServer.Skillengine.Effect;

/// <summary>Java parity: skillengine/effect/ShapeChangeEffect (ATracer). @XmlType(name)→[XmlType]. TransformEffect red-tolerated.</summary>
[XmlType("ShapeChangeEffect")]
public class ShapeChangeEffect : TransformEffect
{
}
