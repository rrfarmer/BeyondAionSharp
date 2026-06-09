using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Mail;

/// <summary>Java parity: model/templates/mail/Title (Rolandas).</summary>
[XmlType("Title")]
public class Title : MailPart
{
    [XmlAttribute("type")] protected MailPartType? type;

    public override MailPartType GetType_()
    {
        if (type == null)
            return MailPartType.TITLE;
        return type.Value;
    }

    public override string GetParamValue(string name)
    {
        return "";
    }
}
