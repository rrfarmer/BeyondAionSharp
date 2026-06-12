namespace Aion.GameServer.Utils.Extensions;

// Java parity: java.util.Collection.isEmpty(). Java-faithful code calls collection.isEmpty();
// C# List/ICollection/IEnumerable have no IsEmpty member. This extension supplies it project-wide.
// Instance IsEmpty members (where a type defines its own) take precedence over this extension,
// so it only fills in for plain collections.
public static class CollectionIsEmptyExtension
{
	public static bool IsEmpty<T>(this IEnumerable<T> source) => !source.Any();

	// Java parity: java.lang.Iterable.forEach(Consumer). C# only defines ForEach on List<T>;
	// instance List<T>.ForEach takes precedence, so this only fills in for other enumerables.
	public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
	{
		foreach (var element in source)
			action(element);
	}
}
