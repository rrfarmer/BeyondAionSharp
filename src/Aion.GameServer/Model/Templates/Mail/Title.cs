using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Mail;

/// <summary>Java parity: model/templates/mail/Title (Rolandas).</summary>
[XmlType("Title")]
public class Title : MailPart
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
            return MailPartType.TITLE;
        return type.Value;
    }

    public override string GetParamValue(string name)
    {
        return "";
    }
}
