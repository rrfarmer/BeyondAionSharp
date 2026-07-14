using System.Data;
using Aion.GameServer.Custom.Instance.Neuralnetwork;
using Aion.GameServer.Dao;

namespace Aion.GameServer.Tests;

public sealed class JdbcNullPrimitiveParityTests
{
    [Fact]
    public void CustomInstanceRows_ValidNullableValid_LoadWithJavaPrimitiveDefaults()
    {
        DataTable table = CreateCustomInstanceTable();
        table.Rows.Add(CreateCustomInstanceRow(101, 1f));

        object[] nullableRow = CreateCustomInstanceRow(102, 2f);
        for (int i = 13; i < nullableRow.Length; i++)
            nullableRow[i] = DBNull.Value;
        table.Rows.Add(nullableRow);

        table.Rows.Add(CreateCustomInstanceRow(103, 3f));

        List<PlayerModelEntry> entries = [];
        using DataTableReader reader = table.CreateDataReader();
        CustomInstancePlayerModelEntryDAO.ReadPlayerModelEntries(77, reader, entries);

        Assert.Equal(3, entries.Count);
        Assert.Equal([101, 102, 103], entries.Select(entry => entry.GetSkillID()));

        PlayerModelEntry nullableEntry = entries[1];
        Assert.Equal(0f, nullableEntry.GetTargetHPpercentage());
        Assert.Equal(0f, nullableEntry.GetTargetMPpercentage());
        Assert.False(nullableEntry.IsTargetFocusesPlayer());
        Assert.Equal(0f, nullableEntry.GetDistance());
        Assert.False(nullableEntry.IsTargetRooted());
        Assert.False(nullableEntry.IsTargetSilenced());
        Assert.False(nullableEntry.IsTargetBound());
        Assert.False(nullableEntry.IsTargetStunned());
        Assert.False(nullableEntry.IsTargetAetherhold());
        Assert.Equal(0, nullableEntry.GetTargetBuffCount());
        Assert.Equal(0, nullableEntry.GetTargetDebuffCount());
        Assert.False(nullableEntry.IsTargetIsShielded());

        Assert.Equal(3f, entries[2].GetTargetHPpercentage());
        Assert.Equal(13, entries[2].GetTargetBuffCount());
        Assert.True(entries[2].IsTargetIsShielded());
    }

    [Fact]
    public void RegisteredItemRows_ValidNullableValid_LoadWithJavaZeroDefaults()
    {
        DataTable table = new();
        table.Columns.Add("h", typeof(int));
        table.Columns.Add("expire_time", typeof(int));
        table.Rows.Add(11, 111);
        table.Rows.Add(DBNull.Value, DBNull.Value);
        table.Rows.Add(33, 333);

        List<(int Heading, int ExpireTime)> values = [];
        using DataTableReader reader = table.CreateDataReader();
        while (reader.Read())
        {
            values.Add((
                PlayerRegisteredItemsDAO.GetJavaInt32(reader, "h"),
                PlayerRegisteredItemsDAO.GetJavaInt32(reader, "expire_time")));
        }

        Assert.Equal([(11, 111), (0, 0), (33, 333)], values);
    }

    private static DataTable CreateCustomInstanceTable()
    {
        DataTable table = new();
        table.Columns.Add("timestamp_epoch_millis", typeof(long));
        table.Columns.Add("skill_id", typeof(int));
        table.Columns.Add("player_class_id", typeof(int));
        table.Columns.Add("player_hp_percentage", typeof(float));
        table.Columns.Add("player_mp_percentage", typeof(float));
        table.Columns.Add("player_is_rooted", typeof(bool));
        table.Columns.Add("player_is_silenced", typeof(bool));
        table.Columns.Add("player_is_bound", typeof(bool));
        table.Columns.Add("player_is_stunned", typeof(bool));
        table.Columns.Add("player_is_aetherhold", typeof(bool));
        table.Columns.Add("player_buff_count", typeof(int));
        table.Columns.Add("player_debuff_count", typeof(int));
        table.Columns.Add("player_is_shielded", typeof(bool));
        table.Columns.Add("target_hp_percentage", typeof(float));
        table.Columns.Add("target_mp_percentage", typeof(float));
        table.Columns.Add("target_focuses_player", typeof(bool));
        table.Columns.Add("distance", typeof(float));
        table.Columns.Add("target_is_rooted", typeof(bool));
        table.Columns.Add("target_is_silenced", typeof(bool));
        table.Columns.Add("target_is_bound", typeof(bool));
        table.Columns.Add("target_is_stunned", typeof(bool));
        table.Columns.Add("target_is_aetherhold", typeof(bool));
        table.Columns.Add("target_buff_count", typeof(int));
        table.Columns.Add("target_debuff_count", typeof(int));
        table.Columns.Add("target_is_shielded", typeof(bool));
        return table;
    }

    private static object[] CreateCustomInstanceRow(int skillId, float targetHp)
    {
        return
        [
            1_783_944_000_000L,
            skillId,
            4,
            75f,
            50f,
            true,
            false,
            true,
            false,
            true,
            7,
            8,
            true,
            targetHp,
            targetHp + 1,
            true,
            targetHp + 2,
            true,
            false,
            true,
            false,
            true,
            10 + (int)targetHp,
            20 + (int)targetHp,
            true,
        ];
    }
}
