using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dao;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items.Storage;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.IdFactory;

namespace Aion.GameServer.Services.Mail;

/// <summary>Java parity: services/mail/SystemMailService (xTz). Singleton-style static; sendMail (validate item template/name lengths, mailbox capacity, build attached item in MAILBOX storage, store Letter+item, log), updateRecipientMailbox (offline counter vs online packet refresh, postman/express handling). Timestamp(currentTimeMillis)->DateTimeOffset.FromUnixTimeMilliseconds(UtcNow...); substring->Substring; named logger SYSMAIL_LOG. Letter/Mailbox/DAO red-tolerated.</summary>
public class SystemMailService
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger("SYSMAIL_LOG");

    private SystemMailService()
    {
    }

    public static bool SendMail(string sender, string recipientName, string title, string message, int attachedItemId, long attachedItemCount,
        long attachedKinahCount, LetterType letterType)
    {
        if (attachedItemId != 0)
        {
            if (attachedItemCount <= 0)
                return false;
            ItemTemplate itemTemplate = DataManager.ITEM_DATA.GetItemTemplate(attachedItemId);
            if (itemTemplate == null)
            {
                log.LogWarning("[SYSMAILSERVICE] > [SenderName: " + sender + "] [RecipientName: " + recipientName + "] RETURN ITEM ID:" + attachedItemId
                    + " ITEM COUNT " + attachedItemCount + " KINAH COUNT " + attachedKinahCount + " ITEM TEMPLATE IS MISSING ");
                return false;
            }
        }

        if (recipientName.Length > 16)
        {
            log.LogWarning("[SYSMAILSERVICE] > [SenderName: " + sender + "] [RecipientName: " + recipientName + "] ITEM RETURN" + attachedItemId + " ITEM COUNT "
                + attachedItemCount + " KINAH COUNT " + attachedKinahCount + " RECIPIENT NAME LENGTH > 16 ");
            return false;
        }

        if (!sender.StartsWith("$$") && sender.Length > 16)
        {
            log.LogWarning("[SYSMAILSERVICE] > [SenderName: " + sender + "] [RecipientName: " + recipientName + "] ITEM RETURN" + attachedItemId + " ITEM COUNT "
                + attachedItemCount + " KINAH COUNT " + attachedKinahCount + " SENDER NAME LENGTH > 16 ");
            return false;
        }

        if (title.Length > 20)
            title = title.Substring(0, 20);

        if (message.Length > 1000)
            message = message.Substring(0, 1000);

        PlayerCommonData recipientCommonData = PlayerService.GetOrLoadPlayerCommonData(recipientName);

        if (recipientCommonData == null)
        {
            log.LogInformation("[SYSMAILSERVICE] > [RecipientName: " + recipientName + "] NO SUCH CHARACTER NAME.");
            return false;
        }

        if (recipientCommonData.GetMailboxLetters() > 199)
        {
            log.LogInformation("[SYSMAILSERVICE] > [SenderName: " + sender + "] [RecipientName: " + recipientCommonData.GetName() + "] ITEM RETURN" + attachedItemId
                + " ITEM COUNT " + attachedItemCount + " KINAH COUNT " + attachedKinahCount + " MAILBOX FULL ");
            return false;
        }
        Item attachedItem = null;
        long finalAttachedKinahCount = 0;

        if (attachedItemId != 0)
        {
            Item senderItem = ItemFactory.NewItem(attachedItemId, attachedItemCount);
            if (senderItem != null)
            {
                senderItem.SetEquipped(false);
                senderItem.SetEquipmentSlot(0);
                senderItem.SetItemLocation(StorageType.MAILBOX.GetId());
                attachedItem = senderItem;
            }
        }

        if (attachedKinahCount > 0)
            finalAttachedKinahCount = attachedKinahCount;

        Letter newLetter = new Letter(IDFactory.GetInstance().NextId(), recipientCommonData.GetPlayerObjId(), attachedItem, finalAttachedKinahCount,
            title, message, sender, DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()), true, letterType);

        if (!MailDAO.StoreLetter(newLetter))
            return false;

        if (attachedItem != null)
            if (!InventoryDAO.Store(attachedItem, recipientCommonData.GetPlayerObjId()))
                return false;

        if (LoggingConfig.LOG_SYSMAIL)
            log.LogInformation("[SYSMAILSERVICE] > [SenderName: " + sender + "] [RecipientName: " + recipientName + "] RETURN ITEM ID:" + attachedItemId
                + " ITEM COUNT " + attachedItemCount + " KINAH COUNT " + attachedKinahCount + " MESSAGE SUCCESSFULLY SENDED ");

        UpdateRecipientMailbox(recipientCommonData, newLetter);
        return true;
    }

    internal static void UpdateRecipientMailbox(PlayerCommonData recipientCommonData, Letter newLetter)
    {
        Player recipient = recipientCommonData.GetPlayer();
        if (recipient == null)
        {
            recipientCommonData.SetMailboxLetters(recipientCommonData.GetMailboxLetters() + 1);
            MailDAO.UpdateOfflineMailCounter(recipientCommonData);
        }
        else if (recipient.GetMailbox() != null)
        { // Send mail update packets
            Mailbox mailbox = recipient.GetMailbox();
            mailbox.PutLetterToMailbox(newLetter);
            recipientCommonData.SetMailboxLetters(mailbox.Size());

            PacketSendUtility.SendPacket(recipient, new SM_MAIL_SERVICE());

            // refresh letters if recipient is currently looking into his mailbox
            if (mailbox.mailBoxState != 0)
            {
                bool isPostman = (mailbox.mailBoxState & PlayerMailboxState.EXPRESS) == PlayerMailboxState.EXPRESS;
                MailService.SendMailList(recipient, isPostman, false);
            }

            if (newLetter.GetLetterType() == LetterType.EXPRESS)
                PacketSendUtility.SendPacket(recipient, SM_SYSTEM_MESSAGE.STR_POSTMAN_NOTIFY());
            // else if (newLetter.getLetterType() == LetterType.BLACKCLOUD) PacketSendUtility.sendPacket(recipient, SM_SYSTEM_MESSAGE.STR_MAIL_CASHITEM_BUY(itemId));
        }
    }
}
