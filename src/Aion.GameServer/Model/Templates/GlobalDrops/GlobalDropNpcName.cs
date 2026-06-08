using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.GlobalDrops;

/// <summary>Java parity: model/templates/globaldrops/GlobalDropNpcName.</summary>
[XmlType("GlobalDropNpcName")]
public class GlobalDropNpcName
{
    [XmlAttribute("value")] public string Name { get; set; } = string.Empty;
    [XmlAttribute("function")] public StringFunction Function { get; set; }
    public string GetValue() => Name;
    public StringFunction GetFunction() => Function;
}
