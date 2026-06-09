using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Ai;

/// <summary>Java parity: model/templates/ai/Bombs (xTz).</summary>
[XmlType("Bombs")]
public class Bombs
{
    [XmlElement("bomb")] private BombTemplate bombTemplate;

    public BombTemplate GetBombTemplate()
    {
        return bombTemplate;
    }
}
