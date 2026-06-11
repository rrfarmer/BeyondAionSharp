using System.Xml.Serialization;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/AbsoluteSnareEffect (Dtem). @XmlType(name)→[XmlType]. BufEffect red-tolerated.</summary>
[XmlType("AbsoluteSnareEffect")]
public class AbsoluteSnareEffect : BufEffect
{
}
