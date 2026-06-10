using System.Xml.Serialization;

namespace Aion.GameServer.Skillengine.Effect;

/// <summary>Java parity: skillengine/effect/AbsoluteSlowEffect (Dtem). @XmlType(name)→[XmlType]. BufEffect red-tolerated.</summary>
[XmlType("AbsoluteSlowEffect")]
public class AbsoluteSlowEffect : BufEffect
{
}
