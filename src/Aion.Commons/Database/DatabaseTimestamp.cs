using System.Data.Common;

namespace Aion.Commons.Database;

/// <summary>
/// UTC-instant boundary for MySQL TIMESTAMP values. Java's java.sql.Timestamp always carries an
/// epoch and Timestamp.getTime() returns that epoch; C# repository code must not infer an epoch
/// from an Unspecified DateTime returned by a driver.
/// </summary>
public static class DatabaseTimestamp
{
	/// <summary>
	/// SQL projection for a MySQL TIMESTAMP column that preserves the represented instant even when
	/// the connection's session zone is not the process-local zone. The caller must supply a trusted
	/// SQL identifier/expression; this is not an escaping helper for user input.
	/// </summary>
	public static string UnixTimeMillisecondsSql(string timestampExpression, string alias)
	{
		return $"CAST(FLOOR(UNIX_TIMESTAMP({timestampExpression}) * 1000) AS SIGNED) AS {alias}";
	}

	public static DateTime RequireUtc(DateTime value)
	{
		if (value.Kind != DateTimeKind.Utc)
		{
			throw new ArgumentException(
				$"Database timestamp values must be UTC instants, but the DateTime kind was {value.Kind}.",
				nameof(value));
		}

		return value;
	}

	public static DateTime FromUnixTimeSeconds(long value)
	{
		return DateTimeOffset.FromUnixTimeSeconds(value).UtcDateTime;
	}

	public static DateTime FromUnixTimeMilliseconds(long value)
	{
		return DateTimeOffset.FromUnixTimeMilliseconds(value).UtcDateTime;
	}

	public static DateTime ReadUtcDateTime(DbDataReader reader, string epochMillisecondsColumn)
	{
		int ordinal = reader.GetOrdinal(epochMillisecondsColumn);
		if (reader.IsDBNull(ordinal))
			throw new InvalidOperationException($"Required database timestamp '{epochMillisecondsColumn}' was NULL.");
		return FromUnixTimeMilliseconds(reader.GetInt64(ordinal));
	}

	public static DateTime? ReadNullableUtcDateTime(DbDataReader reader, string epochMillisecondsColumn)
	{
		int ordinal = reader.GetOrdinal(epochMillisecondsColumn);
		return reader.IsDBNull(ordinal) ? null : FromUnixTimeMilliseconds(reader.GetInt64(ordinal));
	}

	public static DateTimeOffset ReadDateTimeOffset(DbDataReader reader, string epochMillisecondsColumn)
	{
		return new DateTimeOffset(ReadUtcDateTime(reader, epochMillisecondsColumn));
	}

	public static DateTimeOffset? ReadNullableDateTimeOffset(DbDataReader reader, string epochMillisecondsColumn)
	{
		DateTime? value = ReadNullableUtcDateTime(reader, epochMillisecondsColumn);
		return value.HasValue ? new DateTimeOffset(value.Value) : null;
	}

	public static long ToUnixTimeSeconds(DateTime value)
	{
		return new DateTimeOffset(RequireUtc(value)).ToUnixTimeSeconds();
	}

	public static long ToUnixTimeMilliseconds(DateTime value)
	{
		return new DateTimeOffset(RequireUtc(value)).ToUnixTimeMilliseconds();
	}

	public static long ToUnixTimeMilliseconds(DateTimeOffset value)
	{
		return value.ToUnixTimeMilliseconds();
	}

	public static object ToUnixTimeMillisecondsOrDbNull(DateTime? value)
	{
		return value.HasValue ? ToUnixTimeMilliseconds(value.Value) : DBNull.Value;
	}

	public static object ToUnixTimeMillisecondsOrDbNull(DateTimeOffset? value)
	{
		return value.HasValue ? ToUnixTimeMilliseconds(value.Value) : DBNull.Value;
	}

	public static int ToInt32UnixTimeSeconds(DateTime? value)
	{
		// Java parity: an explicit long-to-int cast narrows modulo 2^32; it does not throw.
		return value.HasValue ? unchecked((int)ToUnixTimeSeconds(value.Value)) : 0;
	}

	public static int MillisecondsToInt32UnixTimeSeconds(long? value)
	{
		// Java parity: (int) (timestamp.getTime() / 1000) wraps outside the int range.
		return value.HasValue ? unchecked((int)(value.Value / 1000)) : 0;
	}
}
