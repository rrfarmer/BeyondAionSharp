using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Housing;

/// <summary>Java parity: model/templates/housing/HousingChair (Rolandas).</summary>
[XmlType("HousingChair")]
public class HousingChair : PlaceableHouseObject
{
    public override byte GetTypeId()
    {
        return 5;
    }
}
