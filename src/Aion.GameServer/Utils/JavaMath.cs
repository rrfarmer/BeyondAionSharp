namespace Aion.GameServer.Utils;

/// <summary>Java numeric helpers whose semantics differ from the .NET defaults.</summary>
public static class JavaMath
{
	/// <summary>Matches Java <c>Math.round(float)</c>, including midpoint and saturation behavior.</summary>
	public static int Round(float value)
	{
		if (float.IsNaN(value))
			return 0;
		if (value <= int.MinValue)
			return int.MinValue;
		if (value >= int.MaxValue)
			return int.MaxValue;
		return (int)MathF.Floor(value + 0.5f);
	}

	/// <summary>Matches Java <c>Math.round(double)</c>, including midpoint and saturation behavior.</summary>
	public static long Round(double value)
	{
		if (double.IsNaN(value))
			return 0;
		if (value <= long.MinValue)
			return long.MinValue;
		if (value >= long.MaxValue)
			return long.MaxValue;
		return (long)Math.Floor(value + 0.5d);
	}
}
