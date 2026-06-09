using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Housing;

/// <summary>Java parity: model/templates/housing/HousingMoveableItem (Rolandas).</summary>
[XmlType("HousingMoveableItem")]
public class HousingMoveableItem : PlaceableHouseObject
{
    public override byte GetTypeId()
    {
        return 0;
    }
}
