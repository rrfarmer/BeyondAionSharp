namespace Aion.GameServer.Model.GameObjects;

// Java parity: model/gameobjects/Letter restored by dao/MailDAO.loadPlayerMailbox.
public sealed record PlayerMail(
	int Id,
	int RecipientId,
	string SenderName,
	string Title,
	string Message,
	bool IsUnread,
	int AttachedItemObjectId,
	int AttachedItemTemplateId,
	long AttachedKinah,
	int LetterType,
	DateTime ReceivedTime,
	InventoryItem? AttachedItem = null)
{
	public bool IsUnreadExpress => IsUnread && LetterType == 1;

	public bool IsUnreadBlackCloud => IsUnread && LetterType == 2;
}
