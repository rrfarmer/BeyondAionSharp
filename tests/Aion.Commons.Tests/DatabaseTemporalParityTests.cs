using Aion.Commons.Configuration;
using Aion.Commons.Database;
using MySqlConnector;
using System.Data;

namespace Aion.Commons.Tests;

public sealed class DatabaseTemporalParityTests
{
	[Fact]
	public void JdbcUrl_TranslatesTimezoneCharsetAndTlsWithoutCopyingJdbcNames()
	{
		var parsed = DatabaseOptions.ParseJdbcMysqlUrl(
			"jdbc:mysql://db.example.test:3307/aion_gs?serverTimezone=America%2FNew_York&characterEncoding=UTF-8&sslMode=VERIFY_IDENTITY");
		var options = new DatabaseOptions
		{
			Server = parsed.Server,
			Port = parsed.Port,
			Database = parsed.Database,
			CharacterSet = parsed.CharacterSet,
			ConnectionTimeZone = parsed.ConnectionTimeZone,
			SslMode = parsed.SslMode,
		};

		var connectionString = DatabaseFactory.BuildConnectionString(options);
		var builder = new MySqlConnectionStringBuilder(connectionString);

		Assert.Equal("America/New_York", parsed.ConnectionTimeZone);
		Assert.Equal("America/New_York", DatabaseFactory.TranslateConnectionTimeZone(parsed.ConnectionTimeZone));
		Assert.Equal("utf8mb4", builder.CharacterSet);
		Assert.Equal(MySqlSslMode.VerifyFull, builder.SslMode);
		Assert.Equal(MySqlDateTimeKind.Unspecified, builder.DateTimeKind);
		Assert.DoesNotContain("serverTimezone", connectionString, StringComparison.OrdinalIgnoreCase);
		Assert.DoesNotContain("characterEncoding", connectionString, StringComparison.OrdinalIgnoreCase);
		Assert.DoesNotContain("sslMode=VERIFY_IDENTITY", connectionString, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void JdbcUrl_ConnectionTimeZoneUtc_UsesUtcDriverKindAndSessionValue()
	{
		var parsed = DatabaseOptions.ParseJdbcMysqlUrl(
			"jdbc:mysql://localhost/aion_ls?connectionTimeZone=UTC&characterEncoding=utf8mb4&useSSL=false");
		var options = new DatabaseOptions
		{
			Server = parsed.Server,
			Port = parsed.Port,
			Database = parsed.Database,
			CharacterSet = parsed.CharacterSet,
			ConnectionTimeZone = parsed.ConnectionTimeZone,
			SslMode = parsed.SslMode,
		};
		var builder = new MySqlConnectionStringBuilder(DatabaseFactory.BuildConnectionString(options));

		Assert.Equal("+00:00", DatabaseFactory.TranslateConnectionTimeZone(parsed.ConnectionTimeZone));
		Assert.Equal(MySqlDateTimeKind.Utc, builder.DateTimeKind);
		Assert.Equal(MySqlSslMode.Disabled, builder.SslMode);
	}

	[Theory]
	[InlineData("useSSL=true", MySqlSslMode.Preferred)]
	[InlineData("useSSL=true&requireSSL=true", MySqlSslMode.Required)]
	[InlineData("useSSL=true&verifyServerCertificate=true", MySqlSslMode.VerifyCA)]
	public void JdbcUrl_LegacyConnectorJSslFlagsMapToEquivalentTlsMode(string query, MySqlSslMode expected)
	{
		var parsed = DatabaseOptions.ParseJdbcMysqlUrl($"jdbc:mysql://localhost/aion_ls?{query}");

		Assert.Equal(expected, parsed.SslMode);
	}

	[Theory]
	[InlineData("cachePrepStmts=true", "cachePrepStmts")]
	[InlineData("characterEncoding=windows-1252", "windows-1252")]
	[InlineData("useSSL=perhaps", "perhaps")]
	public void JdbcUrl_UnsupportedOrUntranslatableOptionFailsVisibly(string query, string expectedMessage)
	{
		var exception = Assert.Throws<FormatException>(
			() => DatabaseOptions.ParseJdbcMysqlUrl($"jdbc:mysql://localhost/aion_ls?{query}"));

		Assert.Contains(expectedMessage, exception.Message, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void JdbcUrl_RejectsAmbiguousTimezoneAliases()
	{
		var exception = Assert.Throws<FormatException>(
			() => DatabaseOptions.ParseJdbcMysqlUrl(
				"jdbc:mysql://localhost/aion_ls?serverTimezone=UTC&connectionTimeZone=SERVER"));

		Assert.Contains("only one", exception.Message, StringComparison.OrdinalIgnoreCase);
	}

	[Theory]
	[InlineData(0, 0u)]
	[InlineData(1, 1u)]
	[InlineData(999, 1u)]
	[InlineData(1000, 1u)]
	[InlineData(1500, 2u)]
	[InlineData(5000, 5u)]
	public void ConnectionTimeout_MillisecondValueNeverExpiresEarlierThanJava(int milliseconds, uint expectedSeconds)
	{
		var connectionString = DatabaseFactory.BuildConnectionString(
			new DatabaseOptions { ConnectionTimeout = milliseconds });
		var builder = new MySqlConnectionStringBuilder(connectionString);

		Assert.Equal(expectedSeconds, builder.ConnectionTimeout);
		if (milliseconds > 0)
			Assert.True(builder.ConnectionTimeout * 1000L >= milliseconds);
	}

	[Fact]
	public void ConnectionTimeout_NegativeValueFailsVisibly()
	{
		Assert.Throws<ArgumentOutOfRangeException>(
			() => DatabaseFactory.BuildConnectionString(new DatabaseOptions { ConnectionTimeout = -1 }));
	}

	[Theory]
	[InlineData(2026, 1, 15, 17, 1_768_496_400L)]
	[InlineData(2026, 7, 15, 16, 1_784_131_200L)]
	public void NewYorkWinterAndSummerInstants_RoundTripLikeJavaTimestampGetTime(
		int year,
		int month,
		int day,
		int expectedUtcHour,
		long expectedEpochSeconds)
	{
		var newYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
		var wallClock = new DateTime(year, month, day, 12, 0, 0, DateTimeKind.Unspecified);
		var utcInstant = TimeZoneInfo.ConvertTimeToUtc(wallClock, newYork);

		Assert.Equal(expectedUtcHour, utcInstant.Hour);
		Assert.Equal(expectedEpochSeconds, DatabaseTimestamp.ToUnixTimeSeconds(utcInstant));

		// The repositories persist this epoch through UNIX_TIMESTAMP/FROM_UNIXTIME. A reload therefore
		// has the same instant that Java Timestamp.getTime() exposes, independent of the test host zone.
		var reloaded = DatabaseTimestamp.FromUnixTimeSeconds(expectedEpochSeconds);
		Assert.Equal(DateTimeKind.Utc, reloaded.Kind);
		Assert.Equal(expectedEpochSeconds * 1000, DatabaseTimestamp.ToUnixTimeMilliseconds(reloaded));
	}

	[Fact]
	public void RepositoryBoundary_RejectsUnspecifiedDateTimeInsteadOfUsingHostLocalZone()
	{
		var unspecified = new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Unspecified);

		var exception = Assert.Throws<ArgumentException>(() => DatabaseTimestamp.ToUnixTimeSeconds(unspecified));

		Assert.Contains("UTC instants", exception.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void SqlEpochProjectionReader_PreservesRequiredNullableAndOffsetInstants()
	{
		const long epochMillis = 1_784_131_200_000L;
		DataTable table = new();
		table.Columns.Add("required_epoch_millis", typeof(long));
		table.Columns.Add("nullable_epoch_millis", typeof(long));
		table.Rows.Add(epochMillis, DBNull.Value);

		using DataTableReader reader = table.CreateDataReader();
		Assert.True(reader.Read());

		DateTime required = DatabaseTimestamp.ReadUtcDateTime(reader, "required_epoch_millis");
		Assert.Equal(DateTimeKind.Utc, required.Kind);
		Assert.Equal(epochMillis, DatabaseTimestamp.ToUnixTimeMilliseconds(required));
		Assert.Equal(TimeSpan.Zero, DatabaseTimestamp.ReadDateTimeOffset(reader, "required_epoch_millis").Offset);
		Assert.Null(DatabaseTimestamp.ReadNullableUtcDateTime(reader, "nullable_epoch_millis"));
		Assert.Null(DatabaseTimestamp.ReadNullableDateTimeOffset(reader, "nullable_epoch_millis"));
	}

	[Fact]
	public void SqlEpochProjectionAndWriteValues_AreExplicitAndNullSafe()
	{
		Assert.Equal(
			"CAST(FLOOR(UNIX_TIMESTAMP(`created`) * 1000) AS SIGNED) AS `created_epoch_millis`",
			DatabaseTimestamp.UnixTimeMillisecondsSql("`created`", "`created_epoch_millis`"));
		Assert.Equal(DBNull.Value, DatabaseTimestamp.ToUnixTimeMillisecondsOrDbNull((DateTime?)null));
		Assert.Equal(DBNull.Value, DatabaseTimestamp.ToUnixTimeMillisecondsOrDbNull((DateTimeOffset?)null));
		Assert.Equal(
			1_784_131_200_000L,
			DatabaseTimestamp.ToUnixTimeMilliseconds(new DateTimeOffset(2026, 7, 15, 16, 0, 0, TimeSpan.Zero)));
	}

	[Fact]
	public void JavaEpochIntNarrowing_WrapsAfter2038InsteadOfThrowing()
	{
		const long firstSecondPastInt32 = 2_147_483_648L;
		DateTime utcInstant = DateTimeOffset.FromUnixTimeSeconds(firstSecondPastInt32).UtcDateTime;

		Assert.Equal(int.MinValue, DatabaseTimestamp.ToInt32UnixTimeSeconds(utcInstant));
		Assert.Equal(
			int.MinValue,
			DatabaseTimestamp.MillisecondsToInt32UnixTimeSeconds(firstSecondPastInt32 * 1000));
	}
}
