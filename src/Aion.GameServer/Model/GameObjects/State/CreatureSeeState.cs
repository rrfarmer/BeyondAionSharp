namespace Aion.GameServer.Model.GameObjects.State;

/// <summary>
/// Stealth detection thresholds for creatures.
/// Java parity: model/gameobjects/state/CreatureSeeState.
/// </summary>
public enum CreatureSeeState
{
    Normal = 0,     // Normal visibility
    Search1 = 1,    // See-Through: Hide I
    Search2 = 2,    // See-Through: Hide II
    Search5 = 5,    // NPC stealth
    Search10 = 10,  // 3.0 NPC stealth
    Search20 = 20,
}

public static class CreatureSeeStateExtensions
{
    // Java parity: getId()
    public static int GetId(this CreatureSeeState state) => (int)state;
}
