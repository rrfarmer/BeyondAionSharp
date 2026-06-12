namespace Aion.GameServer.Model.Animations;

/// <summary>
/// Animation IDs for use with SM_DELETE and SM_PET packets.
/// Java parity: model/animations/ObjectDeleteAnimation.
/// <br/>
/// Note: Java's <c>getId()</c> method = <c>(byte)value</c> in C#.
/// </summary>
public enum ObjectDeleteAnimation : byte
{
    // Java parity: NONE(0)
    None = 0,
    // Java parity: FADE_OUT(1)
    FadeOut = 1,
    // Java parity: FADE_OUT_BEAM(2)
    FadeOutBeam = 2,
    // Java parity: JUMP_IN(11) — players and humanoids only
    JumpIn = 11,
    // Java parity: DELAYED(19) — also deletes flags from map
    Delayed = 19,

    // Java-name aliases (call sites use the Java SCREAMING_SNAKE names; same values).
    NONE = 0,
    FADE_OUT = 1,
    FADE_OUT_BEAM = 2,
    JUMP_IN = 11,
    DELAYED = 19,
}
