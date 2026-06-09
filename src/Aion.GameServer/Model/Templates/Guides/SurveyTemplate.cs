using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Guides;

/// <summary>Java parity: model/templates/Guides/SurveyTemplate (xTz).</summary>
[XmlType("SurveyTemplate")]
public class SurveyTemplate
{
    [XmlAttribute("itemId")] private int itemId;
    [XmlAttribute("count")] private long count;

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
