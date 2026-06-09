using System;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Mail;

/// <summary>Java parity: model/templates/mail/MailPartType (Rolandas).</summary>
[XmlType("MailPartType")]
public enum MailPartType
{
    CUSTOM,
    SENDER,
    TITLE,
    HEADER,
    BODY,
    TAIL
}

public static class MailPartTypeExtensions
{
    public static string Value(this MailPartType v) => v.ToString();

    public static MailPartType FromValue(string v) => (MailPartType) Enum.Parse(typeof(MailPartType), v);
}
