using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Aion.Commons.Database;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Items.Storage;

namespace Aion.GameServer.Dao;

/// <summary>
/// Java parity: dao/MailDAO (@author kosyachok). JDBC DAO over the mail table. MIXED: loadPlayerMailbox/saves via the commons DB callback
/// helper (anonymous ParamReadStH/IUStH->nested), haveUnread/getUsedIDs via DatabaseFactory. LetterType.getLetterTypeById->
/// LetterTypeExtensions.GetLetterTypeById; getLetterType().getId()->GetLetterType().GetId() (extension). Letter ctor takes non-nullable
/// DateTime (Java Timestamp nullable) -> getTimestamp via IsDBNull?default(DateTime):GetDateTime; getTimeStamp()->DateTime directly.
/// Map&lt;Letter,Integer&gt;->Dictionary&lt;Letter,int&gt;. storeLetter switch NEW/UPDATE_REQUIRED->IPersistable.PersistentState; getUsedIDs
/// scrollable->forward-only. Uses now-ported InventoryDAO.LoadItems + ItemStoneListDAO.Load (lazy). SQL verbatim.
/// </summary>
public class MailDAO
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(MailDAO));

    public static Mailbox LoadPlayerMailbox(Player player)
    {
        Mailbox mailbox = new Mailbox(player);
        Dictionary<Letter, int> letters = new Dictionary<Letter, int>();
        List<Item> mailboxItems = null;

        DB.Select("SELECT * FROM mail WHERE mail_recipient_id = ?", new LoadMailboxHandler(player, letters));

        foreach (KeyValuePair<Letter, int> e in letters)
        {
            Letter letter = e.Key;
            int attachedItemObjId = e.Value;

            if (attachedItemObjId > 0)
            {
                if (mailboxItems == null) // lazy initialization to minimize DB io
                {
                    mailboxItems = InventoryDAO.LoadItems(player.GetObjectId(), StorageType.MAILBOX);
                    ItemStoneListDAO.Load(mailboxItems);
                }
                foreach (Item item in mailboxItems)
                    if (item.GetObjectId() == attachedItemObjId)
                        letter.SetAttachedItem(item);
            }
            letter.SetPersistentState(IPersistable.PersistentState.UPDATED);
            mailbox.PutLetterToMailbox(letter);
        }

        return mailbox;
    }

    private sealed class LoadMailboxHandler : ParamReadStH
    {
        private readonly Player player;
        private readonly Dictionary<Letter, int> letters;

        internal LoadMailboxHandler(Player player, Dictionary<Letter, int> letters)
        {
            this.player = player;
            this.letters = letters;
        }

        public void SetParams(MySqlCommand stmt)
        {
            stmt.Parameters.Add(new MySqlParameter { Value = player.GetObjectId() });
        }

        public void HandleRead(MySqlDataReader rset)
        {
            while (rset.Read())
            {
                int mailUniqueId = rset.GetInt32(rset.GetOrdinal("mail_unique_id"));
                int recipientId = rset.GetInt32(rset.GetOrdinal("mail_recipient_id"));
                string senderName = rset.GetString(rset.GetOrdinal("sender_name"));
                string mailTitle = rset.GetString(rset.GetOrdinal("mail_title"));
                string mailMessage = rset.GetString(rset.GetOrdinal("mail_message"));
                bool unread = rset.GetInt32(rset.GetOrdinal("unread")) == 1;
                int attachedItemObjId = rset.GetInt32(rset.GetOrdinal("attached_item_id"));
                long attachedKinahCount = rset.GetInt64(rset.GetOrdinal("attached_kinah_count"));
                LetterType letterType = LetterTypeExtensions.GetLetterTypeById(rset.GetInt32(rset.GetOrdinal("express")));
                int rtOrd = rset.GetOrdinal("recieved_time");
                DateTime receivedTime = rset.IsDBNull(rtOrd) ? default(DateTime) : rset.GetDateTime(rtOrd);
                letters[new Letter(mailUniqueId, recipientId, null, attachedKinahCount, mailTitle, mailMessage, senderName, receivedTime, unread, letterType)] =
                    attachedItemObjId;
            }
        }
    }

    public static bool HaveUnread(int playerId)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = "SELECT * FROM mail WHERE mail_recipient_id = ? ORDER BY recieved_time";

            stmt.Parameters.Add(new MySqlParameter { Value = playerId });
            using MySqlDataReader rset = stmt.ExecuteReader();
            while (rset.Read())
            {
                int unread = rset.GetInt32(rset.GetOrdinal("unread"));
                if (unread == 1)
                {
                    return true;
                }
            }
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not read mail for player: " + playerId + " from DB: " + e.Message);
        }
        return false;
    }

    public static void StoreMailbox(Player player)
    {
        Mailbox mailbox = player.GetMailbox();
        if (mailbox == null)
            return;
        ICollection<Letter> letters = mailbox.GetLetters();
        foreach (Letter letter in letters)
        {
            StoreLetter(letter);
        }
    }

    public static bool StoreLetter(Letter letter)
    {
        bool result = false;
        switch (letter.GetPersistentState())
        {
            case IPersistable.PersistentState.NEW:
                result = SaveLetter(letter);
                break;
            case IPersistable.PersistentState.UPDATE_REQUIRED:
                result = UpdateLetter(letter);
                break;
        }
        letter.SetPersistentState(IPersistable.PersistentState.UPDATED);

        return result;
    }

    private static bool SaveLetter(Letter letter)
    {
        int attachedItemId = 0;
        if (letter.GetAttachedItem() != null)
            attachedItemId = letter.GetAttachedItem().GetObjectId();

        int fAttachedItemId = attachedItemId;

        return DB.InsertUpdate(
            "INSERT INTO `mail` (`mail_unique_id`, `mail_recipient_id`, `sender_name`, `mail_title`, `mail_message`, `unread`, `attached_item_id`, `attached_kinah_count`, `express`, `recieved_time`) VALUES(?,?,?,?,?,?,?,?,?,?)",
            new SaveLetterHandler(letter, fAttachedItemId));
    }

    private sealed class SaveLetterHandler : IUStH
    {
        private readonly Letter letter;
        private readonly int fAttachedItemId;

        internal SaveLetterHandler(Letter letter, int fAttachedItemId)
        {
            this.letter = letter;
            this.fAttachedItemId = fAttachedItemId;
        }

        public void HandleInsertUpdate(MySqlCommand stmt)
        {
            stmt.Parameters.Add(new MySqlParameter { Value = letter.GetObjectId() });
            stmt.Parameters.Add(new MySqlParameter { Value = letter.GetRecipientId() });
            stmt.Parameters.Add(new MySqlParameter { Value = letter.GetSenderName() });
            stmt.Parameters.Add(new MySqlParameter { Value = letter.GetTitle() });
            stmt.Parameters.Add(new MySqlParameter { Value = letter.GetMessage() });
            stmt.Parameters.Add(new MySqlParameter { Value = letter.IsUnread() });
            stmt.Parameters.Add(new MySqlParameter { Value = fAttachedItemId });
            stmt.Parameters.Add(new MySqlParameter { Value = letter.GetAttachedKinah() });
            stmt.Parameters.Add(new MySqlParameter { Value = letter.GetLetterType().GetId() });
            stmt.Parameters.Add(new MySqlParameter { Value = letter.GetTimeStamp() });
            stmt.ExecuteNonQuery();
        }
    }

    private static bool UpdateLetter(Letter letter)
    {
        int attachedItemId = 0;
        if (letter.GetAttachedItem() != null)
            attachedItemId = letter.GetAttachedItem().GetObjectId();

        int fAttachedItemId = attachedItemId;

        return DB.InsertUpdate(
            "UPDATE mail SET  unread=?, attached_item_id=?, attached_kinah_count=?, `express`=?, recieved_time=? WHERE mail_unique_id=?", new UpdateLetterHandler(letter, fAttachedItemId));
    }

    private sealed class UpdateLetterHandler : IUStH
    {
        private readonly Letter letter;
        private readonly int fAttachedItemId;

        internal UpdateLetterHandler(Letter letter, int fAttachedItemId)
        {
            this.letter = letter;
            this.fAttachedItemId = fAttachedItemId;
        }

        public void HandleInsertUpdate(MySqlCommand stmt)
        {
            stmt.Parameters.Add(new MySqlParameter { Value = letter.IsUnread() });
            stmt.Parameters.Add(new MySqlParameter { Value = fAttachedItemId });
            stmt.Parameters.Add(new MySqlParameter { Value = letter.GetAttachedKinah() });
            stmt.Parameters.Add(new MySqlParameter { Value = letter.GetLetterType().GetId() });
            stmt.Parameters.Add(new MySqlParameter { Value = letter.GetTimeStamp() });
            stmt.Parameters.Add(new MySqlParameter { Value = letter.GetObjectId() });
            stmt.ExecuteNonQuery();
        }
    }

    public static bool DeleteLetter(int letterId)
    {
        return DB.InsertUpdate("DELETE FROM mail WHERE mail_unique_id=?", new DeleteLetterHandler(letterId));
    }

    private sealed class DeleteLetterHandler : IUStH
    {
        private readonly int letterId;

        internal DeleteLetterHandler(int letterId)
        {
            this.letterId = letterId;
        }

        public void HandleInsertUpdate(MySqlCommand stmt)
        {
            stmt.Parameters.Add(new MySqlParameter { Value = letterId });
            stmt.ExecuteNonQuery();
        }
    }

    public static void UpdateOfflineMailCounter(PlayerCommonData recipientCommonData)
    {
        DB.InsertUpdate("UPDATE players SET mailbox_letters=? WHERE name=?", new UpdateOfflineMailCounterHandler(recipientCommonData));
    }

    private sealed class UpdateOfflineMailCounterHandler : IUStH
    {
        private readonly PlayerCommonData recipientCommonData;

        internal UpdateOfflineMailCounterHandler(PlayerCommonData recipientCommonData)
        {
            this.recipientCommonData = recipientCommonData;
        }

        public void HandleInsertUpdate(MySqlCommand stmt)
        {
            stmt.Parameters.Add(new MySqlParameter { Value = recipientCommonData.GetMailboxLetters() });
            stmt.Parameters.Add(new MySqlParameter { Value = recipientCommonData.GetName() });
            stmt.ExecuteNonQuery();
        }
    }

    public static int[] GetUsedIDs()
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = "SELECT mail_unique_id FROM mail";
            using MySqlDataReader rs = stmt.ExecuteReader();
            List<int> ids = new List<int>();
            while (rs.Read())
                ids.Add(rs.GetInt32(rs.GetOrdinal("mail_unique_id")));
            return ids.ToArray();
        }
        catch (Exception e)
        {
            log.LogError(e, "Can't get list of IDs from mail table");
            return null;
        }
    }

    public static bool CleanMail(string recipient)
    {
        return DB.InsertUpdate(
            "DELETE FROM mail WHERE mail_recipient_id=(SELECT id FROM players WHERE name=?) AND attached_item_id=0 AND attached_kinah_count=0",
            new CleanMailHandler(recipient));
    }

    private sealed class CleanMailHandler : IUStH
    {
        private readonly string recipient;

        internal CleanMailHandler(string recipient)
        {
            this.recipient = recipient;
        }

        public void HandleInsertUpdate(MySqlCommand stmt)
        {
            stmt.Parameters.Add(new MySqlParameter { Value = recipient });
            stmt.ExecuteNonQuery();
        }
    }
}
