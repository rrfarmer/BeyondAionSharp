using System.Xml.Serialization;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/AbsoluteSlowEffect (Dtem). @XmlType(name)→[XmlType]. BufEffect red-tolerated.</summary>
[XmlType("AbsoluteSlowEffect")]
public class AbsoluteSlowEffect : BufEffect
{
}
