using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Mail;

/// <summary>Java parity: model/templates/mail/Body (Rolandas).</summary>
[XmlType("Body")]
public class Body : MailPart
{
    [XmlAttribute("type")] protected MailPartType? type;

    public override MailPartType GetType_()
    {
        if (type == null)
            return MailPartType.BODY;
        return type.Value;
    }

    public override string GetParamValue(string name)
    {
        return "";
    }
}
