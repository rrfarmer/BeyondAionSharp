using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Item;

/// <summary>
/// Idian burn-attack/defend parameters.
/// Java parity: model/templates/item/Idian (@XmlType("Idian")).
/// </summary>
[XmlType("Idian")]
public class Idian
{
    [XmlAttribute("burn_defend")] public int BurnDefend { get; set; }
    [XmlAttribute("burn_attack")] public int BurnAttack { get; set; }

    public int GetBurnAttack() => BurnAttack;
    public int GetBurnDefend() => BurnDefend;
}
