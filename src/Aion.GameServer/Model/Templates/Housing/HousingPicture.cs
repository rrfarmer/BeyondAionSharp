using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Housing;

/// <summary>Java parity: model/templates/housing/HousingPicture (Rolandas).</summary>
[XmlType("HousingPicture")]
public class HousingPicture : PlaceableHouseObject
{
    public override byte GetTypeId()
    {
        return 0;
    }
}
