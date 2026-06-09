using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Housing;

/// <summary>Java parity: model/templates/housing/HousingPostbox (Rolandas).</summary>
[XmlType("HousingPostbox")]
public class HousingPostbox : PlaceableHouseObject
{
    public override byte GetTypeId()
    {
        return 3;
    }
}
