using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using Aion.GameServer.Model.Team.Legion;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Type = Aion.GameServer.Model.Team.Legion.LegionHistoryAction.Type;

namespace Aion.GameServer.Tests;

/// <summary>
/// Faithful golden coverage for network/aion/serverpackets/SM_LEGION_HISTORY. Drives the LIVE model
/// (LegionHistoryEntry + LegionHistoryAction class-enum + nested Type enum) exactly as
/// LegionService/CM_LEGION_HISTORY do, instead of the retired flat-snapshot reworked SmLegionHistory
/// packet. Java has no Unsafe golden test for this packet; this migrates the byte coverage onto the
/// faithful WriteImpl path. Per-entry layout: epochSeconds(D), actionId(C), 0(C), name(S,32),
/// description(S,32), H(0); trailing type ordinal(H).
/// </summary>
public sealed class SmLegionHistoryTests
{
    [Fact]
    public void EmptyHistory_WritesHeaderAndTypeOrdinalOnly()
    {
        // type REWARD => ordinal 1.
        var packet = new SM_LEGION_HISTORY(new List<LegionHistoryEntry>(), page: 0, type: Type.REWARD);
        var reader = new HexReader(Write(packet));

        Assert.Equal(0, reader.D()); // totalEntries
        Assert.Equal(0, reader.D()); // page
        Assert.Equal(0, reader.D()); // pageEntries count
        Assert.Equal(1, reader.H()); // type ordinal (REWARD = 1)
        Assert.True(reader.AtEnd);
    }

    [Fact]
    public void SingleEntry_WritesEntryFieldsAndFixedStrings()
    {
        // KICK => actionId 2; type LEGION => ordinal 0.
        var entry = new LegionHistoryEntry(1, 1717459200, LegionHistoryAction.KICK, "Hi", "d");
        var packet = new SM_LEGION_HISTORY(new List<LegionHistoryEntry> { entry }, page: 0, type: Type.LEGION);
        var reader = new HexReader(Write(packet));

        Assert.Equal(1, reader.D()); // totalEntries
        Assert.Equal(0, reader.D()); // page
        Assert.Equal(1, reader.D()); // pageEntries count

        Assert.Equal(1717459200, reader.D()); // epochSeconds
        Assert.Equal(2, reader.C()); // actionId (KICK)
        Assert.Equal(0, reader.C()); // unk

        // name: 32 fixed UTF-16 chars (zero padded) + terminator.
        Assert.Equal('H', (char)reader.H());
        Assert.Equal('i', (char)reader.H());
        for (var i = 2; i < 32; i++)
            Assert.Equal(0, reader.H());
        Assert.Equal(0, reader.H()); // name terminator

        // description: 32 fixed chars + terminator.
        Assert.Equal('d', (char)reader.H());
        for (var i = 1; i < 32; i++)
            Assert.Equal(0, reader.H());
        Assert.Equal(0, reader.H()); // description terminator

        Assert.Equal(0, reader.H()); // trailing per-entry H(0)
        Assert.Equal(0, reader.H()); // type ordinal (LEGION = 0)
        Assert.True(reader.AtEnd);
    }

    [Fact]
    public void Paging_SlicesEightPerPageAndReportsFullTotal()
    {
        // ENTRIES_PER_PAGE=8; page 1 of 10 yields entries [8,10).
        var history = new List<LegionHistoryEntry>();
        for (var i = 0; i < 10; i++)
            history.Add(new LegionHistoryEntry(i, i, LegionHistoryAction.CREATE, "n" + i, string.Empty));

        var packet = new SM_LEGION_HISTORY(history, page: 1, type: Type.WAREHOUSE);
        var reader = new HexReader(Write(packet));

        Assert.Equal(10, reader.D()); // totalEntries = full history size
        Assert.Equal(1, reader.D()); // page
        Assert.Equal(2, reader.D()); // pageEntries count (entries 8 and 9)

        Assert.Equal(8, reader.D()); // epochSeconds of entry index 8
    }

    [Fact]
    public void PageBeyondEnd_WritesEmptyPageButFullTotal()
    {
        // startIndex >= size -> empty list, but totalEntries still reports full size.
        var history = new List<LegionHistoryEntry>
        {
            new(0, 5, LegionHistoryAction.CREATE, "a", "b"),
        };

        var packet = new SM_LEGION_HISTORY(history, page: 3, type: Type.LEGION);
        var reader = new HexReader(Write(packet));

        Assert.Equal(1, reader.D()); // totalEntries
        Assert.Equal(3, reader.D()); // page
        Assert.Equal(0, reader.D()); // pageEntries count (out of range)
        Assert.Equal(0, reader.H()); // type ordinal
        Assert.True(reader.AtEnd);
    }

    private static byte[] Write(SM_LEGION_HISTORY packet)
    {
        var buffer = Aion.Commons.Nio.ByteBuffer.Allocate(8192).Order(Aion.Commons.Nio.ByteOrder.LITTLE_ENDIAN);
        packet.SetBuf(buffer);
        var writeImpl = typeof(AionServerPacket).GetMethod("WriteImpl",
            BindingFlags.Instance | BindingFlags.NonPublic,
            new[] { typeof(AionConnection) })!;
        writeImpl.Invoke(packet, new object?[] { null });
        var length = buffer.Position();
        var payload = new byte[length];
        buffer.Flip();
        buffer.Get(payload);
        return payload;
    }

    private sealed class HexReader(byte[] payload)
    {
        private int _pos;

        public bool AtEnd => _pos >= payload.Length;

        public int C() => payload[_pos++];

        public int H() => payload[_pos++] | (payload[_pos++] << 8);

        public int D() => payload[_pos++] | (payload[_pos++] << 8) | (payload[_pos++] << 16) | (payload[_pos++] << 24);
    }
}
