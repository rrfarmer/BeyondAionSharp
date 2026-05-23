namespace Aion.GameServer.Model;

public readonly struct TeleportAnimation : IEquatable<TeleportAnimation>
{
	// Java parity: model/animations/TeleportAnimation IDs consumed by SM_TELEPORT_LOC.
	public static readonly TeleportAnimation None = new(0, "NONE", ArrivalAnimation.Landing, ObjectDeleteAnimation.FadeOut);
	public static readonly TeleportAnimation FadeOutBeam = new(1, "FADE_OUT_BEAM", ArrivalAnimation.FadeInBeam, ObjectDeleteAnimation.FadeOutBeam);
	public static readonly TeleportAnimation FadeOut = new(2, "FADE_OUT", ArrivalAnimation.Landing, ObjectDeleteAnimation.FadeOut);
	public static readonly TeleportAnimation JumpIn = new(3, "JUMP_IN", ArrivalAnimation.JumpOutCameraBehind, ObjectDeleteAnimation.JumpIn);
	public static readonly TeleportAnimation JumpInStatue = new(4, "JUMP_IN_STATUE", ArrivalAnimation.JumpOutCameraFront, ObjectDeleteAnimation.JumpIn);
	public static readonly TeleportAnimation JumpInGate = new(8, "JUMP_IN_GATE", ArrivalAnimation.JumpOutCameraBehind, ObjectDeleteAnimation.JumpIn);
	public static readonly TeleportAnimation Battleground = new(0, "BATTLEGROUND", ArrivalAnimation.LandingGlow, ObjectDeleteAnimation.FadeOut);

	private TeleportAnimation(
		byte id,
		string javaName,
		ArrivalAnimation defaultArrivalAnimation,
		ObjectDeleteAnimation defaultObjectDeleteAnimation)
	{
		Id = id;
		JavaName = javaName;
		DefaultArrivalAnimation = defaultArrivalAnimation;
		DefaultObjectDeleteAnimation = defaultObjectDeleteAnimation;
	}

	public byte Id { get; }

	public string JavaName { get; }

	public ArrivalAnimation DefaultArrivalAnimation { get; }

	public ObjectDeleteAnimation DefaultObjectDeleteAnimation { get; }

	public static explicit operator byte(TeleportAnimation animation) => animation.Id;

	public bool Equals(TeleportAnimation other) => Id == other.Id && JavaName == other.JavaName;

	public override bool Equals(object? obj) => obj is TeleportAnimation other && Equals(other);

	public override int GetHashCode() => HashCode.Combine(Id, JavaName);

	public override string ToString() => JavaName;

	public static bool operator ==(TeleportAnimation left, TeleportAnimation right) => left.Equals(right);

	public static bool operator !=(TeleportAnimation left, TeleportAnimation right) => !left.Equals(right);
}
