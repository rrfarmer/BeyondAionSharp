namespace Aion.GameServer.Model.GameObjects.Players;

/// <summary>Java parity: model/gameobjects/player/InRoll (xTz).</summary>
public class InRoll
{
    private int npcId;
    private int itemId;
    private int rollType;
    private int index;

    public InRoll(int npcId, int itemId, int index, int rollType)
    {
        this.npcId = npcId;
        this.itemId = itemId;
        this.index = index;
        this.rollType = rollType;
    }

    public int GetNpcId()
    {
        return npcId;
    }

    public int GetItemId()
    {
        return itemId;
    }

    public int GetIndex()
    {
        return index;
    }

    public int GetRollType()
    {
        return rollType;
    }

    public void SetNpcId(int npcId)
    {
        this.npcId = npcId;
    }

    public void SetItemId(int itemId)
    {
        this.itemId = itemId;
    }

    // Java parity: setIndexd (sic) — Java assigns itemId (not index); preserved 1:1.
    public void SetIndexd(int index)
    {
        this.index = itemId;
    }

    public void SetRollType(int rollType)
    {
        this.rollType = rollType;
    }
}
