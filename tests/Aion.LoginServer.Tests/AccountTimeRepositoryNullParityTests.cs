using System.Data;
using Aion.LoginServer.Data;

namespace Aion.LoginServer.Tests;

public sealed class AccountTimeRepositoryNullParityTests
{
    [Fact]
    public void NullablePrimitiveCounters_MapToJavaLongDefaults()
    {
        const long lastActiveEpochMillis = 1_783_944_000_000L;
        DateTime lastActive = DateTimeOffset.FromUnixTimeMilliseconds(lastActiveEpochMillis).UtcDateTime;
        DataTable table = new();
        table.Columns.Add("last_active_epoch_millis", typeof(long));
        table.Columns.Add("session_duration", typeof(long));
        table.Columns.Add("accumulated_online", typeof(long));
        table.Columns.Add("accumulated_rest", typeof(long));
        table.Columns.Add("penalty_end_epoch_millis", typeof(long));
        table.Columns.Add("expiration_time_epoch_millis", typeof(long));
        table.Rows.Add(lastActiveEpochMillis, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value);

        using DataTableReader reader = table.CreateDataReader();
        Assert.True(reader.Read());

        var accountTime = AccountTimeRepository.ReadAccountTime(reader);

        Assert.Equal(lastActive, accountTime.LastLoginTime);
        Assert.Equal(0, accountTime.SessionDuration);
        Assert.Equal(0, accountTime.AccumulatedOnlineTime);
        Assert.Equal(0, accountTime.AccumulatedRestTime);
        Assert.Null(accountTime.PenaltyEnd);
        Assert.Null(accountTime.ExpirationTime);
    }
}
