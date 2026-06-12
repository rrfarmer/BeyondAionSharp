namespace Aion.GameServer.Model.GameObjects.State;

/// <summary>
/// Visual (hide/blink) states for creatures.
/// Java parity: model/gameobjects/state/CreatureVisualState.
/// </summary>
public enum CreatureVisualState
{
    VISIBLE = 0,   // Normal
    HIDE1 = 1,     // Hide I
    HIDE2 = 2,     // Hide II
    HIDE3 = 3,     // Hide by Artifact?
    HIDE5 = 5,     // No idea :D
    HIDE10 = 10,   // Hide from Npc?
    HIDE13 = 13,   // Hide from Npc?
    HIDE20 = 20,   // Hide from Npc?
    BLINKING = 64, // Blinking when entering to zone
}

public static class CreatureVisualStateExtensions
{
    // Java parity: getId()
    public static int GetId(this CreatureVisualState state) => (int)state;
}
