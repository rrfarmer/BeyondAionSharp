using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Housing;

/// <summary>Java parity: model/templates/housing/HousingStorage (Rolandas).</summary>
[XmlType("HousingStorage")]
public class HousingStorage : PlaceableHouseObject
{
    [XmlAttribute("warehouse_id")] public int warehouseId;

    public int GetWarehouseId()
    {
        return warehouseId;
    }

    public override byte GetTypeId()
    {
        return 2;
    }
}
