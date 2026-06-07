namespace Aion.GameServer.Model.Animations;

/// <summary>
/// Animation IDs for use with SM_PLAYER_INFO packets.
/// Java parity: model/animations/ArrivalAnimation.
/// <br/>
/// Note: Java's <c>getId()</c> method = <c>(byte)value</c> in C#.
/// </summary>
public enum ArrivalAnimation : byte
{
    // Java parity: NONE(0)
    None = 0,
    // Java parity: LANDING(2)
    Landing = 2,
    // Java parity: FADE_IN_BEAM(4)
    FadeInBeam = 4,
    // Java parity: JUMP_OUT_CAMERA_BEHIND(10)
    JumpOutCameraBehind = 10,
    // Java parity: JUMP_OUT_CAMERA_FRONT(11)
    JumpOutCameraFront = 11,
    // Java parity: LANDING_GLOW(18)
    LandingGlow = 18,
}
