using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Mail;

/// <summary>Java parity: model/templates/mail/Tail (Rolandas).</summary>
[XmlType("Tail")]
public class Tail : MailPart
{
    [XmlAttribute("type")] protected MailPartType? type;

    public override MailPartType GetType_()
    {
        if (type == null)
            return MailPartType.TAIL;
        return type.Value;
    }

    public override string GetParamValue(string name)
    {
        return "";
    }
}
