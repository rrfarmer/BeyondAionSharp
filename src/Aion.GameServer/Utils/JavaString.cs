namespace Aion.GameServer.Utils;

/// <summary>String primitives whose contracts are defined by the Java reference server.</summary>
public static class JavaString
{
	/// <summary>Matches Java <c>String.hashCode()</c> over UTF-16 code units.</summary>
	public static int HashCode(string value)
	{
		ArgumentNullException.ThrowIfNull(value);

		var hash = 0;
		foreach (var codeUnit in value)
		{
			unchecked
			{
				hash = 31 * hash + codeUnit;
			}
		}
		return hash;
	}
}
