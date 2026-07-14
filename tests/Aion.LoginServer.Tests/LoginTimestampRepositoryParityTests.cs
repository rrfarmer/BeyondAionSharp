using System.Data;
using Aion.Commons.Database;
using Aion.LoginServer.Data;

namespace Aion.LoginServer.Tests;

public sealed class LoginTimestampRepositoryParityTests
{
	[Fact]
	public void AccountRow_CreationEpochMaterializesTheJavaTimestampInstant()
	{
		const long epochMillis = 1_784_131_200_000L;
		DataTable table = new();
		table.Columns.Add("id", typeof(int));
		table.Columns.Add("name", typeof(string));
		table.Columns.Add("password", typeof(string));
		table.Columns.Add("creation_date_epoch_millis", typeof(long));
		table.Columns.Add("access_level", typeof(byte));
		table.Columns.Add("membership", typeof(byte));
		table.Columns.Add("activated", typeof(byte));
		table.Columns.Add("last_server", typeof(sbyte));
		table.Columns.Add("last_ip", typeof(string));
		table.Columns.Add("last_mac", typeof(string));
		table.Columns.Add("ip_force", typeof(string));
		table.Columns.Add("allowed_hdd_serial", typeof(string));
		table.Rows.Add(7, "qa", "hash", epochMillis, (byte)1, (byte)2, (byte)1, (sbyte)-1, DBNull.Value, "aa-bb", DBNull.Value, DBNull.Value);

		using DataTableReader reader = table.CreateDataReader();
		Assert.True(reader.Read());
		var account = AccountRepository.ReadAccount(reader, useExternalAuth: false);

		Assert.Equal(DateTimeKind.Utc, account.CreationDate.Kind);
		Assert.Equal(epochMillis, DatabaseTimestamp.ToUnixTimeMilliseconds(account.CreationDate));
		Assert.Null(account.LastIp);
	}

	[Fact]
	public void AccountTimeRow_AllTimestampEpochsMaterializeAsUtcInstants()
	{
		const long lastActive = 1_768_496_400_000L;
		const long penaltyEnd = 1_768_500_000_000L;
		const long expiration = 1_784_131_200_000L;
		DataTable table = new();
		table.Columns.Add("last_active_epoch_millis", typeof(long));
		table.Columns.Add("session_duration", typeof(long));
		table.Columns.Add("accumulated_online", typeof(long));
		table.Columns.Add("accumulated_rest", typeof(long));
		table.Columns.Add("penalty_end_epoch_millis", typeof(long));
		table.Columns.Add("expiration_time_epoch_millis", typeof(long));
		table.Rows.Add(lastActive, 10L, 20L, 30L, penaltyEnd, expiration);

		using DataTableReader reader = table.CreateDataReader();
		Assert.True(reader.Read());
		var accountTime = AccountTimeRepository.ReadAccountTime(reader);

		Assert.Equal(lastActive, DatabaseTimestamp.ToUnixTimeMilliseconds(accountTime.LastLoginTime));
		Assert.Equal(penaltyEnd, DatabaseTimestamp.ToUnixTimeMilliseconds(accountTime.PenaltyEnd!.Value));
		Assert.Equal(expiration, DatabaseTimestamp.ToUnixTimeMilliseconds(accountTime.ExpirationTime!.Value));
		Assert.All(
			new[] { accountTime.LastLoginTime, accountTime.PenaltyEnd.Value, accountTime.ExpirationTime.Value },
			value => Assert.Equal(DateTimeKind.Utc, value.Kind));
	}

	[Fact]
	public void BannedIpRow_NullAndFiniteEndsMatchJavaGetTimestamp()
	{
		DataTable table = new();
		table.Columns.Add("id", typeof(int));
		table.Columns.Add("mask", typeof(string));
		table.Columns.Add("time_end_epoch_millis", typeof(long));
		table.Rows.Add(1, "10.0.0.0/8", DBNull.Value);
		table.Rows.Add(2, "192.0.2.1", 1_784_131_200_000L);

		using DataTableReader reader = table.CreateDataReader();
		Assert.True(reader.Read());
		Assert.Null(BannedIpRepository.ReadBannedIp(reader).TimeEnd);
		Assert.True(reader.Read());
		var finite = BannedIpRepository.ReadBannedIp(reader);
		Assert.Equal(DateTimeKind.Utc, finite.TimeEnd!.Value.Kind);
		Assert.Equal(1_784_131_200_000L, DatabaseTimestamp.ToUnixTimeMilliseconds(finite.TimeEnd.Value));
	}
}
