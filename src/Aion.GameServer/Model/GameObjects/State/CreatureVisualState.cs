namespace Aion.GameServer.Model.GameObjects.State;

/// <summary>
/// Visual (hide/blink) states for creatures.
/// Java parity: model/gameobjects/state/CreatureVisualState.
/// </summary>
public enum CreatureVisualState
{
    Visible = 0,   // Normal
    Hide1 = 1,     // Hide I
    Hide2 = 2,     // Hide II
    Hide3 = 3,     // Hide by Artifact?
    Hide5 = 5,     // No idea :D
    Hide10 = 10,   // Hide from Npc?
    Hide13 = 13,   // Hide from Npc?
    Hide20 = 20,   // Hide from Npc?
    Blinking = 64, // Blinking when entering to zone
}

public static class CreatureVisualStateExtensions
{
    // Java parity: getId()
    public static int GetId(this CreatureVisualState state) => (int)state;
}
