using System;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace Aion.GameServer.Model.GameObjects.Players;

/// <summary>Java parity: model/gameobjects/player/Mailbox.</summary>
public class Mailbox
{
    private readonly ConcurrentDictionary<int, Aion.GameServer.Model.GameObjects.Letter> mails = new ConcurrentDictionary<int, Aion.GameServer.Model.GameObjects.Letter>();
    private readonly ConcurrentDictionary<int, Aion.GameServer.Model.GameObjects.Letter> reserveMail = new ConcurrentDictionary<int, Aion.GameServer.Model.GameObjects.Letter>();
    private Aion.GameServer.Model.GameObjects.Players.Player owner;

    // 0x00 - closed
    // 0x01 - regular
    // 0x02 - express
    public byte mailBoxState = 0;

    public Mailbox(Aion.GameServer.Model.GameObjects.Players.Player player)
    {
        this.owner = player;
    }

    public void PutLetterToMailbox(Aion.GameServer.Model.GameObjects.Letter letter)
    {
        if (HaveFreeSlots())
            mails[letter.GetObjectId()] = letter;
        else
            reserveMail[letter.GetObjectId()] = letter;
    }

    public ICollection<Aion.GameServer.Model.GameObjects.Letter> GetLetters()
    {
        return new List<Aion.GameServer.Model.GameObjects.Letter>(mails.Values);
    }

    /// <summary>
    /// Get system letters which senders start with the string specified and were received since the last player login.
    /// </summary>
    /// <param name="substring">must start with special characters: % or $$</param>
    public List<Aion.GameServer.Model.GameObjects.Letter> GetNewSystemLetters(string substring)
    {
        List<Aion.GameServer.Model.GameObjects.Letter> letters = new List<Aion.GameServer.Model.GameObjects.Letter>();
        if (substring.StartsWith("%") || substring.StartsWith("$$"))
        {
            long lastOnlineMillis = owner.GetCommonData().GetLastOnline() == null ? 0 : ToMillis(owner.GetCommonData().GetLastOnline().Value);
            foreach (Aion.GameServer.Model.GameObjects.Letter letter in mails.Values)
            {
                if (!letter.IsUnread() || lastOnlineMillis > ToMillis(letter.GetTimeStamp()))
                    continue;
                if (letter.GetSenderName() == null || !letter.GetSenderName().StartsWith(substring))
                    continue;
                letters.Add(letter);
            }
        }
        return letters;
    }

    /// <summary>Get letter with specified letter id</summary>
    public Aion.GameServer.Model.GameObjects.Letter GetLetterFromMailbox(int letterObjId)
    {
        return mails.TryGetValue(letterObjId, out Aion.GameServer.Model.GameObjects.Letter letter) ? letter : null;
    }

    /// <summary>Check whether mailbox contains empty letters</summary>
    public bool HaveUnread()
    {
        foreach (Aion.GameServer.Model.GameObjects.Letter letter in mails.Values)
        {
            if (letter.IsUnread())
                return true;
        }
        return false;
    }

    public int GetUnreadCount()
    {
        int unreadCount = 0;
        foreach (Aion.GameServer.Model.GameObjects.Letter letter in mails.Values)
        {
            if (letter.IsUnread())
                unreadCount++;
        }
        return unreadCount;
    }

    public bool HaveUnreadByType(Aion.GameServer.Model.GameObjects.LetterType letterType)
    {
        foreach (Aion.GameServer.Model.GameObjects.Letter letter in mails.Values)
        {
            if (letter.IsUnread() && letter.GetLetterType() == letterType)
                return true;
        }
        return false;
    }

    public int GetUnreadCountByType(Aion.GameServer.Model.GameObjects.LetterType letterType)
    {
        int count = 0;
        foreach (Aion.GameServer.Model.GameObjects.Letter letter in mails.Values)
        {
            if (letter.IsUnread() && letter.GetLetterType() == letterType)
                count++;
        }
        return count;
    }

    public bool HaveFreeSlots()
    {
        return mails.Count < 100;
    }

    public void RemoveLetter(int letterId)
    {
        mails.TryRemove(letterId, out _);
        UploadReserveLetters();
        owner.GetCommonData().SetMailboxLetters(Size());
    }

    /// <summary>Current size of mailbox (including those in overflow storage)</summary>
    public int Size()
    {
        return mails.Count + reserveMail.Count;
    }

    public void UploadReserveLetters()
    {
        if (reserveMail.Count > 0 && HaveFreeSlots())
        {
            foreach (Aion.GameServer.Model.GameObjects.Letter letter in reserveMail.Values)
            {
                if (HaveFreeSlots())
                {
                    mails[letter.GetObjectId()] = letter;
                    reserveMail.TryRemove(letter.GetObjectId(), out _);
                }
                else
                    break;
            }
            Aion.GameServer.Services.Mail.MailService.SendMailList(GetOwner(), false, true);
        }
    }

    public Aion.GameServer.Model.GameObjects.Players.Player GetOwner()
    {
        return owner;
    }

    // Java parity: java.sql.Timestamp.getTime() returns epoch millis.
    private static long ToMillis(DateTime dt)
    {
        return new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
    }
}
