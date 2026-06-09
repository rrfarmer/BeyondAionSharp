namespace Aion.GameServer.Model.Siege;

/// <summary>Java parity: model/siege/Assaulter (Estrayl).</summary>
public class Assaulter
{
    private readonly int npcId;
    private readonly float spawnCost;
    private readonly int headingOffset;
    private readonly int distanceOffset;

    public Assaulter(int npcId, float spawnCost, int headingOffset, int distanceOffset)
    {
        this.npcId = npcId;
        this.spawnCost = spawnCost;
        this.headingOffset = headingOffset;
        this.distanceOffset = distanceOffset;
    }

    public int GetNpcId()
    {
        return npcId;
    }

    public float GetSpawnCost()
    {
        return spawnCost;
    }

    public int GetHeadingOffset()
    {
        return headingOffset;
    }

    public int GetDistanceOffset()
    {
        return distanceOffset;
    }
}
