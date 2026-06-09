using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Housing;

/// <summary>Java parity: model/templates/housing/HousingJukeBox (Rolandas).</summary>
[XmlType("HousingJukeBox")]
public class HousingJukeBox : PlaceableHouseObject
{
    public override byte GetTypeId()
    {
        return 6;
    }
}
