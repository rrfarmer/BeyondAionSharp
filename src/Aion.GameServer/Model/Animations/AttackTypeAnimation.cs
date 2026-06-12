namespace Aion.GameServer.Model.Animations;

/// <summary>
/// Attack type animation IDs.
/// Java parity: model/animations/AttackTypeAnimation.
/// <br/>
/// Note: Java's <c>getId()</c> method = <c>(byte)value</c> in C#.
/// </summary>
public enum AttackTypeAnimation : byte
{
    // Java parity: MELEE(0)
    MELEE = 0,
    // Java parity: RANGED(1)
    RANGED = 1,
}
