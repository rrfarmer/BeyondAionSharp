namespace Aion.GameServer.Utils.Extensions;

// Java parity: java.util.Collection.isEmpty(). Java-faithful code calls collection.isEmpty();
// C# List/ICollection/IEnumerable have no IsEmpty member. This extension supplies it project-wide.
// Instance IsEmpty members (where a type defines its own) take precedence over this extension,
// so it only fills in for plain collections.
public static class CollectionIsEmptyExtension
{
	public static bool IsEmpty<T>(this IEnumerable<T> source) => !source.Any();
}
