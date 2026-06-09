using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Housing;

/// <summary>Java parity: model/templates/housing/HousingPassiveItem (Rolandas).</summary>
[XmlType("HousingPassiveItem")]
public class HousingPassiveItem : PlaceableHouseObject
{
    public override byte GetTypeId()
    {
        return 0;
    }
}
