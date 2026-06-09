using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model;

namespace Aion.GameServer.Model.Templates.Mail;

/// <summary>Java parity: model/templates/mail/Mails (Rolandas).</summary>
[XmlRoot("mails")]
public class Mails
{
    [XmlElement("mail")] private List<SysMail> sysMailTemplates;

    [XmlIgnore] private Dictionary<string, SysMail> sysMailByName = new Dictionary<string, SysMail>();

    // Java parity: afterUnmarshal(Unmarshaller, Object parent).
    public void AfterUnmarshal(object parent)
    {
        foreach (SysMail template in sysMailTemplates)
        {
            string sysMailName = template.GetName().ToLower();
            sysMailByName[sysMailName] = template;
        }
        sysMailTemplates = null;
    }

    public MailTemplate GetMailTemplate(string name, string eventName, Race playerRace)
    {
        if (!sysMailByName.TryGetValue(name.ToLower(), out var template))
            return null;
        return template.GetTemplate(eventName, playerRace);
    }

    public int Size()
    {
        return sysMailByName.Values.Count;
    }
}
