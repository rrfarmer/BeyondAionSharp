namespace Aion.GameServer.SpawnEngine;

/// <summary>Java parity: spawnengine/WalkerGroupShift (Rolandas).</summary>
public class WalkerGroupShift
{
    private float sagittalShift; // left and right (sides)
    private float coronalShift; // or dorsoventral (back and front)

    public const float DISTANCE = 2; // 2 meters distance by default

    public WalkerGroupShift(float leftRight, float backFront)
    {
        sagittalShift = leftRight;
        coronalShift = backFront;
    }

    /// <summary>left and right (sides)</summary>
    public float GetSagittalShift()
    {
        return sagittalShift;
    }

    /// <summary>dorsoventral (back and front)</summary>
    public float GetCoronalShift()
    {
        return coronalShift;
    }

    public void Set(WalkerGroupShift shift)
    {
        this.sagittalShift = shift.sagittalShift;
        this.coronalShift = shift.coronalShift;
    }
}
