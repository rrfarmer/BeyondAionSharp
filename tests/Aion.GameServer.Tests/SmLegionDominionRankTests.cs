using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Aion.GameServer.Model.LegionDominion;
using Aion.GameServer.Model.Team.Legion;
using Aion.GameServer.Model.Templates;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

/// <summary>
/// Faithful golden coverage for network/aion/serverpackets/SM_LEGION_DOMINION_RANK.
/// Drives the LIVE model graph (LegionDominionLocation + LegionDominionParticipantInfo + Legion via
/// LegionService cache) exactly as LegionDominionService/CM_LEGION_DOMINION_REQUEST_RANKING do, instead
/// of the retired flat-snapshot reworked SmLegionDominionRank packet. Java has no Unsafe golden test for
/// this packet; this migrates the byte coverage onto the faithful WriteImpl path. Private fields are poked
/// via reflection (mirrors the SM_FIND_GROUP faithful-fixture recipe); Legion names come from real cached
/// Legion instances so GetLegionName() resolves without touching the DB.
/// </summary>
public sealed class SmLegionDominionRankTests
{
    [Fact]
    public void WritesJavaPayloadSortedByPointsThenDate()
    {
        ResetLegionCache();
        RegisterLegion(10, "Late");
        RegisterLegion(20, "Early");
        RegisterLegion(30, "Winner");

        var location = MakeLocation(5,
        [
            MakeParticipant(10, 100, 11, 2000),
            MakeParticipant(20, 100, 22, 1000),
            MakeParticipant(30, 200, 33, 3000),
        ]);

        var payload = Write(new SM_LEGION_DOMINION_RANK(location, GetLegion(20)));
        var reader = new HexReader(payload);

        Assert.Equal(5, reader.D());
        Assert.Equal(2, reader.C());
        Assert.Equal(3, reader.H());

        AssertParticipant(reader, 200, 33, 3000, "Winner");
        AssertParticipant(reader, 100, 22, 1000, "Early");
        AssertParticipant(reader, 100, 11, 2000, "Late");
        Assert.True(reader.AtEnd);
    }

    [Fact]
    public void ReplacesLastTopRowWithRequesterWhenRequesterRankIsOutsideTopTwentyFive()
    {
        ResetLegionCache();

        var participants = new List<LegionDominionParticipantInfo>();
        for (var i = 0; i < 26; i++)
        {
            RegisterLegion(1000 + i, "Legion " + i);
            participants.Add(MakeParticipant(1000 + i, 100 - i, 10 + i, 2000 + i));
        }

        RegisterLegion(77, "Requester");
        participants.Add(MakeParticipant(77, 1, 99, 9999));

        var location = MakeLocation(6, participants);

        var payload = Write(new SM_LEGION_DOMINION_RANK(location, GetLegion(77)));
        var reader = new HexReader(payload);

        Assert.Equal(6, reader.D());
        Assert.Equal(27, reader.C());
        Assert.Equal(25, reader.H());

        for (var i = 0; i < 24; i++)
            AssertParticipant(reader, 100 - i, 10 + i, 2000 + i, "Legion " + i);

        AssertParticipant(reader, 1, 99, 9999, "Requester");
        Assert.True(reader.AtEnd);
    }

    private static void AssertParticipant(HexReader reader, int points, int survivedTime, long epochSeconds, string legionName)
    {
        Assert.Equal(points, reader.D());
        Assert.Equal(survivedTime, reader.D());
        Assert.Equal(epochSeconds, reader.Q());
        Assert.Equal(legionName, reader.S());
    }

    // ---- fixtures ----

    private static LegionDominionParticipantInfo MakeParticipant(int legionId, int points, int time, long epochSeconds)
    {
        var info = new LegionDominionParticipantInfo();
        info.SetLegionId(legionId);
        info.SetPoints(points);
        info.SetTime(time);
        // GetDate() returns date.ToUnixTimeMilliseconds()/1000, so a whole-second offset round-trips exactly.
        info.SetDate(DateTimeOffset.FromUnixTimeSeconds(epochSeconds));
        return info;
    }

    private static LegionDominionLocation MakeLocation(int locationId, IReadOnlyList<LegionDominionParticipantInfo> participants)
    {
        // Allocate without the ctor (it builds zoneName from template strings we don't need); poke template (for
        // GetLocationId -> template.GetId()) and the participantInfo SortedDictionary the packet ranks over.
        var template = (LegionDominionLocationTemplate)RuntimeHelpers.GetUninitializedObject(typeof(LegionDominionLocationTemplate));
        SetField(template, "id", locationId);

        var location = (LegionDominionLocation)RuntimeHelpers.GetUninitializedObject(typeof(LegionDominionLocation));
        SetField(location, "template", template);

        var map = new SortedDictionary<int, LegionDominionParticipantInfo>();
        foreach (var info in participants)
            map[info.GetLegionId()] = info;
        SetField(location, "participantInfo", map);
        return location;
    }

    private static void RegisterLegion(int legionId, string name)
    {
        var cache = (ConcurrentDictionary<int, Legion>)GetField(LegionService.GetInstance(), "legionsById");
        cache[legionId] = new Legion(legionId, name);
    }

    private static Legion GetLegion(int legionId) => LegionService.GetInstance().GetLegion(legionId);

    private static void ResetLegionCache()
    {
        var cache = (ConcurrentDictionary<int, Legion>)GetField(LegionService.GetInstance(), "legionsById");
        cache.Clear();
    }

    private static object GetField(object target, string name)
    {
        var type = target.GetType();
        while (type != null)
        {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
                return field.GetValue(target)!;
            type = type.BaseType;
        }
        throw new MissingFieldException(target.GetType().Name, name);
    }

    private static void SetField(object target, string name, object value)
    {
        var type = target.GetType();
        while (type != null)
        {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(target, value);
                return;
            }
            type = type.BaseType;
        }
        throw new MissingFieldException(target.GetType().Name, name);
    }

    private static byte[] Write(SM_LEGION_DOMINION_RANK packet)
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

        public long Q()
        {
            long v = 0;
            for (var i = 0; i < 8; i++)
                v |= (long)payload[_pos++] << (8 * i);
            return v;
        }

        public string S()
        {
            var sb = new StringBuilder();
            while (true)
            {
                int c = payload[_pos++] | (payload[_pos++] << 8);
                if (c == 0)
                    return sb.ToString();
                sb.Append((char)c);
            }
        }
    }
}
