using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Mail;

/// <summary>Java parity: model/templates/mail/Tail (Rolandas).</summary>
[XmlType("Tail")]
public class Tail : MailPart
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
            return MailPartType.TAIL;
        return type.Value;
    }

    public override string GetParamValue(string name)
    {
        return "";
    }
}
