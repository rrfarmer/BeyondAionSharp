using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Housing;

/// <summary>Java parity: model/templates/housing/HousingEmblem (Rolandas).</summary>
[XmlType("HousingEmblem")]
public class HousingEmblem : PlaceableHouseObject
{
    [XmlAttribute("level")] public int level;

    public override byte GetTypeId()
    {
        return 11;
    }

    public int GetLevel()
    {
        return level;
    }
}
