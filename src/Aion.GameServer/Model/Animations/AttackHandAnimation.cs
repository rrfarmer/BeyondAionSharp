namespace Aion.GameServer.Model.Animations;

/// <summary>
/// Attack hand animation IDs.
/// Java parity: model/animations/AttackHandAnimation.
/// <br/>
/// Note: Java's <c>getId()</c> method = <c>(byte)value</c> in C#.
/// </summary>
public enum AttackHandAnimation : byte
{
    // Java parity: MAIN_HAND(0)
    MainHand = 0,
    // Java parity: OFF_HAND(1) — only some NPCs support off-hand (e.g. Hyperion)
    OffHand = 1,
    // Java parity: RANDOM(2)
    Random = 2,
}
