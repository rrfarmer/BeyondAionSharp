namespace Aion.GameServer.Model.Templates.Mail;

/// <summary>Java parity: model/templates/mail/MailMessage (kosyachok). Ids 0..6 match ordinals.</summary>
public enum MailMessage
{
    MAIL_SEND_SUCCESS,
    NO_SUCH_CHARACTER_NAME,
    RECIPIENT_MAILBOX_FULL,
    MAIL_IS_ONE_RACE_ONLY,
    YOU_ARE_IN_RECIPIENT_IGNORE_LIST,
    RECIPIENT_IGNORING_MAIL_FROM_PLAYERS_LOWER_206_LVL, // WTF??
    MAILSPAM_WAIT_FOR_SOME_TIME
}

public static class MailMessageExtensions
{
    public static int GetId(this MailMessage m) => (int) m;
}
