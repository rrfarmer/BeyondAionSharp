namespace Aion.GameServer.Model.Templates.Rewards;

/// <summary>Java parity: model/templates/rewards/RewardEntryItem (KID, Neon).</summary>
public class RewardEntryItem : RewardItem
{
    private readonly int entryId;

    public RewardEntryItem(int entryId, int itemId, long count)
        : base(itemId, count)
    {
        this.entryId = entryId;
    }

    public int GetEntryId()
    {
        return entryId;
    }
}
