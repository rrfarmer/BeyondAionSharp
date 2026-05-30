namespace Aion.GameServer.Services;

/// <summary>
/// Pure formula ports for Java Util, TimeUtil, and Rates utility methods.
/// </summary>
public static class GameUtilFormulaService
{
	// ---- Util.convertName ----

	public static string ConvertName(string name, bool allowCustomNames)
	{
		// Java parity: utils/Util.convertName(String).
		// If ALLOW_CUSTOM_NAMES → return as-is; else capitalize first letter + lowercase rest.
		if (string.IsNullOrEmpty(name))
			return string.Empty;
		if (allowCustomNames)
			return name;
		return char.ToUpperInvariant(name[0]) + name.Substring(1).ToLowerInvariant();
	}

	// ---- TimeUtil.isExpired ----

	public static bool IsExpired(long timeMillis, long currentTimeMillis)
	{
		// Java parity: utils/TimeUtil.isExpired(long) - return time < System.currentTimeMillis().
		return timeMillis < currentTimeMillis;
	}

	// ---- Rates.get ----

	public static float GetMembershipRate(int membershipLevel, float[] membershipRates)
	{
		// Java parity: model/gameobjects/player/Rates.get(Player, float[]).
		// Clamps membershipLevel to valid index range.
		if (membershipRates.Length == 0)
			return 1f; // Java logs warn and returns 1
		var index = Math.Min(membershipRates.Length - 1, membershipLevel);
		return membershipRates[index];
	}

	// ---- Mentoring check from Predicates ----

	public static bool CanBeMentoredBy(int playerLevel, int mentorLevel)
	{
		// Java parity: utils/collections/Predicates.Players.canBeMentoredBy(Player).
		// return player.getLevel() + 10 <= mentor.getLevel()
		return playerLevel + 10 <= mentorLevel;
	}
}
