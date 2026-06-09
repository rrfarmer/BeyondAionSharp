namespace Aion.GameServer.Model.Templates.Mail;

/// <summary>Java parity: model/templates/mail/IMailFormatter (Rolandas).</summary>
public interface IMailFormatter
{
    // Java parity: getType() — renamed GetType_ (GetType collides with object.GetType()).
    MailPartType GetType_();

    string GetFormattedString(MailPartType partType);

    string GetParamValue(string name);
}
