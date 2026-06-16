using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/TitleData. @XmlRootElement(player_titles); afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("player_titles")]
public class TitleData
{
    [XmlElement("title")] public List<TitleTemplate> tts;

    [XmlIgnore] private readonly Dictionary<int, TitleTemplate> titles = new();

    public void AfterUnmarshal(object parent)
    {
        foreach (TitleTemplate tt in tts)
        {
            titles[tt.GetTitleId()] = tt;
        }
        tts = null;
    }

    public TitleTemplate GetTitleTemplate(int titleId)
    {
        return titles.TryGetValue(titleId, out var v) ? v : null;
    }

    public int Size()
    {
        return titles.Count;
    }
}
