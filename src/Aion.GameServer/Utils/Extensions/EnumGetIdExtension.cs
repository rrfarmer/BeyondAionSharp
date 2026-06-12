namespace Aion.GameServer.Utils.Extensions;

// Java parity: many Aion enums carry an int id field with getId() returning it; those enums are ported
// to C# with their underlying value == the Java id, so getId() == the numeric value. This global
// fallback supplies GetId() for every such enum. Enums whose id differs from the declaration value
// (e.g. PlayerAllianceEvent) define their own more-specific GetId() extension, which wins over this one.
public static class EnumGetIdExtension
{
	public static int GetId(this Enum value) => Convert.ToInt32(value);
}
