namespace Aion.GameServer.Utils.Extensions;

// Java parity: java.sql.Timestamp/java.util.Date.getTime() -> epoch milliseconds.
// C# only exposes ToUnixTimeMilliseconds() on DateTimeOffset; Java-faithful code calls it on the
// DateTime values loaded from the DB (stored UTC) or built from clocks. The DateTime is interpreted
// as UTC so the epoch result matches Java's getTime().
public static class DateTimeUnixExtensions
{
	public static long ToUnixTimeMilliseconds(this DateTime dateTime) =>
		new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)).ToUnixTimeMilliseconds();

	// Java parity: calling getTime() on a null Timestamp throws (NPE); Value access mirrors that.
	public static long ToUnixTimeMilliseconds(this DateTime? dateTime) =>
		dateTime!.Value.ToUnixTimeMilliseconds();

	// Java parity: java.util.Date/java.sql.Timestamp.getTime() -> epoch milliseconds.
	public static long GetTime(this DateTime dateTime) => dateTime.ToUnixTimeMilliseconds();
}
