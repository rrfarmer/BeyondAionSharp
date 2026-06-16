using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Guides;

/// <summary>Java parity: model/templates/Guides/SurveyTemplate (xTz).</summary>
[XmlType("SurveyTemplate")]
public class SurveyTemplate
{
    [XmlAttribute("itemId")] public int itemId;
    [XmlAttribute("count")] public long count;

    /// <returns>the count</returns>
    public long GetCount()
    {
        return this.count;
    }

    /// <returns>the itemId</returns>
    public int GetItemId()
    {
        return this.itemId;
    }
}
