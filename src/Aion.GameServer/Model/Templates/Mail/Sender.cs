using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Mail;

/// <summary>Java parity: model/templates/mail/Sender (Rolandas).</summary>
[XmlType("Sender")]
public class Sender : MailPart
{
    [XmlIgnore] protected MailPartType? type;

    [XmlAttribute("type")]
    public string TypeRaw
    {
        get => type?.ToString();
        set => type = string.IsNullOrEmpty(value) ? null : System.Enum.Parse<MailPartType>(value);
    }

    public override MailPartType GetType_()
    {
        if (type == null)
            return MailPartType.SENDER;
        return type.Value;
    }

    public override string GetParamValue(string name)
    {
        return "";
    }
}
