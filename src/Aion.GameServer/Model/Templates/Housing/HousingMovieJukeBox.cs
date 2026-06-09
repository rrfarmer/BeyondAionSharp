using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Housing;

/// <summary>Java parity: model/templates/housing/HousingMovieJukeBox (Rolandas).</summary>
[XmlType("HousingMovieJukeBox")]
public class HousingMovieJukeBox : HousingJukeBox
{
    public override byte GetTypeId()
    {
        return 0;
    }
}
