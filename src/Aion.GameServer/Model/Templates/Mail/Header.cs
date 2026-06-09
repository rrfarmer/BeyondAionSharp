using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Mail;

/// <summary>Java parity: model/templates/mail/Header (Rolandas).</summary>
[XmlType("Header")]
public class Header : MailPart
{
    [XmlAttribute("type")] protected MailPartType? type;

    public override MailPartType GetType_()
    {
        if (type == null)
            return MailPartType.HEADER;
        return type.Value;
    }

    public override string GetParamValue(string name)
    {
        return "";
    }
}
