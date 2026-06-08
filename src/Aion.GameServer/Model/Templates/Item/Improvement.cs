using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Item;

/// <summary>
/// Conditioning/charge improvement parameters of an item.
/// Java parity: model/templates/item/Improvement (@XmlType("Improvement")).
/// </summary>
[XmlType("Improvement")]
public class Improvement
{
    [XmlAttribute("way")] public int Way { get; set; }
    [XmlAttribute("price2")] public int Price2 { get; set; }
    [XmlAttribute("price1")] public int Price1 { get; set; }
    [XmlAttribute("burn_defend")] public int BurnDefend { get; set; }
    [XmlAttribute("burn_attack")] public int BurnAttack { get; set; }
    [XmlAttribute("level")] public int Level { get; set; }

    public int GetLevel() => Level;
    public int GetChargeWay() => Way;
    public int GetPrice1() => Price1;
    public int GetPrice2() => Price2;
    public int GetBurnAttack() => BurnAttack;
    public int GetBurnDefend() => BurnDefend;
}
