namespace Aion.GameServer.Model.GameObjects;

public sealed record PlayerMotion(int Id, int ExpireTimeSeconds, bool IsActive)
{
	// Java parity: model/gameobjects/player/motion/Motion.secondsUntilExpiration.
	public int SecondsUntilExpiration(DateTimeOffset now)
	{
		return ExpireTimeSeconds == 0 ? 0 : ExpireTimeSeconds - (int)now.ToUnixTimeSeconds();
	}

	public static int GetMotionType(int motionId)
	{
		// Java parity: model/gameobjects/player/motion/Motion.motionType.
		return motionId switch
		{
			1 or 5 or 11 or 15 or 20 or 21 or 22 or 23 => 1,
			2 or 6 or 12 or 16 or 24 => 2,
			3 or 7 or 13 or 17 or 26 => 3,
			4 or 8 or 14 or 18 or 25 => 4,
			9 or 10 or 19 => 5,
			_ => 0,
		};
	}
}
