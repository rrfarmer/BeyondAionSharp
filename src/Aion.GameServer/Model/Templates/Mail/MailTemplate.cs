using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model;

namespace Aion.GameServer.Model.Templates.Mail;

/// <summary>Java parity: model/templates/mail/MailTemplate (Rolandas).</summary>
[XmlType("MailTemplate")]
public class MailTemplate
{
    // Java parity: @XmlElements mapping element names to MailPart subtypes.
    [XmlElement("sender", typeof(Sender))]
    [XmlElement("title", typeof(Title))]
    [XmlElement("header", typeof(Header))]
    [XmlElement("body", typeof(Body))]
    [XmlElement("tail", typeof(Tail))]
    public List<MailPart> mailParts;

    [XmlAttribute("name")] public string name;
    [XmlAttribute("race")] public Race race;

    [XmlIgnore] private Dictionary<MailPartType, MailPart> mailPartsMap = new Dictionary<MailPartType, MailPart>();

    // Java parity: afterUnmarshal(Unmarshaller, Object parent).
    public void AfterUnmarshal(object parent)
    {
        if (mailParts != null)
            foreach (MailPart part in mailParts)
            {
                mailPartsMap[((IMailFormatter) part).GetType_()] = part;
            }
        mailParts = null;
    }

    public MailPart GetSender()
    {
        return mailPartsMap.TryGetValue(MailPartType.SENDER, out var v) ? v : null;
    }

    public MailPart GetTitle()
    {
        return mailPartsMap.TryGetValue(MailPartType.TITLE, out var v) ? v : null;
    }

    public MailPart GetHeader()
    {
        return mailPartsMap.TryGetValue(MailPartType.HEADER, out var v) ? v : null;
    }

    public MailPart GetBody()
    {
        return mailPartsMap.TryGetValue(MailPartType.BODY, out var v) ? v : null;
    }

    public MailPart GetTail()
    {
        return mailPartsMap.TryGetValue(MailPartType.TAIL, out var v) ? v : null;
    }

    public string GetName()
    {
        return name;
    }

    public Race GetRace()
    {
        return race;
    }

    public string GetFormattedTitle(IMailFormatter customFormatter)
    {
        return GetTitle().GetFormattedString(customFormatter);
    }

    public string GetFormattedMessage(IMailFormatter customFormatter)
    {
        string headerStr = GetHeader().GetFormattedString(customFormatter);
        string bodyStr = GetBody().GetFormattedString(customFormatter);
        string tailStr = GetTail().GetFormattedString(customFormatter);
        string message = headerStr;
        if (message.Length == 0)
            message = bodyStr;
        else if (bodyStr.Length != 0)
        {
            message += "," + bodyStr;
        }
        if (message.Length == 0)
            message = tailStr;
        else if (tailStr.Length != 0)
        {
            message += "," + tailStr;
        }
        return message;
    }
}
