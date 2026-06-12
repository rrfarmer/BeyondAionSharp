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
    NONE = 0,
    // Java parity: FADE_OUT_BEAM(1)
    FADE_OUT_BEAM = 1,
    // Java parity: FADE_OUT(2)
    FADE_OUT = 2,
    // Java parity: JUMP_IN(3)
    JUMP_IN = 3,
    // Java parity: JUMP_IN_STATUE(4)
    JUMP_IN_STATUE = 4,
    // Java parity: JUMP_IN_GATE(8)
    JUMP_IN_GATE = 8,
    // Java parity: BATTLEGROUND(0) — for custom battlegrounds/pvp-maps only; shares value 0 with None
    BATTLEGROUND = 0,
}

/// <summary>
/// Extension methods mirroring TeleportAnimation's Java instance methods.
/// Java parity: model/animations/TeleportAnimation::getDefaultArrivalAnimation,
///              model/animations/TeleportAnimation::getDefaultObjectDeleteAnimation.
/// </summary>
public static class TeleportAnimationExtensions
{
    // Java parity: TeleportAnimation::getId (returns the byte animation id)
    public static byte GetId(this TeleportAnimation teleport) => (byte)teleport;

    // Java parity: TeleportAnimation::getDefaultArrivalAnimation
    public static ArrivalAnimation GetDefaultArrivalAnimation(this TeleportAnimation teleport)
    {
        return teleport switch
        {
            TeleportAnimation.FADE_OUT_BEAM => ArrivalAnimation.FadeInBeam,
            TeleportAnimation.JUMP_IN_STATUE => ArrivalAnimation.JumpOutCameraFront,
            TeleportAnimation.JUMP_IN or TeleportAnimation.JUMP_IN_GATE => ArrivalAnimation.JumpOutCameraBehind,
            TeleportAnimation.BATTLEGROUND => ArrivalAnimation.LandingGlow,
            _ => ArrivalAnimation.Landing,
        };
    }

    // Java parity: TeleportAnimation::getDefaultObjectDeleteAnimation
    public static ObjectDeleteAnimation GetDefaultObjectDeleteAnimation(this TeleportAnimation teleport)
    {
        return teleport switch
        {
            TeleportAnimation.FADE_OUT_BEAM => ObjectDeleteAnimation.FadeOutBeam,
            TeleportAnimation.JUMP_IN or TeleportAnimation.JUMP_IN_GATE or TeleportAnimation.JUMP_IN_STATUE
                => ObjectDeleteAnimation.JumpIn,
            _ => ObjectDeleteAnimation.FadeOut,
        };
    }
}
