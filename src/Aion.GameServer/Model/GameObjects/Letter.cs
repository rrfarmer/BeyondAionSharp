using System;

namespace Aion.GameServer.Model.GameObjects;

/// <summary>Java parity: model/gameobjects/Letter (kosyachok).</summary>
public class Letter : AionObject, IPersistable
{
    private int recipientId;
    private Item attachedItem;
    private long attachedKinahCount;
    private string senderName;
    private string title;
    private string message;
    private bool unread;
    private bool express;
    private DateTime timeStamp;
    private IPersistable.PersistentState persistentState;
    private LetterType letterType;

    /// <summary>New letter constructor.</summary>
    public Letter(int objId, int recipientId, Item attachedItem, long attachedKinahCount, string title, string message, string senderName,
        DateTime timeStamp, bool unread, LetterType letterType)
        : base(objId)
    {
        this.express = letterType == LetterType.EXPRESS || letterType == LetterType.BLACKCLOUD;
        this.recipientId = recipientId;
        this.attachedItem = attachedItem;
        this.attachedKinahCount = attachedKinahCount;
        this.title = title;
        this.message = message;
        this.senderName = senderName;
        this.timeStamp = timeStamp;
        this.unread = unread;
        this.persistentState = IPersistable.PersistentState.NEW;
        this.letterType = letterType;
    }

    public override string Name => title;

    public int GetRecipientId()
    {
        return recipientId;
    }

    public Item GetAttachedItem()
    {
        return attachedItem;
    }

    public void SetAttachedItem(Item attachedItem)
    {
        this.attachedItem = attachedItem;
        this.persistentState = IPersistable.PersistentState.UPDATE_REQUIRED;
    }

    public long GetAttachedKinah()
    {
        return attachedKinahCount;
    }

    public string GetTitle()
    {
        return title;
    }

    public string GetMessage()
    {
        return message;
    }

    public string GetSenderName()
    {
        return senderName;
    }

    public LetterType GetLetterType()
    {
        return letterType;
    }

    public bool IsUnread()
    {
        return unread;
    }

    public void SetReadLetter()
    {
        this.unread = false;
        this.persistentState = IPersistable.PersistentState.UPDATE_REQUIRED;
    }

    public bool IsExpress()
    {
        return express;
    }

    public void SetExpress(bool express)
    {
        this.express = express;
        this.persistentState = IPersistable.PersistentState.UPDATE_REQUIRED;
    }

    public void SetLetterType(LetterType letterType)
    {
        this.letterType = letterType;
        if (letterType == LetterType.EXPRESS || letterType == LetterType.BLACKCLOUD)
            this.express = true;
        else
            this.express = false;
    }

    public IPersistable.PersistentState GetPersistentState()
    {
        return persistentState;
    }

    public void RemoveAttachedKinah()
    {
        this.attachedKinahCount = 0;
        this.persistentState = IPersistable.PersistentState.UPDATE_REQUIRED;
    }

    public void SetPersistentState(IPersistable.PersistentState state)
    {
        this.persistentState = state;
    }

    /// <returns>the timeStamp</returns>
    public DateTime GetTimeStamp()
    {
        return timeStamp;
    }
}
