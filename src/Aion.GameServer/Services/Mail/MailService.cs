using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dao;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items.Storage;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Templates.Mail;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Services.Trade;
using Aion.GameServer.Taskmanager.Tasks;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Audit;
using Aion.GameServer.Utils.Collections;
using Aion.GameServer.Utils.Idfactory;
using Aion.GameServer.World;
using ItemAddType = Aion.GameServer.Services.Items.ItemPacketService.ItemAddType;
using PersistentState = Aion.GameServer.Model.GameObjects.Persistable.PersistentState;

namespace Aion.GameServer.Services.Mail;

/// <summary>Java parity: services/mail/MailService (kosyachok). Static; sendMail (trading/length/blackcloud guards, recipient validation, commission/price math with quality rate, attached-item handling incl. disposition unpack + count split, kinah debit, store, mailbox update), validateRecipient, getQualityPriceRate, readMail, getAttachments (item/kinah by type), deleteMail, onPlayerLogin, sendMailList (stream filter/sort -> LINQ, SplitList paging). Timestamp(currentTimeMillis)->DateTimeOffset.FromUnixTimeMilliseconds(UtcNow...); Comparator.comparing(...).reversed()->OrderByDescending; Collections.singletonList->new List; named logger MAIL_LOG. Many model/DAO/SplitList types red-tolerated.</summary>
public class MailService
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger("MAIL_LOG");

    private MailService()
    {
    }

    public static void SendMail(Player sender, string recipientName, string title, string message, int attachedItemObjId, long attachedItemCount,
        long attachedKinah, LetterType letterType)
    {
        if (sender.IsTrading() || recipientName.Length > 16)
            return;
        if (letterType == LetterType.BLACKCLOUD || attachedKinah < 0)
        {
            AuditLogger.Log(sender, "tried to send letter of type " + letterType + " with " + attachedKinah + " Kinah");
            return;
        }

        if (title.Length > 20)
            title = title.Substring(0, 20);

        if (message.Length > 1000)
            message = message.Substring(0, 1000);

        PlayerCommonData recipientCommonData = PlayerService.GetOrLoadPlayerCommonData(recipientName);
        MailMessage status = ValidateRecipient(sender, recipientCommonData);
        if (status != MailMessage.MAIL_SEND_SUCCESS)
        {
            PacketSendUtility.SendPacket(sender, new SM_MAIL_SERVICE(status));
            return;
        }

        Item senderItem = null;
        int baseCost = letterType == LetterType.EXPRESS ? 500 : 10;
        int costFactor = letterType == LetterType.EXPRESS ? 5 : 1;
        long kinahMailCommission = 0;
        long itemMailCommission = 0;

        Storage senderInventory = sender.GetInventory();

        if (attachedItemObjId != 0 && attachedItemCount > 0)
        {
            senderItem = senderInventory.GetItemByObjId(attachedItemObjId);

            if (senderItem == null || senderItem.GetItemCount() < attachedItemCount)
            {
                PacketSendUtility.SendPacket(sender, SM_SYSTEM_MESSAGE.STR_MAIL_SEND_USED_ITEM());
                return;
            }

            if (senderItem.IsEquipped())
            {
                PacketSendUtility.SendPacket(sender, SM_SYSTEM_MESSAGE.STR_MAIL_SEND_CAN_NOT_SEND_EQUIPPED_ITEM());
                return;
            }

            if (!AdminService.GetInstance().CanOperate(sender, null, senderItem, "mail"))
                return;

            itemMailCommission = (long)(senderItem.GetItemTemplate().GetPrice() * GetQualityPriceRate(senderItem) * attachedItemCount * costFactor);
        }

        if (attachedKinah > 0)
            kinahMailCommission = (long)(attachedKinah * 0.01f * costFactor);

        long finalMailKinah = PricesService.GetPriceForService(baseCost + kinahMailCommission + itemMailCommission, sender.GetRace()) + attachedKinah;

        if (senderInventory.GetKinah() < finalMailKinah)
        {
            PacketSendUtility.SendPacket(sender, SM_SYSTEM_MESSAGE.STR_NOT_ENOUGH_MONEY());
            return;
        }

        Item attachedItem = null;
        if (senderItem != null)
        {
            // Check Mailing untradables with Cash items (Special courier passes)
            if (senderItem.GetPackCount() <= 0 && !senderItem.IsTradeable())
            {
                Disposition dispo = senderItem.GetItemTemplate().GetDisposition();
                if (dispo == null || dispo.GetId() == 0 || dispo.GetCount() == 0) // can not be traded, hack
                    return;

                if (senderInventory.GetItemCountByItemId(dispo.GetId()) >= dispo.GetCount())
                {
                    senderInventory.DecreaseByItemId(dispo.GetId(), dispo.GetCount());
                }
                else
                    return;
            }

            // reuse item in case of full decrease of count
            if (senderItem.GetItemCount() == attachedItemCount)
            {
                senderInventory.Remove(senderItem);
                PacketSendUtility.SendPacket(sender, new SM_DELETE_ITEM(attachedItemObjId));
                attachedItem = senderItem;
            }
            else if (senderItem.GetItemCount() > attachedItemCount)
            {
                attachedItem = ItemFactory.NewItem(senderItem.GetItemTemplate().GetTemplateId(), attachedItemCount);
                senderInventory.DecreaseItemCount(senderItem, attachedItemCount);
            }

            if (attachedItem == null)
                return;

            // unpack
            if (attachedItem.GetPackCount() > 0)
                attachedItem.SetPackCount(attachedItem.GetPackCount() * -1);

            attachedItem.SetItemLocation(StorageType.MAILBOX.GetId());
        }

        senderInventory.DecreaseKinah(finalMailKinah);
        Letter newLetter = new Letter(IDFactory.GetInstance().NextId(), recipientCommonData.GetPlayerObjId(), attachedItem, attachedKinah, title, message,
            sender.GetName(), DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()), true, letterType);

        // first save attached item for FK consistency
        if (attachedItem != null)
        {
            if (!InventoryDAO.Store(attachedItem, recipientCommonData.GetPlayerObjId()))
                return;
            // save item stones too
            ItemStoneListDAO.Save(new List<Item> { attachedItem });
        }
        // save letter
        if (!MailDAO.StoreLetter(newLetter))
            return;

        if (attachedItem != null && LoggingConfig.LOG_MAIL)
            log.LogInformation("Player: " + sender.GetName() + " sent item " + attachedItem.GetItemId() + " [" + attachedItem.GetItemName() + "] (count: "
                + attachedItem.GetItemCount() + ") to player " + recipientName);

        PacketSendUtility.SendPacket(sender, new SM_MAIL_SERVICE(status));
        SystemMailService.UpdateRecipientMailbox(recipientCommonData, newLetter);
    }

    private static MailMessage ValidateRecipient(Player sender, PlayerCommonData recipientCommonData)
    {
        if (recipientCommonData == null)
            return MailMessage.NO_SUCH_CHARACTER_NAME;
        if (recipientCommonData.GetRace() != sender.GetRace() && !sender.IsStaff())
            return MailMessage.MAIL_IS_ONE_RACE_ONLY;
        if (recipientCommonData.GetMailboxLetters() >= 100)
            return MailMessage.RECIPIENT_MAILBOX_FULL;
        Player p = World.GetInstance().GetPlayer(recipientCommonData.GetPlayerObjId());
        BlockList blockList = p != null ? p.GetBlockList() : BlockListDAO.Load(recipientCommonData.GetPlayerObjId());
        if (blockList.Contains(sender.GetObjectId()))
            return MailMessage.YOU_ARE_IN_RECIPIENT_IGNORE_LIST;
        return MailMessage.MAIL_SEND_SUCCESS;
    }

    private static float GetQualityPriceRate(Item senderItem)
    {
        switch (senderItem.GetItemTemplate().GetItemQuality())
        {
            case ItemQuality.MYTHIC:
            case ItemQuality.EPIC:
                return 0.05f;
            case ItemQuality.UNIQUE:
            case ItemQuality.LEGEND:
                return 0.04f;
            case ItemQuality.RARE:
                return 0.03f;
            default:
                return 0.02f;
        }
    }

    /// <summary>
    /// Read letter with specified letter id
    /// </summary>
    public static void ReadMail(Player player, int letterId)
    {
        Letter letter = player.GetMailbox().GetLetterFromMailbox(letterId);
        if (letter == null)
        {
            log.LogWarning("Cannot read mail " + player.GetObjectId() + " " + letterId);
            return;
        }

        PacketSendUtility.SendPacket(player, new SM_MAIL_SERVICE(player, letter, letter.GetTimeStamp().GetTime()));
        letter.SetReadLetter();
    }

    public static void GetAttachments(Player player, int letterId, byte attachmentType)
    {
        Letter letter = player.GetMailbox().GetLetterFromMailbox(letterId);

        if (letter == null)
            return;

        switch (attachmentType)
        {
            case 0:
                Item attachedItem = letter.GetAttachedItem();
                if (attachedItem == null)
                    return;
                if (player.GetInventory().IsFull())
                {
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MAIL_TAKE_ALL_CANCEL());
                    return;
                }
                if (attachedItem.GetExpireTime() != 0 && attachedItem.GetExpireTime() <= DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000)
                {
                    attachedItem.SetPersistentState(PersistentState.DELETED);
                    InventoryDAO.Store(attachedItem, player);
                }
                else
                {
                    if (player.GetInventory().Add(attachedItem, ItemAddType.MAIL) == null)
                        return;
                    ExpireTimerTask.GetInstance().RegisterExpirable(attachedItem, player);
                }
                PacketSendUtility.SendPacket(player, new SM_MAIL_SERVICE(letterId, attachmentType));
                letter.SetAttachedItem(null);
                break;
            case 1:
                // TODO normal fix
                // fix for kinah dupe
                long attachedKinahCount = letter.GetAttachedKinah();
                letter.RemoveAttachedKinah();
                if (!MailDAO.StoreLetter(letter))
                {
                    AuditLogger.Log(player, "tried to use kinah mail exploit. Location: " + player.GetPosition() + ", kinah count: " + attachedKinahCount);
                    return;
                }
                player.GetInventory().IncreaseKinah(attachedKinahCount);
                PacketSendUtility.SendPacket(player, new SM_MAIL_SERVICE(letterId, attachmentType));
                break;
        }
    }

    public static void DeleteMail(Player player, int[] mailObjId)
    {
        Mailbox mailbox = player.GetMailbox();
        foreach (int letterId in mailObjId)
        {
            mailbox.RemoveLetter(letterId);
            MailDAO.DeleteLetter(letterId);
        }
        PacketSendUtility.SendPacket(player, new SM_MAIL_SERVICE(mailObjId));
    }

    public static void OnPlayerLogin(Player player)
    {
        player.SetMailbox(MailDAO.LoadPlayerMailbox(player));
        PacketSendUtility.SendPacket(player, new SM_MAIL_SERVICE());
    }

    public static void SendMailList(Player player, bool isExpress, bool sendRefreshPacket)
    {
        if (sendRefreshPacket)
            PacketSendUtility.SendPacket(player, new SM_MAIL_SERVICE());

        IEnumerable<Letter> letterStream = player.GetMailbox().GetLetters();
        if (isExpress)
            letterStream = letterStream.Where(letter => letter.IsExpress() && letter.IsUnread());
        List<Letter> letters = letterStream.OrderByDescending(l => l.GetTimeStamp()).ToList();
        SplitList<Letter> mailSplitList = new DynamicServerPacketBodySplitList<Letter>(letters, true, SM_MAIL_SERVICE.STATIC_BODY_SIZE,
            SM_MAIL_SERVICE.DYNAMIC_BODY_PART_SIZE_CALCULATOR);
        mailSplitList.ForEach(part => PacketSendUtility.SendPacket(player, new SM_MAIL_SERVICE(player, part, part.IsLast())));
    }
}
