using System;
using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Templates.Mail;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.Iteminfo;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_MAIL_SERVICE (kosyachok, Source, Neon). Mailbox state / message / list / read / attachment / delete by serviceId. Converges MailService/SystemMailService. Function->Func; switch-arrow->switch statement; byteLengthForString (inherited static)->ByteLengthForString; writeMe(getBuf())->WriteMe(GetBuf()); int... -> params int[]. Letter/Mailbox/MailMessage/ItemInfoBlob red-tolerated.</summary>
public class SM_MAIL_SERVICE : AionServerPacket
{
    // Java parity (writeImpl audited 1:1 vs game-server/.../serverpackets/SM_MAIL_SERVICE.java): 2026-06-17. writeImpl reads con.getActivePlayer().getMailbox() (live graph) + full Letter/Item/ItemInfoBlob graph -> T2 audit; serviceId switch (0/1/2/3/5/6), letters.size()*-1 on isLastPacket, totalCount+unreadCount*0x10000, item-blob-vs-writeQ(0)/writeQ(0)/writeD(0), (int)(time/1000), letterType.getId() explicit-id; byte-identical.
    public const int STATIC_BODY_SIZE = 8;
    public static readonly Func<Letter, int> DYNAMIC_BODY_PART_SIZE_CALCULATOR = letter => 22 + ByteLengthForString(letter.GetSenderName())
        + ByteLengthForString(letter.GetTitle());

    private Player player;
    private int serviceId;
    private List<Letter> letters;
    private int mailMessage;
    private Letter letter;
    private long time;
    private int letterId;
    private int[] letterIds;
    private byte attachmentType;
    private bool isLastPacket;

    public SM_MAIL_SERVICE()
    {
        this.serviceId = 0;
    }

    /// <summary>Send mailMessage (e.g. Send OK, Mailbox full etc.)</summary>
    public SM_MAIL_SERVICE(MailMessage mailMessage)
    {
        this.serviceId = 1;
        this.mailMessage = mailMessage.GetId();
    }

    /// <summary>Send mailbox info</summary>
    public SM_MAIL_SERVICE(Player player, List<Letter> letters, bool isLastPacket)
    {
        this.player = player;
        this.serviceId = 2;
        this.letters = letters;
        this.isLastPacket = isLastPacket;
    }

    /// <summary>used when reading letter</summary>
    public SM_MAIL_SERVICE(Player player, Letter letter, long time)
    {
        this.player = player;
        this.serviceId = 3;
        this.letter = letter;
        this.time = time;
    }

    /// <summary>used when getting attached items</summary>
    public SM_MAIL_SERVICE(int letterId, byte attachmentType)
    {
        this.serviceId = 5;
        this.letterId = letterId;
        this.attachmentType = attachmentType;
    }

    /// <summary>used when deleting letter</summary>
    public SM_MAIL_SERVICE(int[] letterIds)
    {
        this.serviceId = 6;
        this.letterIds = letterIds;
    }

    protected override void WriteImpl(AionConnection con)
    {
        Mailbox mailbox = con.GetActivePlayer().GetMailbox();
        int totalCount = mailbox.Size();
        int unreadCount = mailbox.GetUnreadCount();
        int unreadExpressCount = mailbox.GetUnreadCountByType(LetterType.EXPRESS);
        int unreadBlackCloudCount = mailbox.GetUnreadCountByType(LetterType.BLACKCLOUD);
        WriteC(serviceId);
        switch (serviceId)
        {
            case 0:
                WriteMailboxState(totalCount, unreadCount, unreadExpressCount, unreadBlackCloudCount);
                break;
            case 1:
                WriteMailMessage(mailMessage);
                break;
            case 2:
                WriteLettersList(letters);
                break;
            case 3:
                WriteLetterRead(letter, time, totalCount, unreadCount, unreadExpressCount, unreadBlackCloudCount);
                break;
            case 5:
                WriteLetterState(letterId, attachmentType);
                break;
            case 6:
                WriteLetterDelete(totalCount, unreadCount, unreadExpressCount, unreadBlackCloudCount, letterIds);
                break;
        }
    }

    private void WriteLettersList(List<Letter> letters)
    {
        WriteD(player.GetObjectId());
        WriteC(0);
        WriteH(isLastPacket ? letters.Count * -1 : letters.Count);
        foreach (Letter letter in letters)
        {
            WriteD(letter.GetObjectId());
            WriteS(letter.GetSenderName());
            WriteS(letter.GetTitle());
            WriteC(letter.IsUnread() ? 0 : 1); // isRead
            WriteD(letter.GetAttachedItem() == null ? 0 : letter.GetAttachedItem().GetObjectId());
            WriteD(letter.GetAttachedItem() == null ? 0 : letter.GetAttachedItem().GetItemTemplate().GetTemplateId());
            WriteQ(letter.GetAttachedKinah());
            WriteC(letter.GetLetterType().GetId());
        }
    }

    private void WriteMailMessage(int messageId)
    {
        WriteC(messageId);
    }

    private void WriteMailboxState(int totalCount, int unreadCount, int expressCount, int blackCloudCount)
    {
        WriteH(totalCount);
        WriteH(unreadCount);
        WriteH(expressCount);
        WriteH(blackCloudCount);
    }

    private void WriteLetterRead(Letter letter, long time, int totalCount, int unreadCount, int expressCount, int blackCloudCount)
    {
        WriteD(letter.GetRecipientId());
        WriteD(totalCount + unreadCount * 0x10000); // total count + unread hex
        WriteD(expressCount + blackCloudCount); // unread express + BC letters count
        WriteD(letter.GetObjectId());
        WriteD(letter.GetRecipientId());
        WriteS(letter.GetSenderName());
        WriteS(letter.GetTitle());
        WriteS(letter.GetMessage());

        Item item = letter.GetAttachedItem();
        if (item != null)
        {
            ItemTemplate itemTemplate = item.GetItemTemplate();

            WriteD(item.GetObjectId());
            WriteD(itemTemplate.GetTemplateId());
            WriteD(1); // unk
            WriteD(0); // unk
            WriteS(itemTemplate.GetL10n());

            ItemInfoBlob itemInfoBlob = ItemInfoBlob.GetFullBlob(player, item);
            itemInfoBlob.WriteMe(GetBuf());
        }
        else
        {
            WriteQ(0);
            WriteQ(0);
            WriteD(0);
        }

        WriteD((int)letter.GetAttachedKinah());
        WriteD(0); // AP reward for castle assault/defense (in future)
        WriteC(0);
        WriteD((int)(time / 1000));
        WriteC(letter.GetLetterType().GetId()); // mail type
    }

    private void WriteLetterState(int letterId, byte attachmentType)
    {
        WriteD(letterId);
        WriteC(attachmentType);
        WriteC(1);
    }

    private void WriteLetterDelete(int totalCount, int unreadCount, int expressCount, int blackCloudCount, params int[] letterIds)
    {
        WriteD(totalCount + unreadCount * 0x10000); // total count + unread hex
        WriteD(expressCount + blackCloudCount); // unread express + BC letters count
        WriteH(letterIds.Length);
        foreach (int letterId in letterIds)
            WriteD(letterId);
    }
}
