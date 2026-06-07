namespace Aion.GameServer.Model.Animations;

/// <summary>
/// Animation IDs for use with SM_TELEPORT_LOC packets.
/// Java parity: model/animations/TeleportAnimation.
/// <br/>
/// Note: Java's <c>getId()</c> method = <c>(byte)value</c> in C#.
/// Note: <c>Battleground</c> and <c>None</c> share value 0, matching Java.
/// </summary>
public enum TeleportAnimation : byte
{
    // Java parity: NONE(0)
    None = 0,
    // Java parity: FADE_OUT_BEAM(1)
    FadeOutBeam = 1,
    // Java parity: FADE_OUT(2)
    FadeOut = 2,
    // Java parity: JUMP_IN(3)
    JumpIn = 3,
    // Java parity: JUMP_IN_STATUE(4)
    JumpInStatue = 4,
    // Java parity: JUMP_IN_GATE(8)
    JumpInGate = 8,
    // Java parity: BATTLEGROUND(0) — for custom battlegrounds/pvp-maps only; shares value 0 with None
    Battleground = 0,
}

/// <summary>
/// Extension methods mirroring TeleportAnimation's Java instance methods.
/// Java parity: model/animations/TeleportAnimation::getDefaultArrivalAnimation,
///              model/animations/TeleportAnimation::getDefaultObjectDeleteAnimation.
/// </summary>
public static class TeleportAnimationExtensions
{
    // Java parity: TeleportAnimation::getDefaultArrivalAnimation
    public static ArrivalAnimation GetDefaultArrivalAnimation(this TeleportAnimation teleport)
    {
        return teleport switch
        {
            TeleportAnimation.FadeOutBeam => ArrivalAnimation.FadeInBeam,
            TeleportAnimation.JumpInStatue => ArrivalAnimation.JumpOutCameraFront,
            TeleportAnimation.JumpIn or TeleportAnimation.JumpInGate => ArrivalAnimation.JumpOutCameraBehind,
            TeleportAnimation.Battleground => ArrivalAnimation.LandingGlow,
            _ => ArrivalAnimation.Landing,
        };
    }

    // Java parity: TeleportAnimation::getDefaultObjectDeleteAnimation
    public static ObjectDeleteAnimation GetDefaultObjectDeleteAnimation(this TeleportAnimation teleport)
    {
        return teleport switch
        {
            TeleportAnimation.FadeOutBeam => ObjectDeleteAnimation.FadeOutBeam,
            TeleportAnimation.JumpIn or TeleportAnimation.JumpInGate or TeleportAnimation.JumpInStatue
                => ObjectDeleteAnimation.JumpIn,
            _ => ObjectDeleteAnimation.FadeOut,
        };
    }
}
