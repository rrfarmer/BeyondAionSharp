namespace Aion.GameServer.Model.Templates.Rewards;

/// <summary>Java parity: model/templates/rewards/RewardItem (Neon).</summary>
public class RewardItem
{
    private readonly int id;
    private readonly long count;

    public RewardItem(int id, long count)
    {
        this.id = id;
        this.count = count;
    }

    public int GetId()
    {
        return id;
    }

    public long GetCount()
    {
        return count;
    }

    public override string ToString()
    {
        return "RewardItem [id=" + id + ", count=" + count + "]";
    }
}
