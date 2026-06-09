using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model;

namespace Aion.GameServer.Model.Templates.Mail;

/// <summary>Java parity: model/templates/mail/SysMail (Rolandas).</summary>
[XmlType("SysMail")]
public class SysMail
{
    [XmlElement("template")] private List<MailTemplate> templates;

    [XmlAttribute("name")] private string name;

    [XmlIgnore] private Dictionary<string, List<MailTemplate>> mailCaseTemplates = new Dictionary<string, List<MailTemplate>>();

    // Java parity: afterUnmarshal(Unmarshaller, Object parent).
    public void AfterUnmarshal(object parent)
    {
        foreach (MailTemplate template in templates)
        {
            string caseName = template.GetName().ToLower();
            if (!mailCaseTemplates.TryGetValue(caseName, out var sysTemplates))
            {
                sysTemplates = new List<MailTemplate>();
                mailCaseTemplates[caseName] = sysTemplates;
            }
            sysTemplates.Add(template);
        }
        templates = null;
    }

    public MailTemplate GetTemplate(string eventName, Race playerRace)
    {
        if (!mailCaseTemplates.TryGetValue(eventName.ToLower(), out var sysTemplates))
            return null;
        foreach (MailTemplate template in sysTemplates)
        {
            if (template.GetRace() == playerRace || template.GetRace() == Race.PC_ALL)
                return template;
        }
        return null;
    }

    public string GetName()
    {
        return name;
    }
}
