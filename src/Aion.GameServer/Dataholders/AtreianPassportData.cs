using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Event;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/AtreianPassportData (Alcapwnd, ViAl). @XmlRootElement(login_events); afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("login_events")]
public class AtreianPassportData
{
    [XmlElement("login_event")] public List<AtreianPassport> list;

    [XmlIgnore] private Dictionary<int, AtreianPassport> passportData = new();

    public void AfterUnmarshal(object parent)
    {
        foreach (AtreianPassport passport in list)
        {
            passportData[passport.GetId()] = passport;
        }
        list = null;
    }

    public int Size()
    {
        return passportData.Count;
    }

    public Dictionary<int, AtreianPassport> GetAll()
    {
        return passportData;
    }

    public AtreianPassport GetAtreianPassportId(int id)
    {
        return passportData.TryGetValue(id, out var v) ? v : null;
    }
}
